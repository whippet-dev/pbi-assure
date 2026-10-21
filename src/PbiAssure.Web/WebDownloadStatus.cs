namespace PbiAssure.Web;

/// <summary>Handed to the browser, not confirmation that a file was saved.</summary>
public static class WebDownloadStatus
{
    public static string Started(string output) => $"Download started: {output}.";

    public static string Failed(string output) => $"The {output} could not be generated or downloaded.";
}
