using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Desktop-authored evidence that persisted bookmark state is not, by itself, safe semantic usage
/// evidence. The paired fixtures retain the controlled stale and live-carrier states separately.
/// </summary>
public sealed class DesktopBookmarkEvidenceFixtureTests
{
    [Fact]
    public void StaleCarrierFixtureRetainsRemovedFieldReferencesWithoutLiveUsage()
    {
        var inventory = Scan("desktop-bookmark-evidence-stale");
        var report = Assert.Single(inventory.Reports);

        Assert.Equal(3, report.BookmarkCount);
        AssertAllExactBookmarkSchemas(report);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Name").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Region").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "SecretCategory").UsageState);

        var bookmarksDirectory = Path.Combine(FixtureRoot("desktop-bookmark-evidence-stale"),
            "desktop-bookmark-evidence.Report", "definition", "bookmarks");
        Assert.Contains("\"Property\": \"Region\"", File.ReadAllText(Path.Combine(bookmarksDirectory, "75ddaea0b52276a51b07.bookmark.json")), StringComparison.Ordinal);
        Assert.Contains("\"Property\": \"SecretCategory\"", File.ReadAllText(Path.Combine(bookmarksDirectory, "1bc9edf2332f8153e7ad.bookmark.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void LiveCarrierFixtureShowsOnlyCurrentReportUsage()
    {
        var inventory = Scan("desktop-bookmark-evidence-live-carrier");
        var report = Assert.Single(inventory.Reports);

        Assert.Equal(4, report.BookmarkCount);
        AssertAllExactBookmarkSchemas(report);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Name").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Region").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "SecretCategory").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "ID").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "ControlUnused").UsageState);
    }

    private static void AssertAllExactBookmarkSchemas(ReportInventory report)
    {
        Assert.All(report.SchemaObservations.Where(item => item.ArtifactKind == ReportSchemaArtifactKinds.BookmarksMetadata), item =>
        {
            Assert.Equal("bookmarksMetadata", item.SchemaFamily);
            Assert.Equal("1.0.0", item.SchemaVersion);
            Assert.Equal(ReportSchemaObservationStates.VerifiedExact, item.State);
        });
        Assert.All(report.SchemaObservations.Where(item => item.ArtifactKind == ReportSchemaArtifactKinds.Bookmark), item =>
        {
            Assert.Equal("bookmark", item.SchemaFamily);
            Assert.Equal("2.1.0", item.SchemaVersion);
            Assert.Equal(ReportSchemaObservationStates.VerifiedExact, item.State);
        });
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, item => item.Table == "People" && item.ObjectName == objectName);

    private static ProjectInventory Scan(string fixture) => ProjectScanner.Scan(FixtureRoot(fixture));

    private static string FixtureRoot(string fixture) => Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
