using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static partial class MConnectorExtractor
{
    private static readonly Dictionary<string, string> ConnectorFamilies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["File.Contents"] = "File",
            ["Folder.Files"] = "Folder",
            ["Folder.Contents"] = "Folder",
            ["Excel.Workbook"] = "Excel",
            ["Csv.Document"] = "Text or CSV",
            ["Pdf.Tables"] = "PDF",
            ["Web.Contents"] = "Web",
            ["OData.Feed"] = "OData",
            ["SharePoint.Files"] = "SharePoint",
            ["SharePoint.Contents"] = "SharePoint",
            ["Sql.Database"] = "SQL Server",
            ["Odbc.DataSource"] = "ODBC",
            ["Odbc.Query"] = "ODBC",
            ["OleDb.DataSource"] = "OLE DB",
            ["Oracle.Database"] = "Oracle",
            ["PostgreSQL.Database"] = "PostgreSQL",
            ["MySQL.Database"] = "MySQL",
            ["Snowflake.Databases"] = "Snowflake",
            ["GoogleBigQuery.Database"] = "Google BigQuery",
            ["AmazonRedshift.Database"] = "Amazon Redshift",
            ["SapHana.Database"] = "SAP HANA",
            ["AnalysisServices.Database"] = "Analysis Services",
            ["CommonDataService.Database"] = "Dataverse",
            ["PowerPlatform.Dataflows"] = "Power Platform dataflow",
            ["AzureStorage.Blobs"] = "Azure Blob Storage",
            ["AzureStorage.DataLake"] = "Azure Data Lake Storage",
            ["Lakehouse.Contents"] = "Fabric Lakehouse",
            ["Warehouse.Contents"] = "Fabric Warehouse",
            ["Spark.Tables"] = "Spark",
        };

    public static ConnectorMatch[] Extract(string expression)
    {
        var searchable = MReferenceExtractor.RemoveStringsAndComments(expression);
        return ConnectorCallRegex().Matches(searchable)
            .Select(match => match.Groups[1].Value)
            .Where(ConnectorFamilies.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(function => new ConnectorMatch(
                ConnectorFamilies[function], function, ClassifyLocation(expression, function)))
            .ToArray();
    }

    private static string ClassifyLocation(string expression, string function)
    {
        var argument = ReadFirstLiteralArgument(expression, function);
        if (argument is null)
        {
            return DataSourceLocationKinds.DynamicOrUnspecified;
        }

        if (function is "File.Contents" or "Folder.Files" or "Folder.Contents")
        {
            if (argument.StartsWith("\\\\", StringComparison.Ordinal))
            {
                return DataSourceLocationKinds.NetworkFile;
            }

            return IsWindowsFullyQualifiedPath(argument)
                ? DataSourceLocationKinds.LocalFile
                : DataSourceLocationKinds.RelativeFile;
        }

        if (function is "Web.Contents" or "OData.Feed" or "SharePoint.Files" or "SharePoint.Contents")
        {
            return DataSourceLocationKinds.WebAddress;
        }

        return function.EndsWith(".Database", StringComparison.OrdinalIgnoreCase) ||
               function is "Odbc.DataSource" or "Odbc.Query" or "OleDb.DataSource"
            ? DataSourceLocationKinds.NamedServer
            : DataSourceLocationKinds.DynamicOrUnspecified;
    }

    private static bool IsWindowsFullyQualifiedPath(string path) =>
        path.StartsWith("\\\\", StringComparison.Ordinal) ||
        path.Length >= 3 &&
        char.IsAsciiLetter(path[0]) &&
        path[1] == ':' &&
        (path[2] == '\\' || path[2] == '/');

    private static string? ReadFirstLiteralArgument(string expression, string function)
    {
        var match = Regex.Match(
            expression,
            $"{Regex.Escape(function)}\\s*\\(\\s*\"((?:[^\"]|\"\")*)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Replace("\"\"", "\"") : null;
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_])([A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectorCallRegex();

    internal sealed record ConnectorMatch(string Family, string Function, string LocationKind);
}
