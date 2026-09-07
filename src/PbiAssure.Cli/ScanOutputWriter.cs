using System.Text;

namespace PbiAssure.Cli;

/// <summary>One file of a run's output, and where it belongs.</summary>
public sealed record ScanOutputFile(ScanOutputPlan Plan, string Content, Encoding? Encoding = null);

/// <summary>
/// The run's timestamped output was written and is complete, but the <c>latest.*</c> mirrors were not
/// updated. Those mirrors have been removed rather than left disagreeing with each other.
/// </summary>
public sealed class ScanLatestOutputException(string message, Exception innerException)
    : IOException(message, innerException);

/// <summary>
/// Writes the files a run produces.
///
/// Writing them one after another meant a failure part-way through left a historical set that looked
/// complete but was not — an HTML report with no CSV beside it, or a half-written file from a disk
/// that filled up. They are now staged beside their destinations and moved into place only once every
/// one of them has been written.
/// </summary>
public static class ScanOutputWriter
{
    private const string StagingSuffix = ".pbiassure-staging-";

    public static Task WriteAsync(ScanOutputPlan outputPlan, string content, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(outputPlan);
        ArgumentNullException.ThrowIfNull(content);

        return WriteSetAsync([new ScanOutputFile(outputPlan, content, encoding)]);
    }

    /// <summary>
    /// Writes every file as one set, in two phases with different promises.
    ///
    /// The timestamped files are the run's record and are committed first, all or none. A generated
    /// historical name is claimed with a non-overwriting move, so two runs racing for the same name
    /// cannot silently replace each other: the loser fails and rolls back rather than destroying the
    /// winner's output. If any of them fails, none is left behind and no <c>latest.*</c> file has been
    /// touched yet.
    ///
    /// The <c>latest.*</c> mirrors are convenience copies of that record, updated only once it is
    /// complete. They are moved into place rather than written in place, so a reader never sees a
    /// half-written one. If either fails, the record stays — it is the canonical output and is never
    /// removed for a mirror's sake — and both mirrors are deleted instead, because a new report beside
    /// an old CSV is exactly the mismatch this is here to prevent. That is reported as a
    /// <see cref="ScanLatestOutputException"/>, never as success.
    ///
    /// This is ordinary-process failure safety. A process killed between two moves can still leave one
    /// of them behind; recovering from that needs a journal, which is far more machinery than this
    /// tool warrants.
    /// </summary>
    public static async Task WriteSetAsync(IReadOnlyList<ScanOutputFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var historical = new List<StagedFile>();
        var latest = new List<StagedFile>();
        var committedHistorical = new List<string>();
        try
        {
            foreach (var file in files)
            {
                ArgumentNullException.ThrowIfNull(file.Content);
                historical.Add(await StageAsync(file.Plan.HistoricalPath, file.Content, file.Encoding,
                    replaceExisting: !file.Plan.IsGeneratedName).ConfigureAwait(false));

                if (file.Plan.LatestPath is not null)
                {
                    latest.Add(await StageAsync(file.Plan.LatestPath, file.Content, file.Encoding,
                        replaceExisting: true).ConfigureAwait(false));
                }
            }

            foreach (var file in historical)
            {
                File.Move(file.StagedPath, file.FinalPath, file.ReplaceExisting);
                committedHistorical.Add(file.FinalPath);
            }
        }
        catch
        {
            // Nothing of this run survives, so nothing incomplete is exposed.
            Delete(historical.Concat(latest).Select(file => file.StagedPath).Concat(committedHistorical));
            throw;
        }

        try
        {
            foreach (var file in latest)
            {
                File.Move(file.StagedPath, file.FinalPath, file.ReplaceExisting);
            }
        }
        catch (Exception exception)
        {
            // The run's record is committed and stays. Its mirrors go, all of them, so none is left
            // describing a different run from the one beside it.
            Delete(latest.Select(file => file.StagedPath).Concat(latest.Select(file => file.FinalPath)));
            throw new ScanLatestOutputException(
                "The report and semantic usage CSV for this run were saved, but the latest files could " +
                "not be updated and have been removed so they cannot disagree with each other.",
                exception);
        }
    }

    private static async Task<StagedFile> StageAsync(
        string outputPath,
        string content,
        Encoding? encoding,
        bool replaceExisting)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Staged beside the destination so the commit is a rename within one volume.
        var stagedPath = fullOutputPath + StagingSuffix + Guid.NewGuid().ToString("N")[..8];
        await File.WriteAllTextAsync(
            stagedPath, content, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .ConfigureAwait(false);
        return new StagedFile(stagedPath, fullOutputPath, replaceExisting);
    }

    /// <summary>
    /// Best effort: a cleanup that itself fails must not replace the error that caused it, which is
    /// the one describing what actually went wrong. Paths already moved simply are not there.
    /// </summary>
    private static void Delete(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record StagedFile(string StagedPath, string FinalPath, bool ReplaceExisting);
}
