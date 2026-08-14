using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace PbiAssure.Privacy.E2E;

internal sealed record CapturedNetworkEvent(
    string Phase,
    string Kind,
    string Method,
    string Url,
    string ResourceType,
    string ObservableContent);

internal sealed class PrivacyNetworkMonitor
{
    private readonly Uri applicationOrigin;
    private readonly ConcurrentQueue<CapturedNetworkEvent> events = new();
    private readonly HashSet<IPage> attachedPages = [];
    private readonly object sync = new();
    private string phase = "Startup";
    private bool monitoring;

    public PrivacyNetworkMonitor(IBrowserContext context, string baseUrl)
    {
        applicationOrigin = new Uri(baseUrl);
        context.Request += (_, request) => CaptureRequest(request);
        context.Page += (_, page) => Attach(page);
        foreach (var page in context.Pages)
        {
            Attach(page);
        }
    }

    public IReadOnlyList<CapturedNetworkEvent> Events => events.ToArray();

    public void Begin(string newPhase)
    {
        phase = newPhase;
        monitoring = true;
    }

    public IReadOnlyList<CapturedNetworkEvent> ExternalEvents() => Events
        .Where(item => IsExternalNetworkUrl(item.Url))
        .ToArray();

    public IReadOnlyList<CapturedNetworkEvent> CanaryLeaks() => Events
        .Where(item => PrivacyCanaries.All.Any(canary =>
            item.ObservableContent.Contains(canary, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    public IReadOnlyList<CapturedNetworkEvent> UnexpectedEvents() => Events
        .Where(item => IsUnexpected(item))
        .ToArray();

    private void Attach(IPage page)
    {
        lock (sync)
        {
            if (!attachedPages.Add(page))
            {
                return;
            }
        }

        page.WebSocket += (_, socket) => CaptureWebSocket(socket);
    }

    private void CaptureRequest(IRequest request)
    {
        if (!monitoring
            || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        var headers = string.Join(
            "\n",
            request.Headers.Select(header => $"{header.Key}: {header.Value}"));
        events.Enqueue(new CapturedNetworkEvent(
            phase,
            "Request",
            request.Method,
            request.Url,
            request.ResourceType,
            $"{request.Url}\n{headers}\n{request.PostData}"));
    }

    private void CaptureWebSocket(IWebSocket socket)
    {
        if (!monitoring)
        {
            return;
        }

        events.Enqueue(new CapturedNetworkEvent(
            phase,
            "WebSocket",
            "CONNECT",
            socket.Url,
            "websocket",
            socket.Url));
    }

    private bool IsUnexpected(CapturedNetworkEvent item)
    {
        if (item.Kind == "WebSocket" || IsExternalNetworkUrl(item.Url))
        {
            return true;
        }

        if (item.Phase is "Scan" or "Exports")
        {
            return IsNetworkUrl(item.Url);
        }

        if (item.Phase == "Viewer" && IsNetworkUrl(item.Url))
        {
            var uri = new Uri(item.Url);
            return !string.Equals(item.Method, "GET", StringComparison.OrdinalIgnoreCase) ||
                uri.AbsolutePath is not "/report-viewer.html" and not "/report-viewer" and not "/report-viewer.js";
        }

        return false;
    }

    private bool IsExternalNetworkUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsNetworkScheme(uri.Scheme))
        {
            return false;
        }

        return !string.Equals(uri.Scheme, applicationOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, applicationOrigin.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != applicationOrigin.Port;
    }

    private static bool IsNetworkUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsNetworkScheme(uri.Scheme);

    private static bool IsNetworkScheme(string scheme) =>
        scheme is "http" or "https" or "ws" or "wss";
}
