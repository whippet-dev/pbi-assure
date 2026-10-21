using System.Text.Json;
using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting.Viewer;

var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx")))
    root = root.Parent;
if (root is null) throw new DirectoryNotFoundException("Run inside the repository.");
var output = Path.Combine(root.FullName, "artifacts", "viewer-test-snapshots");
Directory.CreateDirectory(output);
foreach (var name in new[] { "pbi-assure-coverage", "desktop-userelationship-evidence" })
{
    var inventory = ProjectScanner.Scan(Path.Combine(root.FullName, "tests", "fixtures", name));
    var snapshot = ViewerSnapshotProjector.Project(inventory);
    File.WriteAllText(Path.Combine(output, name + ".json"),
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{name}: {snapshot.Objects.Count} object results");
}

// Two independently owned, identically named report measures and visual/page labels.
// Scan saved definitions, so the identity and binding assertions exercise Core's real projector.
var files = new List<ProjectFileContent>
{
    Content("Model.SemanticModel/definition.pbism", "{}"),
    Content("Model.SemanticModel/definition/tables/Sales.tmdl",
        "table Sales\n" + Column("Amount") + Column("Qty"))
};
foreach (var (report, column) in new[] { ("A", "Amount"), ("B", "Qty") })
{
    files.Add(Content($"{report}.Report/definition.pbir",
        """{"datasetReference":{"byPath":{"path":"../Model.SemanticModel"}}}"""));
    files.Add(Content($"{report}.Report/definition/reportExtensions.json", """
        {"$schema":"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json",
        "name":"extension","entities":[{"name":"Sales","measures":[{"name":"Local","dataType":"Decimal",
        "expression":"SUM(Sales[COLUMN])","references":{"unrecognizedReferences":false,"measures":[]}}]}]}
        """.Replace("COLUMN", column, StringComparison.Ordinal)));
    files.Add(Content($"{report}.Report/definition/pages/pages.json",
        """{"pageOrder":["p1"],"activePageName":"p1"}"""));
    files.Add(Content($"{report}.Report/definition/pages/p1/page.json",
        """{"name":"p1","displayName":"Page 1"}"""));
    files.Add(Content($"{report}.Report/definition/pages/p1/visuals/v1/visual.json",
        """
        {"name":"v1","visual":{"visualType":"card","query":{"queryState":{"Values":{"projections":[
        {"field":{"Measure":{"Expression":{"SourceRef":{"Entity":"Sales"}},"Property":"Local"}},"queryRef":"Sales.Local"}]}}}}}
        """));
}
var ownedMeasures = ViewerSnapshotProjector.Project(
    ProjectScanner.Scan(new InMemoryProjectFileSource("Two reports", files)));
FileWrite("two-report-measures", ownedMeasures);

void FileWrite(string name, ViewerSnapshot snapshot)
{
    System.IO.File.WriteAllText(Path.Combine(output, name + ".json"),
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{name}: {snapshot.Objects.Count} object results");
}
static string Column(string name) =>
    $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";
static ProjectFileContent Content(string path, string content) =>
    new(path, Encoding.UTF8.GetBytes(content));
