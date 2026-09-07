using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Opening the picker to look at another project is not a decision to throw the current one away.
/// Cancelling, or picking something that turns out not to be a project, must leave the work in progress
/// exactly where it was — including the retained handle a later refresh depends on.
/// </summary>
public sealed class BrowserProjectReplacementTests
{
    [Fact]
    public async Task SuccessfulSelectionAndScanEstablishTheWorkingState()
    {
        var workspace = await Established();

        Assert.Equal("ProjectA", workspace.Selection!.DisplayName);
        Assert.True(workspace.HasResults);
        Assert.Contains(workspace.Inventory!.SemanticObjectUsages, usage => usage.ObjectName == "AmountA");
    }

    [Fact]
    public async Task CancellingAReplacementLeavesTheProjectResultsAndExportStateUntouched()
    {
        var before = await Established() with { ExportPanelOpen = true };
        var picker = Picker.ThatThrows(new JSLikeException("[PBIASSURE:CANCELLED] Project selection was cancelled."));

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(before, picker, false, Describe);

        Assert.False(outcome.Replaced);
        Assert.Equal(BrowserProjectSelectionWorkflow.CancelledMessage, outcome.Message);
        // Same project, same analysis, same export configuration version and panel state.
        Assert.Same(before.Selection, outcome.Workspace.Selection);
        Assert.Same(before.Inventory, outcome.Workspace.Inventory);
        Assert.Equal(before.ExportConfigurationVersion, outcome.Workspace.ExportConfigurationVersion);
        Assert.True(outcome.Workspace.ExportPanelOpen);
        Assert.Equal(before, outcome.Workspace);
    }

    [Fact]
    public async Task CancellingDoesNotRunAScanOrLeavePartialReplacementState()
    {
        var before = await Established();
        var picker = Picker.ThatThrows(new JSLikeException("[PBIASSURE:CANCELLED] cancelled"));

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(before, picker, false, Describe);

        Assert.Equal(1, picker.Attempts);
        Assert.Contains(outcome.Workspace.Inventory!.SemanticObjectUsages, usage => usage.ObjectName == "AmountA");
        Assert.DoesNotContain(outcome.Workspace.Inventory.SemanticObjectUsages, usage => usage.ObjectName == "AmountB");
    }

    [Fact]
    public async Task TheRetainedDirectoryStaysUsableAfterACancelledReplacement()
    {
        var folder = FolderA();
        var before = (await Established(folder)) with { ExportPanelOpen = true };

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            before, Picker.ThatThrows(new JSLikeException("[PBIASSURE:CANCELLED] cancelled")), false, Describe);

