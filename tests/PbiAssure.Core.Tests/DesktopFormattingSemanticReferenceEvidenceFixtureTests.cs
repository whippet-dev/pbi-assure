using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DesktopFormattingSemanticReferenceEvidenceFixtureTests
{
    [Fact]
    public void SanitisedDesktopFixturePinsExistingFormattingSemanticCoverage()
    {
        var inventory = ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures",
            "desktop-formatting-semantic-reference-sanitized"));
        var usages = inventory.SemanticObjectUsages.Where(usage => usage.Table == "EvidenceData").ToArray();
        var formattingOnly = new[] { "Dynamic Title Only", "Dynamic Subtitle Only", "Conditional Colour Only", "Background Colour Only", "Reference Line Only", "Error Bar Upper Only", "Error Bar Lower Only", "Conditional Icon Only" };

        Assert.All(formattingOnly, name => Assert.Equal(SemanticUsageStates.DirectlyUsed, Assert.Single(usages, usage => usage.ObjectName == name).UsageState));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Assert.Single(usages, usage => usage.ObjectName == "Unused Measure Control").UsageState);

        var references = Assert.Single(Assert.Single(inventory.Reports).Pages).Visuals.SelectMany(visual => visual.FieldReferences).ToArray();
        Assert.Equal("conditionalFormatting", Assert.Single(references, reference => reference.ObjectName == "Conditional Icon Only").Role);
        Assert.All(references.Where(reference => reference.ObjectName != "Conditional Icon Only"), reference => Assert.Equal(UsageContexts.Formatting, reference.UsageContext));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
