using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Analysing again must describe the project as it is now. A folder enumerated once is only a record of
/// how it looked then, so a rerun that reread that record would report a saved-over project as current —
/// the failure mode this covers.
/// </summary>
public sealed class BrowserProjectRerunTests
{
    [Fact]
    public async Task FirstAnalysisScansTheSelectedFilesWithoutReEnumerating()
    {
        var folder = Folder();
        var selection = folder.Enumerate();

        var prepared = await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: false);

        Assert.Equal(0, folder.EnumerationCount);
        Assert.Equal(selection.Files.Count, prepared.Files.Count);
        Assert.Contains(Scan(prepared).SemanticObjectUsages, usage => usage.ObjectName == "Amount");
    }

    [Fact]
    public async Task AnalyseAgainDiscoversAFileAddedSinceTheFirstScan()
    {
        var folder = Folder();
        var selection = folder.Enumerate();
        folder.Write(
            "Sales.SemanticModel/definition/tables/Added.tmdl",
            "table Added\n\n\tcolumn AddedColumn\n\t\tdataType: string\n\t\tsourceColumn: AddedColumn\n");

        var prepared = await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: true);

        Assert.Contains(prepared.Files, file => file.RelativePath.EndsWith("Added.tmdl", StringComparison.Ordinal));
        Assert.Contains(Scan(prepared).SemanticObjectUsages, usage => usage.ObjectName == "AddedColumn");
    }

    [Fact]
    public async Task AnalyseAgainReadsTheNewContentsOfAChangedFile()
    {
        var folder = Folder();
        var selection = folder.Enumerate();
        folder.Write(
            "Sales.SemanticModel/definition/tables/Fact.tmdl",
            "table Fact\n\n\tcolumn Renamed\n\t\tdataType: int64\n\t\tsourceColumn: Renamed\n");

        var usages = Scan(await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: true))
            .SemanticObjectUsages;

        Assert.Contains(usages, usage => usage.ObjectName == "Renamed");
        Assert.DoesNotContain(usages, usage => usage.ObjectName == "Amount");
    }

    [Fact]
    public async Task AnalyseAgainStopsIncludingADeletedFile()
    {
        var folder = Folder();
        folder.Write(
            "Sales.SemanticModel/definition/tables/Temporary.tmdl",
            "table Temporary\n\n\tcolumn TemporaryColumn\n\t\tdataType: string\n\t\tsourceColumn: TemporaryColumn\n");
        var selection = folder.Enumerate();
        Assert.Contains(selection.Files, file => file.RelativePath.EndsWith("Temporary.tmdl", StringComparison.Ordinal));

        folder.Delete("Sales.SemanticModel/definition/tables/Temporary.tmdl");
        var prepared = await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: true);

        Assert.DoesNotContain(prepared.Files, file => file.RelativePath.EndsWith("Temporary.tmdl", StringComparison.Ordinal));
        Assert.DoesNotContain(Scan(prepared).SemanticObjectUsages, usage => usage.ObjectName == "TemporaryColumn");
    }

    [Fact]
    public async Task AnalyseAgainEnumeratesTheDirectoryRatherThanReusingTheManifest()
    {
        var folder = Folder();
        var selection = folder.Enumerate();

        await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: true);

        Assert.Equal(1, folder.EnumerationCount);
        // Every file read came from the refreshed manifest, not the one captured at selection.
        Assert.All(folder.PathsRead, path => Assert.Contains(path, folder.CurrentPaths));
    }

    [Fact]
    public async Task AFailedRefreshLeavesThePreviousResultUntouched()
    {
        var folder = Folder();
        var selection = folder.Enumerate();
        var established = Scan(await BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: false));

        // The folder becomes something that cannot be validated as a project.
        folder.Delete("Sales.pbip");
        var readsBeforeRefresh = folder.PathsRead.Count;

        await Assert.ThrowsAsync<BrowserProjectSelectionException>(
            () => BrowserProjectAnalysis.PrepareAsync(selection, folder, refresh: true));
        // Validation rejected the folder before a single file was read, so no partially refreshed
        // inventory could have reached a scan and replaced the result already on screen.
        Assert.Equal(readsBeforeRefresh, folder.PathsRead.Count);
        Assert.Contains(established.SemanticObjectUsages, usage => usage.ObjectName == "Amount");
    }

    [Fact]
    public void ASnapshotSelectionRequiresReselectionRatherThanPretendingToRefresh()
    {
        var snapshot = Folder().Enumerate() with { CanRefresh = false };

        Assert.Equal(BrowserProjectRerunAction.RequiresReselection,
            BrowserProjectAnalysis.Decide(snapshot, hasResults: true));
        Assert.Contains("Choose the project folder again", BrowserProjectAnalysis.ReselectionRequiredMessage,
            StringComparison.Ordinal);
        Assert.Contains("snapshot", BrowserProjectAnalysis.ReselectionRequiredMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstSelectionBehaviourIsUnchangedForBothPickers()
    {
        var retained = Folder().Enumerate();
        var snapshot = retained with { CanRefresh = false };

        // Nothing has been analysed yet, so neither picker re-enumerates or demands reselection.
        Assert.Equal(BrowserProjectRerunAction.Analyse, BrowserProjectAnalysis.Decide(retained, hasResults: false));
        Assert.Equal(BrowserProjectRerunAction.Analyse, BrowserProjectAnalysis.Decide(snapshot, hasResults: false));
        Assert.Equal(BrowserProjectRerunAction.RefreshThenAnalyse, BrowserProjectAnalysis.Decide(retained, hasResults: true));
        Assert.Equal(BrowserProjectRerunAction.None, BrowserProjectAnalysis.Decide(null, hasResults: false));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static ProjectInventory Scan(BrowserProjectAnalysisInput prepared) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource(prepared.Selection.DisplayName, prepared.Files));

    private static FakeDirectory Folder()
    {
        var folder = new FakeDirectory("Sales");
        folder.Write("Sales.pbip", "{}");
        folder.Write("Sales.SemanticModel/definition.pbism", "{}");
        folder.Write(
            "Sales.SemanticModel/definition/tables/Fact.tmdl",
            "table Fact\n\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n");
        return folder;
    }

    /// <summary>
    /// Stands in for a retained directory handle: enumeration reads whatever the folder contains at that
    /// moment, exactly as re-enumerating a real handle does.
    /// </summary>
    private sealed class FakeDirectory(string displayName) : IBrowserProjectFileAccess
    {
        private readonly Dictionary<string, byte[]> contents = new(StringComparer.Ordinal);

        public int EnumerationCount { get; private set; }

        public List<string> PathsRead { get; } = [];

        public IReadOnlyCollection<string> CurrentPaths => contents.Keys;

        public void Write(string relativePath, string text) =>
            contents[relativePath] = Encoding.UTF8.GetBytes(text);

        public void Delete(string relativePath) => contents.Remove(relativePath);

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

        public ValueTask<byte[]> ReadAsync(string relativePath)
        {
            PathsRead.Add(relativePath);
            return contents.TryGetValue(relativePath, out var value)
                ? ValueTask.FromResult(value)
                : throw new InvalidOperationException($"'{relativePath}' is no longer in the folder.");
        }
    }
}
