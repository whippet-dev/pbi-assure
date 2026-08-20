using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class RowLevelSecurityHtmlRendererTests
{
    [Fact]
    public void RenderGroupsEncodedRolesAndFiltersWithinTheirSemanticModels()
    {
        var inventory = Scan([
            File("Review.pbip", "{}"),
            File("Alpha.SemanticModel/definition.pbism", "{}"),
            File("Alpha.SemanticModel/definition/tables/Sales & Targets.tmdl", """
                table 'Sales & Targets'

                    column Region
                        dataType: string

                    column Note
                        dataType: string
                """),
            File("Alpha.SemanticModel/definition/tables/Customers.tmdl", """
                table Customers

                    column Active
                        dataType: boolean
                """),
            File("Alpha.SemanticModel/definition/roles/Regional Admin.tmdl", """
                role 'Regional <Admin>'
                    modelPermission: read

                    tablePermission 'Sales & Targets' =
                            [Region] = "<West>"
                                && NOT CONTAINSSTRING([Note], "<script>")

                    tablePermission Customers = [Active] = TRUE()
                """),
            File("Alpha.SemanticModel/definition/roles/All regions.tmdl", """
                role 'All regions'
                    modelPermission: read
                """),
            File("Beta.SemanticModel/definition.pbism", "{}"),
            File("Beta.SemanticModel/definition/tables/Sales & Targets.tmdl", """
                table 'Sales & Targets'

                    column Region
                        dataType: string
                """),
            File("Beta.SemanticModel/definition/roles/Regional Admin.tmdl", """
                role 'Regional <Admin>'
                    modelPermission: read

                    tablePermission 'Sales & Targets' = [Region] = "North"
                """),
        ]);

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("data-section-target=\"row-level-security\"", html, StringComparison.Ordinal);
        Assert.Contains("<small>Roles and table filters</small>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"row-level-security\"", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("<h3>Alpha</h3>", StringComparison.Ordinal) <
            html.IndexOf("<h3>Beta</h3>", StringComparison.Ordinal));
        Assert.Equal(2, CountOccurrences(html, "Regional &lt;Admin&gt;"));
        Assert.Contains("<strong>All regions</strong><span>0 table filters", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Model permission</dt><dd>Read</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Regional &lt;Admin&gt;</strong><span>2 table filters", html, StringComparison.Ordinal);
        Assert.Contains("<h5><span>Table</span>Customers</h5>", html, StringComparison.Ordinal);
        Assert.Contains("<h5><span>Table</span>Sales &amp; Targets</h5>", html, StringComparison.Ordinal);
        Assert.Contains("[Region] = &quot;&lt;West&gt;&quot;", html, StringComparison.Ordinal);
        Assert.Contains("&amp;&amp; NOT CONTAINSSTRING([Note], &quot;&lt;script&gt;&quot;)", html, StringComparison.Ordinal);
        Assert.Contains("[Region] = &quot;North&quot;", html, StringComparison.Ordinal);
        Assert.Contains("No table filters were found in this role definition.", html, StringComparison.Ordinal);
        Assert.Contains("white-space: pre-wrap; overflow-wrap: anywhere;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<Admin>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT CONTAINSSTRING([Note], \"<script>\")", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderStatesTheProjectOnlySecurityBoundaryWithoutCertificationClaims()
    {
        var inventory = Scan([
            File("Review.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales"),
            File("Model.SemanticModel/definition/roles/Reader.tmdl", """
                role Reader
                    modelPermission: read
                """),
        ]);

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("<strong>Project definitions only</strong>", html, StringComparison.Ordinal);
        Assert.Contains("cannot see who is assigned to roles in Power BI Service", html, StringComparison.Ordinal);
        Assert.Contains("assess effective runtime identity", html, StringComparison.Ordinal);
        Assert.Contains("confirm the overall security design", html, StringComparison.Ordinal);
        Assert.Contains("Complete object-level security and column permissions are not assessed.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("security passed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RLS validated", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compliant", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderLinksUnanalysedRoleMetadataToExistingAnalysisCoverage()
    {
        var inventory = Scan([
            File("Review.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", """
                table Sales

                    column Region
                        dataType: string
                """),
            File("Model.SemanticModel/definition/roles/Reader.tmdl", """
                role Reader
                    modelPermission: read

                    tablePermission Sales = [Region] = "West"
                        columnPermission Region = None
                """),
        ]);

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("Some metadata in this role was not fully checked.", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#analysis-coverage-model-1\">Review analysis coverage</a>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"analysis-coverage-heading\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderOmitsRowLevelSecurityWhenNoRolesExist()
    {
        var inventory = Scan([
            File("Review.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales"),
        ]);

        var html = HtmlReportRenderer.Render(inventory);

        Assert.DoesNotContain("data-section-target=\"row-level-security\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"row-level-security\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Project definitions only", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDoesNotChangeDesktopFixtureAnalysisJsonOrCsv()
    {
        var inventory = ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", "desktop-semantic-constructs"));
        var usagesBefore = inventory.SemanticObjectUsages
            .Select(item => (item.SemanticModel, item.Table, item.ObjectName, item.UsageState, item.ClassificationConfidence))
            .ToArray();
        var limitationsBefore = inventory.AnalysisLimitations.ToArray();
        var jsonBefore = JsonSerializer.Serialize(inventory);
        var csvBefore = SemanticUsageCsvRenderer.Render(inventory);

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("<strong>DynamicUser</strong><span>1 table filter", html, StringComparison.Ordinal);
        Assert.Contains("[UserEmail] = USERPRINCIPALNAME()", html, StringComparison.Ordinal);
        Assert.Contains("<strong>RegionalManager</strong><span>1 table filter", html, StringComparison.Ordinal);
        Assert.Contains("[Region] = &quot;West&quot;", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"analysis-coverage-heading\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Some metadata in this role was not fully checked.", html, StringComparison.Ordinal);
        Assert.Equal(usagesBefore, inventory.SemanticObjectUsages.Select(item =>
            (item.SemanticModel, item.Table, item.ObjectName, item.UsageState, item.ClassificationConfidence)));
        Assert.Equal(limitationsBefore, inventory.AnalysisLimitations);
        Assert.Equal(jsonBefore, JsonSerializer.Serialize(inventory));
        Assert.Equal(csvBefore, SemanticUsageCsvRenderer.Render(inventory));
    }

    private static ProjectInventory Scan(IReadOnlyList<ProjectFileContent> files) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource("RLS review", files));

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static int CountOccurrences(string value, string expected) =>
        value.Split(expected, StringSplitOptions.None).Length - 1;

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
