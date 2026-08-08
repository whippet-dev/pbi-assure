using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class ReportModelBinder
{
    public static SemanticModelInventory? FindLocalModel(
        ReportInventory report,
        IReadOnlyList<SemanticModelInventory> semanticModels)
    {
        var connection = report.ModelConnection;
        if (connection.ConnectionKind == ReportModelConnectionKinds.ByPath)
        {
            if (!connection.IsTargetAvailableLocally || connection.TargetSemanticModelPath is null)
            {
                return null;
            }

            return semanticModels.FirstOrDefault(model => PathsEqual(
                model.RelativePath, connection.TargetSemanticModelPath));
        }

        if (connection.ConnectionKind == ReportModelConnectionKinds.ByConnection)
        {
            return null;
        }

        return semanticModels.FirstOrDefault(model =>
            string.Equals(model.Name, report.Name, StringComparison.OrdinalIgnoreCase));
    }

    public static ReportInventory[] FindReports(
        SemanticModelInventory model,
        IReadOnlyList<ReportInventory> reports,
        IReadOnlyList<SemanticModelInventory> semanticModels)
    {
        return reports.Where(report => ReferenceEquals(FindLocalModel(report, semanticModels), model)).ToArray();
    }

    private static bool PathsEqual(string left, string right)
    {
        var normalizedLeft = left.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        var normalizedRight = right.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}