        // The handle was never replaced, so the original project can still be re-enumerated and read.
        Assert.True(outcome.Workspace.Selection!.CanRefresh);
        var prepared = await BrowserProjectAnalysis.PrepareAsync(outcome.Workspace.Selection, folder, refresh: true);
        Assert.Equal(1, folder.EnumerationCount);
        Assert.NotEmpty(prepared.Files);
    }

    [Fact]
    public async Task AnalyseAgainAfterACancelledReplacementStillRefreshesTheOriginalProject()
    {
        var folder = FolderA();
        var before = await Established(folder);
        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            before, Picker.ThatThrows(new JSLikeException("[PBIASSURE:CANCELLED] cancelled")), false, Describe);

        // A change saved to the original project after the cancelled pick is still picked up.
        folder.Write(
            "ProjectA.SemanticModel/definition/tables/Added.tmdl",
            "table Added\n\n\tcolumn AddedAfterCancel\n\t\tdataType: string\n\t\tsourceColumn: AddedAfterCancel\n");
        var prepared = await BrowserProjectAnalysis.PrepareAsync(outcome.Workspace.Selection!, folder, refresh: true);

        Assert.Contains(Scan(prepared).SemanticObjectUsages, usage => usage.ObjectName == "AddedAfterCancel");
    }

    [Theory]
    [InlineData("[PBIASSURE:BLOCKED] Folder access was blocked.")]
    [InlineData("[PBIASSURE:PICKER_FAILED] The project folder could not be opened.")]
    public async Task APickerFailurePreservesThePreviousState(string error)
    {
        var before = await Established();

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            before, Picker.ThatThrows(new JSLikeException(error)), false, Describe);

        Assert.False(outcome.Replaced);
        Assert.Equal(before, outcome.Workspace);
        Assert.NotNull(outcome.Message);
    }

    [Fact]
    public async Task AnInvalidReplacementSelectionPreservesThePreviousState()
    {
        var before = await Established();
        // A folder with no .pbip at its root: the picker returns, validation rejects it.
        var picker = Picker.Returning(new BrowserProjectSelection(
            "NotAProject",
            [new BrowserProjectFileManifest("Some.SemanticModel/definition/tables/Fact.tmdl", 10)],
            10, 1, 3));

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(before, picker, false, Describe);

        Assert.False(outcome.Replaced);
        Assert.Equal(before, outcome.Workspace);
        Assert.Contains("No Power BI project was found", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASuccessfulReplacementSwapsOnlyAfterValidation()
    {
        var before = (await Established()) with { ExportPanelOpen = true };
        var replacement = FolderB().Enumerate();

        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            before, Picker.Returning(replacement), false, Describe);

        Assert.True(outcome.Replaced);
        Assert.Null(outcome.Message);
        Assert.Equal("ProjectB", outcome.Workspace.Selection!.DisplayName);
        // The previous project's derived state goes at this transition, and only here.
        Assert.Null(outcome.Workspace.Inventory);
        Assert.False(outcome.Workspace.ExportPanelOpen);
        Assert.Equal(before.ExportConfigurationVersion + 1, outcome.Workspace.ExportConfigurationVersion);
    }

    [Fact]
    public async Task CancellingTheFirstPickWithNoProjectSelectedBehavesAsBefore()
    {
        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            BrowserProjectWorkspace.Empty,
            Picker.ThatThrows(new JSLikeException("[PBIASSURE:CANCELLED] cancelled")),
            false,
            Describe);

        Assert.False(outcome.Replaced);
        Assert.Equal(BrowserProjectSelectionWorkflow.CancelledMessage, outcome.Message);
        Assert.False(outcome.Workspace.HasProject);
        Assert.False(outcome.Workspace.HasResults);
        Assert.Equal(BrowserProjectWorkspace.Empty, outcome.Workspace);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    /// <summary>Mirrors how Home maps a picker exception to the message it shows.</summary>
    private static string Describe(Exception exception) => exception switch
    {
        BrowserProjectSelectionException selectionException => selectionException.Message,
        JSLikeException js when js.Message.Contains("CANCELLED", StringComparison.OrdinalIgnoreCase) =>
            BrowserProjectSelectionWorkflow.CancelledMessage,
        JSLikeException js when js.Message.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase) =>
            "Folder access was blocked.",
        _ => "The project folder could not be opened.",
    };

    private static async Task<BrowserProjectWorkspace> Established(FakeFolder? folder = null)
    {
        folder ??= FolderA();
        var outcome = await BrowserProjectSelectionWorkflow.ChooseAsync(
            BrowserProjectWorkspace.Empty, Picker.Returning(folder.Enumerate()), false, Describe);
        Assert.True(outcome.Replaced);

        var prepared = await BrowserProjectAnalysis.PrepareAsync(outcome.Workspace.Selection!, folder, refresh: false);
        return outcome.Workspace with { Inventory = Scan(prepared) };
    }

    private static ProjectInventory Scan(BrowserProjectAnalysisInput prepared) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource(prepared.Selection.DisplayName, prepared.Files));

    private static FakeFolder FolderA() => Folder("ProjectA", "AmountA");

    private static FakeFolder FolderB() => Folder("ProjectB", "AmountB");

    private static FakeFolder Folder(string name, string column)
    {
        var folder = new FakeFolder(name);
        folder.Write($"{name}.pbip", "{}");
        folder.Write($"{name}.SemanticModel/definition.pbism", "{}");
        folder.Write(
            $"{name}.SemanticModel/definition/tables/Fact.tmdl",
            $"table Fact\n\n\tcolumn {column}\n\t\tdataType: int64\n\t\tsourceColumn: {column}\n");
        return folder;
    }

    private sealed class JSLikeException(string message) : Exception(message);

    private sealed class Picker : IBrowserProjectPicker
    {
        private BrowserProjectSelection? result;
        private Exception? failure;

        public int Attempts { get; private set; }

        public static Picker Returning(BrowserProjectSelection selection) => new() { result = selection };

        public static Picker ThatThrows(Exception exception) => new() { failure = exception };

        public ValueTask<BrowserProjectSelection> ChooseAsync(bool useFallback)
        {
            Attempts++;
            return failure is not null ? throw failure : ValueTask.FromResult(result!);
        }
    }

    private sealed class FakeFolder(string displayName) : IBrowserProjectFileAccess
    {
        private readonly Dictionary<string, byte[]> contents = new(StringComparer.Ordinal);

        public int EnumerationCount { get; private set; }

        public void Write(string relativePath, string text) =>
            contents[relativePath] = Encoding.UTF8.GetBytes(text);

        public BrowserProjectSelection Enumerate() => new(
            displayName,
            contents.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new BrowserProjectFileManifest(entry.Key, entry.Value.Length))
                .ToList(),
            contents.Values.Sum(value => (long)value.Length),
            contents.Count,
            contents.Keys.Max(path => path.Count(character => character == '/')))
        {
            CanRefresh = true,
        };

        public ValueTask<BrowserProjectSelection> RefreshAsync()
        {
            EnumerationCount++;
            return ValueTask.FromResult(Enumerate());
        }

        public ValueTask<byte[]> ReadAsync(string relativePath) => ValueTask.FromResult(contents[relativePath]);
    }
}
