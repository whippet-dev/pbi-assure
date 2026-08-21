using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Pins the Power BI Desktop-authored inactive-relationship experiment. These tests preserve the
/// evidence needed for a future structured USERELATIONSHIP parser; they do not infer relationship
/// activation from today's flat DAX reference stream.
/// </summary>
public sealed class DesktopUserRelationshipEvidenceFixtureTests
{
    [Fact]
    public void DesktopRoundTripRetainsActiveAndInactiveRelationshipControls()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);

        Assert.Equal(4, model.Relationships.Count);
        AssertRelationship(model, "BillingCustomerID", isActive: true);
        AssertRelationship(model, "ShippingCustomerID", isActive: false);
        AssertRelationship(model, "ReferralCustomerID", isActive: false);
        AssertRelationship(model, "LegacyCustomerID", isActive: false);
    }

    [Fact]
    public void DesktopRoundTripRetainsUsedAndUnusedUserRelationshipMeasures()
    {
        var inventory = ScanFixture();
        var model = Assert.Single(inventory.SemanticModels);
        var sales = Assert.Single(model.Tables, table => table.Name == "Sales");
        var shipping = Assert.Single(sales.Measures, measure => measure.Name == "Sales by Shipping Customer");
        var referral = Assert.Single(sales.Measures, measure => measure.Name == "Sales by Referral Customer");

        Assert.Contains("USERELATIONSHIP", shipping.Expression, StringComparison.Ordinal);
        Assert.Contains("Customers[CustomerID]", shipping.Expression, StringComparison.Ordinal);
        Assert.Contains("Sales[ShippingCustomerID]", shipping.Expression, StringComparison.Ordinal);
        Assert.Contains("USERELATIONSHIP", referral.Expression, StringComparison.Ordinal);
        Assert.Contains("Sales[ReferralCustomerID]", referral.Expression, StringComparison.Ordinal);

        Assert.True(Reachability(inventory, shipping.Name).ReachableFromReport);
        Assert.False(Reachability(inventory, referral.Name).ReachableFromReport);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Sales by Shipping Customer").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Sales by Referral Customer").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "ControlUnused").UsageState);
    }

    private static void AssertRelationship(SemanticModelInventory model, string fromColumn, bool isActive)
    {
        var relationship = Assert.Single(model.Relationships, item => item.FromColumn == fromColumn);
        Assert.Equal(isActive, relationship.IsActive);
        Assert.Equal("Sales", relationship.FromTable);
        Assert.Equal("Customers", relationship.ToTable);
        Assert.Equal("CustomerID", relationship.ToColumn);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, item => item.Table == "Sales" && item.ObjectName == objectName);

    private static SemanticNodeReachability Reachability(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticNodeReachability, item =>
            item.Table == "Sales" && item.ObjectName == objectName && item.ObjectType == SemanticObjectTypes.Measure);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(FixturePath());

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "fixtures",
        "desktop-userelationship-evidence");

    private static string FindRepositoryRoot()
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
