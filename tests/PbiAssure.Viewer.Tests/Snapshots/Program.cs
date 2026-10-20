using System.Text.Json;
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
