using PbiAssure.Core.Inventory;

namespace PbiAssure.Web;

/// <summary>Opening the picker to replace the project currently being worked on.</summary>
public interface IBrowserProjectPicker
{
    ValueTask<BrowserProjectSelection> ChooseAsync(bool useFallback);
}

/// <summary>
/// The project a person is working on, together with what has been derived from it.
///
/// Held as one value so replacing a project is a single transition rather than several fields cleared
/// in sequence. Nothing here is cleared in anticipation of a replacement that might not arrive.
/// </summary>
public sealed record BrowserProjectWorkspace(
    BrowserProjectSelection? Selection,
    ProjectInventory? Inventory,
    int ExportConfigurationVersion,
    bool ExportPanelOpen)
{
    public static BrowserProjectWorkspace Empty { get; } = new(null, null, 0, false);

    public bool HasProject => Selection is not null;

    public bool HasResults => Inventory is not null;

    /// <summary>
    /// Takes on a replacement project. Everything derived from the previous one goes at this point and
    /// only at this point: the results describe a project that is no longer selected, and the export
    /// configuration was built against its columns.
    /// </summary>
    public BrowserProjectWorkspace Accept(BrowserProjectSelection replacement) => this with
    {
        Selection = replacement,
        Inventory = null,
        ExportConfigurationVersion = ExportConfigurationVersion + 1,
        ExportPanelOpen = false,
    };
}

/// <summary>The result of one attempt to choose a project, replacement or first.</summary>
public sealed record BrowserProjectSelectionOutcome(
    BrowserProjectWorkspace Workspace,
    string? Message,
    bool Replaced);

/// <summary>
/// Choosing a project, including choosing a different one part-way through a piece of work.
///
/// Selection is staged: the picker runs, the result is validated, and only a selection that survives
/// both replaces what is on screen. A person who opens the picker to look and then changes their mind
/// has not asked for anything to be discarded, so cancelling — or a picker that fails, or a folder that
/// turns out not to be a project — leaves the previous project, its analysis and its export
/// configuration exactly as they were.
/// </summary>
public static class BrowserProjectSelectionWorkflow
{
    public const string CancelledMessage = "Project selection was cancelled.";

    public static async Task<BrowserProjectSelectionOutcome> ChooseAsync(
        BrowserProjectWorkspace current,
        IBrowserProjectPicker picker,
        bool useFallback,
        Func<Exception, string> describeFailure,
        BrowserProjectSelectionLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(describeFailure);

        BrowserProjectSelection validated;
        try
        {
            var picked = await picker.ChooseAsync(useFallback).ConfigureAwait(false);
            validated = BrowserProjectSelectionValidator.Validate(picked, limits);
        }
        catch (Exception exception)
        {
            // Nothing has been touched yet, so the previous workspace is simply returned as it stands.
            return new BrowserProjectSelectionOutcome(current, describeFailure(exception), Replaced: false);
        }

        return new BrowserProjectSelectionOutcome(current.Accept(validated), Message: null, Replaced: true);
    }
}
