using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using System.Text.Json;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Pins the Power BI Desktop-authored inactive-relationship experiment. These tests preserve the
/// evidence needed for the bounded USERELATIONSHIP extractor and exact relationship resolver.
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

    [Fact]
    public void ScanDistinguishesProvenInactiveRelationshipActivationStatesWithoutChangingUsage()
    {
        var inventory = ScanFixture();
        var model = Assert.Single(inventory.SemanticModels);

        AssertActivation(model, "ShippingCustomerID", SemanticRelationshipActivationStates.ActivatedByReportUsedDax, "Sales by Shipping Customer", reportReachable: true);
        AssertActivation(model, "ReferralCustomerID", SemanticRelationshipActivationStates.ReferencedOnlyByUnusedDax, "Sales by Referral Customer", reportReachable: false);
        Assert.Equal(SemanticRelationshipActivationStates.NoDetectedActivation,
            AssertRelationship(model, "LegacyCustomerID", isActive: false).Activation!.State);
        Assert.Null(AssertRelationship(model, "BillingCustomerID", isActive: true).Activation);

        AssertUsage(inventory, "Customers", "CustomerName", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Sales by Shipping Customer", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Total Sales", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Amount", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "ShippingCustomerID", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Customers", "CustomerID", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "BillingCustomerID", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Sales", "ReferralCustomerID", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Sales", "LegacyCustomerID", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Sales", "Sales by Referral Customer", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Sales", "SaleID", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Sales", "ControlUnused", SemanticUsageStates.ApparentlyUnused);
    }

    [Fact]
    public void HtmlShowsInactiveRelationshipReviewContextWithoutAddingFindings()
    {
        var inventory = ScanFixture();
        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("Activated by report-used DAX", html, StringComparison.Ordinal);
        Assert.Contains("Activated by</dt><dd>Sales[Sales by Shipping Customer]", html, StringComparison.Ordinal);
        Assert.Contains("Referenced only by unused DAX", html, StringComparison.Ordinal);
        Assert.Contains("Referenced only by unused DAX</dt><dd>Sales[Sales by Referral Customer]", html, StringComparison.Ordinal);
        Assert.Contains("No USERELATIONSHIP call found in analysed DAX", html, StringComparison.Ordinal);
        Assert.Contains("No <code>USERELATIONSHIP</code> call found in the analysed DAX.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Activated by report-used DAX</dt><dd>Sales[Total Sales]", html, StringComparison.Ordinal);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId.Contains("RELATIONSHIP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JsonAddsStructuredRelationshipActivationEvidenceWithoutChangingCsvScope()
    {
        var inventory = ScanFixture();
        var json = JsonSerializer.Serialize(inventory);

        Assert.Equal("0.25", inventory.SchemaVersion);
        Assert.Contains("\"Activation\":{\"State\":\"ActivatedByReportUsedDax\"", json, StringComparison.Ordinal);
        Assert.Contains("\"State\":\"ReferencedOnlyByUnusedDax\"", json, StringComparison.Ordinal);
        Assert.Contains("\"State\":\"NoDetectedActivation\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RelationshipActivation", SemanticUsageCsvRenderer.Render(inventory), StringComparison.Ordinal);
    }

    private static SemanticRelationshipInventory AssertRelationship(SemanticModelInventory model, string fromColumn, bool isActive)
    {
        var relationship = Assert.Single(model.Relationships, item => item.FromColumn == fromColumn);
        Assert.Equal(isActive, relationship.IsActive);
        Assert.Equal("Sales", relationship.FromTable);
        Assert.Equal("Customers", relationship.ToTable);
        Assert.Equal("CustomerID", relationship.ToColumn);
        return relationship;
    }

    private static void AssertActivation(
        SemanticModelInventory model,
        string column,
        string state,
        string sourceMeasure,
        bool reportReachable)
    {
        var activation = AssertRelationship(model, column, isActive: false).Activation;
        Assert.NotNull(activation);
        Assert.Equal(state, activation.State);
        var source = Assert.Single(activation.Sources);
        Assert.Equal("Sales", source.Table);
        Assert.Equal(sourceMeasure, source.ObjectName);
        Assert.Equal(SemanticObjectTypes.Measure, source.ObjectType);
        Assert.Equal(reportReachable, source.ReachableFromReport);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, item => item.Table == "Sales" && item.ObjectName == objectName);

    private static void AssertUsage(ProjectInventory inventory, string table, string objectName, string state) =>
        Assert.Equal(state, Assert.Single(inventory.SemanticObjectUsages,
            item => item.Table == table && item.ObjectName == objectName).UsageState);

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
