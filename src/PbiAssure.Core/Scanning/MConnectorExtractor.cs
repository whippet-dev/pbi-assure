using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static partial class MConnectorExtractor
{
    private const string EnteredDataFamily = "Entered data";

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
            // Web tables by example: Desktop emits this beside Web.Contents for the same connector.
            ["Web.BrowserContents"] = "Web",
            ["OData.Feed"] = "OData",
            ["SharePoint.Files"] = "SharePoint",
            ["SharePoint.Contents"] = "SharePoint",
            // The SharePoint Online list connector.
            ["SharePoint.Tables"] = "SharePoint",
            ["Sql.Database"] = "SQL Server",
            // Desktop emits the server-level form whenever the database box is left blank.
            ["Sql.Databases"] = "SQL Server",
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
            ["PowerBI.Dataflows"] = "Power BI dataflow",
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
            .Concat(EmbeddedTables(searchable))
            .ToArray();
    }

    /// <summary>
    /// Data that lives in the model definition itself rather than anywhere a connector reaches.
    ///
    /// Enter data is serialised by Desktop as one fixed shape — the rows as a compressed, base64
    /// payload unpacked by <c>Table.FromRows(Json.Document(Binary.Decompress(Binary.FromText(…))))</c>
    /// — and it is the whole chain that identifies it: each of those functions is ordinary on its own
    /// (<c>Json.Document</c> wraps web and file sources all the time). A <c>#table</c> literal is the
    /// hand-written equivalent. Neither is a file, so neither can be a file-location finding.
    /// </summary>
    private static IEnumerable<ConnectorMatch> EmbeddedTables(string searchable)
    {
        if (EnteredDataRegex().IsMatch(searchable))
        {
            yield return new ConnectorMatch(EnteredDataFamily, "Table.FromRows", DataSourceLocationKinds.EmbeddedInModel);
        }

        if (TableLiteralRegex().IsMatch(searchable))
        {
            yield return new ConnectorMatch(EnteredDataFamily, "#table", DataSourceLocationKinds.EmbeddedInModel);
        }
    }

    private static string ClassifyLocation(string expression, string function)
    {
        // Dataflows live in the service. Desktop writes the call as Dataflows(null) and navigates by
        // workspace and dataflow afterwards, so there is no address to inspect and none to report.
        if (function is "PowerBI.Dataflows" or "PowerPlatform.Dataflows")
        {
            return DataSourceLocationKinds.OnlineService;
        }

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

        if (function is "Web.Contents" or "Web.BrowserContents" or "OData.Feed"
            or "SharePoint.Files" or "SharePoint.Contents" or "SharePoint.Tables")
        {
            return DataSourceLocationKinds.WebAddress;
        }

        return function.EndsWith(".Database", StringComparison.OrdinalIgnoreCase) ||
               function is "Sql.Databases" or "Odbc.DataSource" or "Odbc.Query" or "OleDb.DataSource"
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

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])Table\.FromRows\s*\(\s*Json\.Document\s*\(\s*Binary\.Decompress\s*\(\s*Binary\.FromText\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnteredDataRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9_#])#table\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex TableLiteralRegex();

    internal sealed record ConnectorMatch(string Family, string Function, string LocationKind);
}
