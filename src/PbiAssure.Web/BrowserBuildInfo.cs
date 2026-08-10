using System.Reflection;

namespace PbiAssure.Web;

public static class BrowserBuildInfo
{
    private static readonly Assembly Assembly = typeof(BrowserBuildInfo).Assembly;

    public static string Version => Assembly.GetName().Version?.ToString(3) ?? "development";

    public static string? Commit
    {
        get
        {
            var informationalVersion = Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var separator = informationalVersion?.LastIndexOf('+') ?? -1;
            if (separator < 0 || separator == informationalVersion!.Length - 1)
            {
                return null;
            }

            var revision = informationalVersion[(separator + 1)..];
            return revision.Length > 12 ? revision[..12] : revision;
        }
    }
}
