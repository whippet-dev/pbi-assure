namespace PbiAssure.Web;

/// <summary>
/// What happened when the interactive report was handed to the viewer.
///
/// Opening a tab is not the same as delivering to it. The viewer acknowledges the report only after it
/// has validated the payload and committed to rendering it, so anything short of that acknowledgement
/// is reported as a failure with somewhere else to go, rather than as a report that opened.
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

    /// <summary>
    /// An unrecognised status is treated as a delivery failure, not as success: a viewer that answered
    /// with something this version does not understand has not confirmed anything.
    /// </summary>
    public static string Describe(string? status) => status switch
    {
        Opened => "HTML report opened in a new tab.",
        Blocked => "The browser blocked the new tab. Allow pop-ups for this site or download the HTML report instead.",
        TimedOut => "The report viewer did not confirm it received the report. Check the new tab, or download the HTML report instead.",
        Rejected => "The report viewer could not accept the report. Download the HTML report instead.",
        _ => "The report could not be delivered to the viewer. Download the HTML report instead.",
    };
}
