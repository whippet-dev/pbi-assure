using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Opening a tab is not delivering to it. Before the handshake, the browser application announced that
/// the report had opened as soon as window.open returned a window — even if the viewer never loaded,
/// never received the payload, or refused it. Success now means the viewer said so, and every message
/// names the report it is about, so the apparently unused review is never described as the
/// interactive report or vice versa.
/// </summary>
public sealed class WebReportViewerHandshakeTests
{
    public static TheoryData<WebReportKind> Reports => new(WebReportKind.InteractiveReport, WebReportKind.ApparentlyUnusedReview);

    [Theory]
    [MemberData(nameof(Reports))]
    public void AnAcknowledgedDeliveryIsTheOnlySuccess(WebReportKind report)
    {
        Assert.True(WebReportViewerStatus.IsOpened(WebReportViewerStatus.Opened));
        Assert.Equal($"{report.SentenceName} opened in a new tab.",
            WebReportViewerStatus.Describe(WebReportViewerStatus.Opened, report));
    }

    [Fact]
    public void TheTwoReportsAreNamedAsTheProductNamesThem()
    {
        Assert.Equal("Interactive report opened in a new tab.",
            WebReportViewerStatus.Describe(WebReportViewerStatus.Opened, WebReportKind.InteractiveReport));
        Assert.Equal("Apparently unused review opened in a new tab.",
            WebReportViewerStatus.Describe(WebReportViewerStatus.Opened, WebReportKind.ApparentlyUnusedReview));
        Assert.Equal("Opening interactive report…", WebReportViewerStatus.Opening(WebReportKind.InteractiveReport));
        Assert.Equal("Opening apparently unused review…", WebReportViewerStatus.Opening(WebReportKind.ApparentlyUnusedReview));
    }

    [Theory]
    [InlineData(WebReportViewerStatus.Blocked)]
    [InlineData(WebReportViewerStatus.TimedOut)]
    [InlineData(WebReportViewerStatus.Rejected)]
    [InlineData(WebReportViewerStatus.Failed)]
    public void NoOtherOutcomeReportsThatTheReportOpened(string status)
    {
        Assert.False(WebReportViewerStatus.IsOpened(status));

        foreach (var report in new[] { WebReportKind.InteractiveReport, WebReportKind.ApparentlyUnusedReview })
        {
            var message = WebReportViewerStatus.Describe(status, report);
            Assert.NotEqual(WebReportViewerStatus.Describe(WebReportViewerStatus.Opened, report), message);
            Assert.DoesNotContain("opened in a new tab", message, StringComparison.OrdinalIgnoreCase);
            // Every failure leaves somewhere else to go, and it is the same report's download.
            Assert.Contains($"{report.DownloadName} to save the {report.Name} instead", message, StringComparison.Ordinal);
        }
    }

    /// <summary>Recovery advice for one report never points at the other one.</summary>
    [Theory]
    [InlineData(WebReportViewerStatus.Blocked)]
    [InlineData(WebReportViewerStatus.TimedOut)]
    [InlineData(WebReportViewerStatus.Rejected)]
    [InlineData(WebReportViewerStatus.Failed)]
    public void RecoveryIsReportSpecific(string status)
    {
        var review = WebReportViewerStatus.Describe(status, WebReportKind.ApparentlyUnusedReview);
        Assert.Contains("apparently unused review", review, StringComparison.Ordinal);
        Assert.DoesNotContain("interactive report", review, StringComparison.Ordinal);
        Assert.DoesNotContain("HTML report", review, StringComparison.Ordinal);

        var interactive = WebReportViewerStatus.Describe(status, WebReportKind.InteractiveReport);
        Assert.Contains("interactive report", interactive, StringComparison.Ordinal);
        Assert.DoesNotContain("apparently unused", interactive, StringComparison.Ordinal);
    }

    [Fact]
    public void ATimeoutIsDistinguishedFromABlockedTab()
    {
        // They need different advice: one is a pop-up setting, the other is a tab that is already open.
        var report = WebReportKind.InteractiveReport;
        Assert.NotEqual(
            WebReportViewerStatus.Describe(WebReportViewerStatus.Blocked, report),
            WebReportViewerStatus.Describe(WebReportViewerStatus.TimedOut, report));
        Assert.Contains("pop-ups", WebReportViewerStatus.Describe(WebReportViewerStatus.Blocked, report),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not confirm", WebReportViewerStatus.Describe(WebReportViewerStatus.TimedOut, report),
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
        Assert.Equal(WebReportViewerStatus.Describe(WebReportViewerStatus.Failed, WebReportKind.InteractiveReport),
            WebReportViewerStatus.Describe(status, WebReportKind.InteractiveReport));
    }

    /// <summary>
    /// A download that has been handed to the browser is all that is known. The browser may still
    /// prompt, block or cancel it, so the wording never claims the file was kept.
    /// </summary>
    [Theory]
    [MemberData(nameof(Reports))]
    public void ADownloadIsReportedAsStartedNotAsRetained(WebReportKind report)
    {
        var started = WebReportViewerStatus.DownloadStarted(report);
        Assert.Equal($"Download started: {report.Name}.", started);
        foreach (var overclaim in new[] { "saved", "downloaded locally", "retained", "kept" })
        {
            Assert.DoesNotContain(overclaim, started, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(report.Name, WebReportViewerStatus.DownloadFailed(report), StringComparison.Ordinal);
        Assert.Contains(report.Name, WebReportViewerStatus.OpenFailed(report), StringComparison.Ordinal);
    }

    [Fact]
    public void HomeAsksForTheStatusAndShowsWhatItDescribesBesideEachReport()
    {
        var markup = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

        Assert.Contains("InvokeAsync<string>(\"pbiAssureDownload.open\"", markup, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Describe(status, WebReportKind.InteractiveReport)", markup, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Describe(status, WebReportKind.ApparentlyUnusedReview)", markup, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Opening(WebReportKind.InteractiveReport)", markup, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Opening(WebReportKind.ApparentlyUnusedReview)", markup, StringComparison.Ordinal);
        // The old unconditional success strings are gone from the page.
        Assert.DoesNotContain("HTML report opened in a new tab.", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("downloaded locally", markup, StringComparison.Ordinal);
        // Feedback lives inside the card of the action that caused it, not below the whole summary.
        Assert.DoesNotContain("<p class=\"message\" role=\"status\">@outputMessage</p>", markup, StringComparison.Ordinal);
        Assert.Contains("<p class=\"report-card-status\" role=\"status\">@interactiveReportMessage</p>", markup, StringComparison.Ordinal);
        Assert.Contains("<p class=\"report-card-status\" role=\"status\">@unusedReviewMessage</p>", markup, StringComparison.Ordinal);
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
