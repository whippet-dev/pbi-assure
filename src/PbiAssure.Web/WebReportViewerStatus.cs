namespace PbiAssure.Web;

/// <summary>
/// The two HTML experiences the browser application generates from one analysis, named as the product
/// names them, so that every status message says which one it is about.
/// </summary>
public sealed record WebReportKind(string Name, string DownloadName)
{
    public static readonly WebReportKind InteractiveReport = new("interactive report", "Download report");

    public static readonly WebReportKind ApparentlyUnusedReview = new("apparently unused review", "Download review");

    /// <summary>The name with its first letter raised, for the start of a sentence.</summary>
    public string SentenceName => char.ToUpperInvariant(Name[0]) + Name[1..];
}

/// <summary>
/// What happened when a report was handed to the viewer.
///
/// Opening a tab is not the same as delivering to it. The viewer acknowledges the report only after it
/// has validated the payload and committed to rendering it, so anything short of that acknowledgement
/// is reported as a failure with somewhere else to go, rather than as a report that opened. Every
/// message names the report involved: someone opening the apparently unused review is never told to
/// download the interactive report instead.
/// </summary>
public static class WebReportViewerStatus
{
    /// <summary>The viewer acknowledged that it accepted the report.</summary>
    public const string Opened = "opened";

    /// <summary>The browser refused to open the viewer tab.</summary>
    public const string Blocked = "blocked";

    /// <summary>The viewer never acknowledged within the handshake timeout.</summary>
    public const string TimedOut = "timeout";

    /// <summary>The viewer answered, but would not accept the report.</summary>
    public const string Rejected = "rejected";

    /// <summary>The report could not be handed to the viewer at all.</summary>
    public const string Failed = "failed";

    public static bool IsOpened(string? status) =>
        string.Equals(status, Opened, StringComparison.Ordinal);

    /// <summary>What the page shows while the viewer handshake is in progress.</summary>
    public static string Opening(WebReportKind report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"Opening {report.Name}…";
    }

    /// <summary>
    /// An unrecognised status is treated as a delivery failure, not as success: a viewer that answered
    /// with something this version does not understand has not confirmed anything.
    /// </summary>
    public static string Describe(string? status, WebReportKind report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var download = $"use {report.DownloadName} to save the {report.Name} instead";
        return status switch
        {
            Opened => $"{report.SentenceName} opened in a new tab.",
            Blocked => $"The browser blocked the new tab. Allow pop-ups for this site, or {download}.",
            TimedOut => $"The viewer did not confirm it received the {report.Name}. Check the new tab, or {download}.",
            Rejected => $"The viewer could not accept the {report.Name}. {Capitalise(download)}.",
            _ => $"The {report.Name} could not be delivered to the viewer. {Capitalise(download)}.",
        };
    }

    /// <summary>
    /// A download was handed to the browser. Nothing more is known: the browser may still prompt, block
    /// or cancel it, so this never claims the file has been kept.
    /// </summary>
    public static string DownloadStarted(WebReportKind report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"Download started: {report.Name}.";
    }

    public static string DownloadFailed(WebReportKind report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"The {report.Name} could not be generated or downloaded.";
    }

    public static string OpenFailed(WebReportKind report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return $"The {report.Name} could not be generated or opened.";
    }

    private static string Capitalise(string value) => char.ToUpperInvariant(value[0]) + value[1..];
}
