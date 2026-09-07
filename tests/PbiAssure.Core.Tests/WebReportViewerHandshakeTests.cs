using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Opening a tab is not delivering to it. Before the handshake, the browser application announced that
/// the report had opened as soon as window.open returned a window — even if the viewer never loaded,
/// never received the payload, or refused it. Success now means the viewer said so.
/// </summary>
public sealed class WebReportViewerHandshakeTests
{
    [Fact]
    public void AnAcknowledgedDeliveryIsTheOnlySuccess()
    {
        Assert.True(WebReportViewerStatus.IsOpened(WebReportViewerStatus.Opened));
        Assert.Equal("HTML report opened in a new tab.",
            WebReportViewerStatus.Describe(WebReportViewerStatus.Opened));
    }

    [Theory]
    [InlineData(WebReportViewerStatus.Blocked)]
    [InlineData(WebReportViewerStatus.TimedOut)]
    [InlineData(WebReportViewerStatus.Rejected)]
    [InlineData(WebReportViewerStatus.Failed)]
    public void NoOtherOutcomeReportsThatTheReportOpened(string status)
    {
        Assert.False(WebReportViewerStatus.IsOpened(status));

        var message = WebReportViewerStatus.Describe(status);
        Assert.NotEqual(WebReportViewerStatus.Describe(WebReportViewerStatus.Opened), message);
        Assert.DoesNotContain("opened in a new tab", message, StringComparison.OrdinalIgnoreCase);
        // Every failure leaves somewhere else to go.
        Assert.Contains("download the HTML report", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATimeoutIsDistinguishedFromABlockedTab()
    {
        // They need different advice: one is a pop-up setting, the other is a tab that is already open.
        Assert.NotEqual(
            WebReportViewerStatus.Describe(WebReportViewerStatus.Blocked),
            WebReportViewerStatus.Describe(WebReportViewerStatus.TimedOut));
        Assert.Contains("pop-ups", WebReportViewerStatus.Describe(WebReportViewerStatus.Blocked),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not confirm", WebReportViewerStatus.Describe(WebReportViewerStatus.TimedOut),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-this-version-does-not-know")]
    [InlineData("Opened")]
    public void AnUnrecognisedStatusIsADeliveryFailureRatherThanSuccess(string? status)
    {
        // Case-sensitive on purpose: only the exact contract value counts as acknowledged.
        Assert.False(WebReportViewerStatus.IsOpened(status));
        Assert.Equal(WebReportViewerStatus.Describe(WebReportViewerStatus.Failed),
            WebReportViewerStatus.Describe(status));
    }

    [Fact]
    public void HomeAsksForTheStatusAndShowsWhatItDescribes()
    {
        var markup = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

        Assert.Contains("InvokeAsync<string>(\"pbiAssureDownload.open\"", markup, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Describe(status)", markup, StringComparison.Ordinal);
        // The old unconditional success string is gone from the page.
        Assert.DoesNotContain("? \"HTML report opened in a new tab.\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSenderWaitsForAnAcknowledgementTiedToThisDelivery()
    {
        var sender = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "download.js"));

        Assert.Contains("PBIASSURE_VIEWER_ACK_TIMEOUT_MS = 10000", sender, StringComparison.Ordinal);
        Assert.Contains("pbi-assure-report-viewer-ack", sender, StringComparison.Ordinal);
        Assert.Contains("message.deliveryId === deliveryId", sender, StringComparison.Ordinal);
        // Origin and source validation both survive on the receiving side of the handshake.
        Assert.Contains("event.origin !== viewerUrl.origin || event.source !== reportWindow", sender, StringComparison.Ordinal);
        // Every send still names the viewer origin explicitly; no wildcard target crept in.
        Assert.DoesNotContain("\"*\"", sender, StringComparison.Ordinal);
        Assert.Contains("}, viewerUrl.origin);", sender, StringComparison.Ordinal);
    }

    [Fact]
    public void TheViewerAcknowledgesOnlyAfterValidatingThePayload()
    {
        var viewer = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "report-viewer.js"));

        var validation = viewer.IndexOf("event.origin !== window.location.origin", StringComparison.Ordinal);
        var contentCheck = viewer.IndexOf("typeof event.data.content !== \"string\"", StringComparison.Ordinal);
        var acceptedAck = viewer.IndexOf("acknowledge(true)", StringComparison.Ordinal);
        var write = viewer.IndexOf("document.write(", StringComparison.Ordinal);

        Assert.True(validation >= 0 && contentCheck > validation,
            "Origin and source are checked before the payload is inspected.");
        Assert.True(acceptedAck > contentCheck, "A positive acknowledgement follows payload validation.");
        Assert.True(write > acceptedAck, "The acknowledgement is sent before document.write destroys the page.");
        Assert.Contains("acknowledge(false)", viewer, StringComparison.Ordinal);
        Assert.Contains("deliveryId: event.data.deliveryId", viewer, StringComparison.Ordinal);
    }

    [Fact]
    public void DownloadingTheReportIsUnchanged()
    {
        var sender = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "download.js"));
        var save = sender[sender.IndexOf("save(filename", StringComparison.Ordinal)..];

        // The save path is untouched by the handshake: same blob, same anchor, same revoke.
        Assert.Contains("new Blob([content], { type: mimeType })", save, StringComparison.Ordinal);
        Assert.Contains("link.download = filename", save, StringComparison.Ordinal);
        Assert.Contains("URL.revokeObjectURL(objectUrl)", save, StringComparison.Ordinal);
        Assert.DoesNotContain("deliveryId", save, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
