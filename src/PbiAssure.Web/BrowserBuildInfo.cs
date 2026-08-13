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
            return DisplayRevision(informationalVersion);
        }
    }

    public static string? DisplayRevision(string? informationalVersion)
    {
        var separator = informationalVersion?.LastIndexOf('+') ?? -1;
        if (separator < 0 || separator == informationalVersion!.Length - 1)
        {
            return null;
        }

        var revision = informationalVersion[(separator + 1)..];
        var isDirty = revision.EndsWith("-dirty", StringComparison.Ordinal);
        var commit = isDirty ? revision[..^"-dirty".Length] : revision;
        var displayCommit = commit.Length > 12 ? commit[..12] : commit;
        return isDirty ? $"{displayCommit}-dirty" : displayCommit;
    }
}
