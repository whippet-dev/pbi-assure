using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace PbiAssure.Privacy.E2E;

internal sealed class PrivacyTestHost : IAsyncDisposable
{
    private readonly WebApplication application;

    private PrivacyTestHost(WebApplication application, string baseUrl)
    {
        this.application = application;
        BaseUrl = baseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }

    public static async Task<PrivacyTestHost> StartAsync(string publishedRoot)
    {
        var headerRules = CloudflareHeaderRule.Parse(Path.Combine(publishedRoot, "_headers"));
        var fileProvider = new PhysicalFileProvider(publishedRoot);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var originalPath = context.Request.Path.Value ?? "/";
            foreach (var rule in headerRules.Where(rule => rule.Matches(originalPath)))
            {
                rule.Apply(context.Response.Headers);
            }

            if (string.Equals(originalPath, "/report-viewer.html", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
                context.Response.Headers.Location = "/report-viewer" + context.Request.QueryString;
                return;
            }

            if (string.Equals(originalPath, "/report-viewer", StringComparison.Ordinal))
            {
                context.Request.Path = "/report-viewer.html";
            }

            await next();
        });
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
        await app.StartAsync();

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("The privacy test server did not expose a listening address.");
        return new PrivacyTestHost(app, address);
    }

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync();
        await application.DisposeAsync();
    }

    private sealed record HeaderOperation(string Name, string? Value);

    private sealed record CloudflareHeaderRule(string Path, IReadOnlyList<HeaderOperation> Operations)
    {
        public bool Matches(string requestPath) =>
            Path == "/*" || string.Equals(Path, requestPath, StringComparison.Ordinal);

        public void Apply(IHeaderDictionary headers)
        {
            foreach (var operation in Operations)
            {
                if (operation.Value is null)
                {
                    headers.Remove(operation.Name);
                }
                else
                {
                    headers[operation.Name] = operation.Value;
                }
            }
        }

        public static List<CloudflareHeaderRule> Parse(string path)
        {
            var rules = new List<CloudflareHeaderRule>();
            string? currentPath = null;
            List<HeaderOperation>? operations = null;
            foreach (var rawLine in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                if (!char.IsWhiteSpace(rawLine[0]))
                {
                    if (currentPath is not null && operations is not null)
                    {
                        rules.Add(new CloudflareHeaderRule(currentPath, operations));
                    }

                    currentPath = rawLine.Trim();
                    operations = [];
                    continue;
                }

                var line = rawLine.Trim();
                if (line.StartsWith("! ", StringComparison.Ordinal))
                {
                    operations?.Add(new HeaderOperation(line[2..].Trim(), null));
                    continue;
                }

                var separator = line.IndexOf(':');
                if (separator > 0)
                {
                    operations?.Add(new HeaderOperation(line[..separator].Trim(), line[(separator + 1)..].Trim()));
                }
            }

            if (currentPath is not null && operations is not null)
            {
                rules.Add(new CloudflareHeaderRule(currentPath, operations));
            }

            return rules;
        }
    }
}
