namespace PbiAssure.Core.Scanning;

/// <summary>
/// Applies supported-input boundaries before any parser, assurance rule or output writer can run.
/// </summary>
internal static class ProjectInputEligibility
{
    public static void EnsureSupported(IProjectFileSource source)
    {
        foreach (var directory in source.EnumerateDirectories(string.Empty)
                     .Where(directory => directory.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase)))
        {
            var hasTmsl = source.FileExists(ProjectFilePaths.Combine(directory, "model.bim"));
            if (!hasTmsl)
            {
                continue;
            }

            var hasTmdlDefinition = source.EnumerateFiles(ProjectFilePaths.Combine(directory, "definition"))
                .Any();
            if (hasTmdlDefinition)
            {
                throw UnsupportedProjectInputException.AmbiguousSemanticModelFormat(directory);
            }

            throw UnsupportedProjectInputException.TmslSemanticModel(directory);
        }
    }
}
