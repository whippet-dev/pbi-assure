using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DaxUserRelationshipExtractorTests
{
    private static readonly HashSet<string> KnownTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Customers",
        "Sales",
        "Sales Territory",
    };

    [Theory]
    [InlineData("USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID])", "Customers", "CustomerID", "Sales", "ShippingCustomerID")]
    [InlineData("USERELATIONSHIP( 'Sales Territory' [Key] , Customers [CustomerID] )", "Sales Territory", "Key", "Customers", "CustomerID")]
    [InlineData("USERELATIONSHIP(\n  Customers[CustomerID],\n  Sales[ShippingCustomerID]\n)", "Customers", "CustomerID", "Sales", "ShippingCustomerID")]
    public void ExtractsOnlyTwoExplicitQualifiedColumnArguments(
        string expression,
        string firstTable,
        string firstColumn,
        string secondTable,
        string secondColumn)
    {
        var call = Assert.Single(DaxReferenceExtractor.ExtractUserRelationshipCalls(expression));

        Assert.Equal(firstTable, call.First.Table);
        Assert.Equal(firstColumn, call.First.Column);
        Assert.Equal(secondTable, call.Second.Table);
        Assert.Equal(secondColumn, call.Second.Column);
    }

    [Fact]
    public void ExtractsMultipleCallsAndLeavesOrdinaryReferenceExtractionUnchanged()
    {
        const string expression = "CALCULATE([Total], USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID]), USERELATIONSHIP(Customers[CustomerID], Sales[ReferralCustomerID]))";

        var calls = DaxReferenceExtractor.ExtractUserRelationshipCalls(expression);
        var references = DaxReferenceExtractor.Extract(expression, KnownTables);

        Assert.Equal(2, calls.Length);
        Assert.Contains(references, reference => reference.Table == "Customers" && reference.ObjectName == "CustomerID");
        Assert.Contains(references, reference => reference.Table == "Sales" && reference.ObjectName == "ShippingCustomerID");
        Assert.Contains(references, reference => reference.Table == "Sales" && reference.ObjectName == "ReferralCustomerID");
    }

    [Theory]
    [InlineData("// USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID])")]
    [InlineData("\"USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID])\"")]
    [InlineData("USERELATIONSHIP(Customers[CustomerID])")]
    [InlineData("USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID], Sales[ReferralCustomerID])")]
    [InlineData("USERELATIONSHIP(Customers[CustomerID], Sales[ShippingCustomerID] + 1)")]
    [InlineData("VAR shipping = Sales[ShippingCustomerID] RETURN USERELATIONSHIP(Customers[CustomerID], shipping)")]
    [InlineData("USERELATIONSHIP(Customers[CustomerID], RELATED(Sales[ShippingCustomerID]))")]
    public void IgnoresUnsupportedOrNonExecutableShapes(string expression)
    {
        Assert.Empty(DaxReferenceExtractor.ExtractUserRelationshipCalls(expression));
    }
}
