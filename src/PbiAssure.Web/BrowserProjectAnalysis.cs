using PbiAssure.Core.Scanning;

namespace PbiAssure.Web;

/// <summary>
/// The browser file access an analysis run needs, separated from the JS interop that provides it so the
/// rerun rules can be exercised without a browser.
/// </summary>
public interface IBrowserProjectFileAccess
{
    /// <summary>
    /// Re-enumerates the retained directory handle and returns what the project contains now. Only
    /// called for a selection that reported <see cref="BrowserProjectSelection.CanRefresh"/>.
    /// </summary>
    ValueTask<BrowserProjectSelection> RefreshAsync();

    ValueTask<byte[]> ReadAsync(string relativePath);
}

/// <summary>What a run of "Analyse" should do for the selection currently held.</summary>
public enum BrowserProjectRerunAction
{
    /// <summary>Nothing is selected.</summary>
    None,

    /// <summary>Read and scan what the current selection describes.</summary>
    Analyse,

    /// <summary>Re-enumerate the directory first, because the project may have changed on disk.</summary>
    RefreshThenAnalyse,

    /// <summary>
    /// The browser only ever handed over a snapshot of files, so there is nothing to re-enumerate and a
    /// rerun could not honestly claim to be current.
    /// </summary>
    RequiresReselection,
}

public sealed record BrowserProjectAnalysisInput(
    BrowserProjectSelection Selection,
    IReadOnlyList<ProjectFileContent> Files);

/// <summary>
/// Rules for analysing a project that was chosen earlier in the session.
///
/// The first analysis reads the selection the picker just produced. A later one must not: Power BI may
/// have been saved since, and a directory enumerated once is only a description of how the project
/// looked then. Where the browser retained a directory handle, the project is enumerated again so files
/// added, changed or deleted since are all accounted for. Where it did not, no amount of rereading the
/// original snapshot would notice a new file, so the honest answer is to ask for the folder again rather
/// than present a stale result as current.
/// </summary>
public static class BrowserProjectAnalysis
{
    public const string ReselectionRequiredMessage =
        "This browser gave PBI Assure a one-time snapshot of the folder, so it cannot check for changes " +
        "you have saved since. Choose the project folder again to analyse its current contents.";

    public static BrowserProjectRerunAction Decide(BrowserProjectSelection? selection, bool hasResults)
    {
        if (selection is null)
        {
            return BrowserProjectRerunAction.None;
        }

        if (!hasResults)
        {
            return BrowserProjectRerunAction.Analyse;
        }

        return selection.CanRefresh
            ? BrowserProjectRerunAction.RefreshThenAnalyse
            : BrowserProjectRerunAction.RequiresReselection;
    }

    /// <summary>
    /// Produces the input for one scan. When <paramref name="refresh"/> is set the directory is
    /// enumerated and validated first, and this throws rather than returning if that fails — the caller
    /// keeps whatever result it is already showing, because a scan never starts on an unvalidated
    /// inventory.
    /// </summary>
    public static async Task<BrowserProjectAnalysisInput> PrepareAsync(
        BrowserProjectSelection selection,
        IBrowserProjectFileAccess access,
        bool refresh,
        BrowserProjectSelectionLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(access);

        var current = selection;
        if (refresh)
        {
            var reenumerated = await access.RefreshAsync().ConfigureAwait(false);
            if (reenumerated is null)
            {
                throw new BrowserProjectSelectionException(
                    "The selected project folder could not be read again. The analysis already shown is unchanged.");
            }

            // Validated before it is used, exactly as a first selection is: a folder that has become
            // something PBI Assure cannot analyse must fail here, not part-way through a scan.
            current = BrowserProjectSelectionValidator.Validate(
                reenumerated with { CanRefresh = selection.CanRefresh }, limits);
        }

        var files = new List<ProjectFileContent>(current.Files.Count);
        foreach (var file in current.Files)
        {
            files.Add(new ProjectFileContent(file.RelativePath, await access.ReadAsync(file.RelativePath).ConfigureAwait(false)));
        }

        return new BrowserProjectAnalysisInput(current, files);
    }
}
