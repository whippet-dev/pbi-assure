using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class ProjectFileSourceTests
{
    [Fact]
    public void NormalizesCanonicalProjectRelativePaths()
    {
        Assert.Equal("Sales.Report/definition/pages/page.json",
            ProjectFilePaths.Normalize("Sales.Report\\definition/./pages\\page.json"));
        Assert.Equal("Sales.Report/definition.pbir",
            ProjectFilePaths.ResolveRelative("Sales.Report", "./definition.pbir"));
        Assert.Equal("Sales.SemanticModel",
            ProjectFilePaths.ResolveRelative("Sales.Report", "../Sales.SemanticModel"));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("C:\\outside.json")]
    [InlineData("\\\\server\\share\\outside.json")]
    public void RejectsPathsThatEscapeOrAreRooted(string path)
    {
        Assert.Throws<ArgumentException>(() => ProjectFilePaths.Normalize(path));
    }

    [Fact]
    public void InMemorySourceProvidesCaseInsensitiveCanonicalFileAccess()
    {
        var source = new InMemoryProjectFileSource("Sample", [
            Content("Sales.Report/definition/pages/page-a/page.json", "{}"),
            Content("Sales.Report/definition/pages/page-a/visuals/one/visual.json", "{}"),
        ]);

        Assert.True(source.FileExists("sales.report\\definition\\pages\\page-a\\page.json"));
        Assert.Equal(["page-a"], source.EnumerateDirectories("Sales.Report/definition/pages").ToArray());
        Assert.Equal(2, source.EnumerateFiles("Sales.Report/definition/pages").Count());
        using var reader = new StreamReader(source.OpenRead("Sales.Report/definition/pages/page-a/page.json"));
        Assert.Equal("{}", reader.ReadToEnd());
    }

    [Fact]
    public void ScanInMemoryProjectMatchesPhysicalScan()
    {
        var root = Path.Combine(Path.GetTempPath(), "PbiAssure.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Write(root, "Sales.pbip", "{}");
            Write(root, "Sales.Report/definition.pbir", """
                { "datasetReference": { "byPath": { "path": "../Sales.SemanticModel" } } }
                """);
            Write(root, "Sales.Report/definition/pages/pages.json", "{ \"pageOrder\": [\"page-a\"] }");
            Write(root, "Sales.Report/definition/pages/page-a/page.json", "{ \"name\": \"page-a\", \"displayName\": \"Overview\" }");
            Write(root, "Sales.Report/definition/pages/page-a/visuals/one/visual.json", "{ \"name\": \"one\" }");
            Write(root, "Sales.Report/definition/pages/page-a/visuals/group/visual.json", "{ \"name\": \"group\", \"visualGroup\": { \"displayName\": \"Group\" }, \"position\": { \"tabOrder\": 0 } }");
            Write(root, "Sales.Report/definition/bookmarks/bookmarks.json", "{ \"items\": [{ \"name\": \"bookmark-a\" }] }");
            Write(root, "Sales.Report/definition/bookmarks/bookmark-a.bookmark.json", "{ \"name\": \"bookmark-a\", \"displayName\": \"Bookmark A\" }");
            Write(root, "Sales.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n    column Amount\n        dataType: int64");

            var physicalSource = new PhysicalProjectFileSource(root);
            Assert.Equal(Path.GetFullPath(root), physicalSource.SourceRoot);
            Assert.All(physicalSource.Files, file => Assert.DoesNotContain('\\', file.RelativePath));
            var physical = ProjectScanner.Scan(root);
            var memory = new InMemoryProjectFileSource("Sales", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => new ProjectFileContent(
                    Path.GetRelativePath(root, path).Replace('\\', '/'),
                    File.ReadAllBytes(path))));
            var inMemory = ProjectScanner.Scan(memory);

            Assert.Equal(physical.Artifacts.Count, inMemory.Artifacts.Count);
            Assert.Equal(physical.ReportCount, inMemory.ReportCount);
            Assert.Equal(physical.PageCount, inMemory.PageCount);
            Assert.Equal(physical.VisualCount, inMemory.VisualCount);
            Assert.Equal(1, Assert.Single(Assert.Single(physical.Reports).Pages).VisualGroupCount);
            Assert.Equal(1, Assert.Single(Assert.Single(inMemory.Reports).Pages).VisualGroupCount);
            Assert.Equal(physical.BookmarkCount, inMemory.BookmarkCount);
            Assert.Equal(physical.SemanticModelCount, inMemory.SemanticModelCount);
            Assert.Equal(physical.SemanticModels.Sum(model => model.Tables.Count), inMemory.SemanticModels.Sum(model => model.Tables.Count));
            Assert.Equal(physical.Reports.Select(report => report.RelativePath), inMemory.Reports.Select(report => report.RelativePath));
            Assert.Equal(physical.SemanticModels.Select(model => model.RelativePath), inMemory.SemanticModels.Select(model => model.RelativePath));
            Assert.Equal("Sales", inMemory.RootPath);
            Assert.True(Assert.Single(inMemory.Reports).ModelConnection.IsTargetAvailableLocally);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PhysicalSourceIndexesOnlyProjectMetadataTrees()
    {
        var root = Path.Combine(Path.GetTempPath(), "PbiAssure.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Write(root, "Sales.pbip", "{}");
            Write(root, "Sales.Report/definition/pages/pages.json", "{}");
            Write(root, "Sales.SemanticModel/definition/tables/Sales.tmdl", "table Sales");
            Write(root, "outputs/latest.pbiassure.html", "generated");
            Write(root, "unrelated/data.json", "{}");

            var source = new PhysicalProjectFileSource(root);

            Assert.Contains(source.Files, file => file.RelativePath == "Sales.pbip");
            Assert.Contains(source.Files, file => file.RelativePath == "Sales.Report/definition/pages/pages.json");
            Assert.Contains(source.Files, file => file.RelativePath == "Sales.SemanticModel/definition/tables/Sales.tmdl");
            Assert.DoesNotContain(source.Files, file => file.RelativePath.StartsWith("outputs/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(source.Files, file => file.RelativePath.StartsWith("unrelated/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("C:\\data\\sales.xlsx", DataSourceLocationKinds.LocalFile)]
    [InlineData("\\\\server\\share\\sales.xlsx", DataSourceLocationKinds.NetworkFile)]
    public void ScanRecognizesWindowsMFileLocationsIndependentOfHost(string path, string expectedLocationKind)
    {
        var source = new InMemoryProjectFileSource("Sales", [
            Content("Sales.SemanticModel/definition/tables/Sales.tmdl", $$"""
                table Sales
                    partition Sales = m
                        mode: import
                        source =
                                let Source = File.Contents("{{path.Replace("\\", "\\\\")}}") in Source
                """),
        ]);

        var inventory = ProjectScanner.Scan(source);

        Assert.Contains(inventory.DataSources, item => item.LocationKind == expectedLocationKind);
    }

    private static ProjectFileContent Content(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static void Write(string root, string relativePath, string text)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
