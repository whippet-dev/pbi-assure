namespace PbiAssure.Web;

/// <summary>Delivery status, separate from analysis success and export generation.</summary>
public static class WebInvestigationStatus
{
    public static string Describe(string status) => status switch
    {
        "opened" => "Investigation opened in a new tab. This analysis remains available here.",
        "blocked" => "Your browser blocked the investigation tab. Allow popups for this site, then choose Open investigation again. Your analysis is still available.",
        "closed" => "The investigation tab closed before accepting the analysis. Choose Open investigation again; no rescan is needed.",
        "rejected" => "The viewer could not accept this analysis. It may be an incompatible or incomplete viewer version. Close that tab and try Open investigation again. Your analysis is still available here.",
        "timeout" => "The investigation tab did not confirm receipt. Its assets may not have loaded. Close that tab and choose Open investigation again; no rescan is needed.",
        "cancelled" => "Opening the investigation was cancelled. Your analysis has not been changed.",
        _ => "The investigation could not be opened. Choose Open investigation again; your analysed result is still available and no rescan is needed."
    };
}
