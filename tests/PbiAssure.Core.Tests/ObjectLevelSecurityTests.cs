using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Bounded object-level security coverage. The Desktop fixture pins the persisted TMDL syntax; synthetic
/// files cover parser boundaries without claiming that Power BI Desktop emits those edge cases.
/// </summary>
public sealed class ObjectLevelSecurityTests
{
    [Fact]
    public void DesktopFixturePreservesTheObservedTableAndColumnPermissionShapes()
    {
        var model = Assert.Single(ScanDesktopFixture().SemanticModels);
        var role = Assert.Single(model.Roles, item => item.Name == "RestrictedViewer");

        Assert.Equal("read", role.ModelPermission);
        Assert.Equal(0, role.TablePermissionCount);
        Assert.Equal(2, role.ObjectLevelPermissionCount);

        var employee = Assert.Single(role.TablePermissions, item => item.Table == "Employee");
        Assert.Equal(string.Empty, employee.FilterExpression);
        Assert.Null(employee.MetadataPermission);
        var salary = Assert.Single(employee.ColumnPermissions);
        Assert.Equal("Salary", salary.Column);
        Assert.Equal("none", salary.Permission);

        var confidential = Assert.Single(role.TablePermissions, item => item.Table == "Confidential");
        Assert.Equal(string.Empty, confidential.FilterExpression);
        Assert.Equal("none", confidential.MetadataPermission);
        Assert.Empty(confidential.ColumnPermissions);
        Assert.Empty(role.UnanalyzedConstructs);
    }

    [Fact]
    public void ExplicitColumnLevelOlsCreatesOnlyTheNamedStructuralDependency()
    {
        var inventory = ScanDesktopFixture();

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Employee", "Name").UsageState);
        var salary = Usage(inventory, "Employee", "Salary");
        Assert.Equal(SemanticUsageStates.StructurallyRequired, salary.UsageState);
        Assert.Equal(ClassificationConfidences.Established, salary.ClassificationConfidence);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Employee", "Department").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Employee", "EmployeeID").UsageState);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.ObjectLevelPermission &&
            edge.FromObjectName == "RestrictedViewer" &&
            edge.ToTable == "Employee" &&
            edge.ToObjectName == "Salary");
    }

    [Fact]
    public void TableLevelOlsMakesOnlyTheProtectedTableStructurallyRequired()
    {
        var inventory = ScanDesktopFixture();

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            Assert.Single(inventory.SemanticTableUsages, item => item.Table == "Confidential").UsageState);
        Assert.All(
            inventory.SemanticObjectUsages.Where(item => item.Table == "Confidential"),
            usage => Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState));
        var dependency = Assert.Single(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.ObjectLevelPermission &&
            edge.FromObjectName == "RestrictedViewer" &&
            edge.ToTable == "Confidential");
        Assert.Equal(SemanticObjectTypes.Table, dependency.ToObjectType);
        Assert.Equal("Confidential", dependency.ToObjectName);
        Assert.DoesNotContain(inventory.Findings, finding =>
            finding.Message.Contains("object-level security", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AbsentTableMetadataPermissionDoesNotCreateATableOlsRoot()
    {
        var inventory = Scan("""
            role Reader
                tablePermission Confidential
            """);

        var permission = Assert.Single(Assert.Single(inventory.SemanticModels).Roles).TablePermissions.Single();
        Assert.Null(permission.MetadataPermission);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            Assert.Single(inventory.SemanticTableUsages, item => item.Table == "Confidential").UsageState);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.DependencyKind == SemanticDependencyKinds.ObjectLevelPermission && edge.ToTable == "Confidential");
    }

    [Fact]
    public void SupportedOlsOnlyRoleDoesNotCreateRoleAnalysisCoverage()
    {
        var inventory = ScanDesktopFixture();

        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.ConstructType == "role");
        Assert.DoesNotContain("Some metadata in this role was not fully checked.", HtmlReportRenderer.Render(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void MixedRowAndObjectLevelPermissionsRemainScopedAndFollowingMetadataIsNotSwallowed()
    {
        var inventory = Scan("""
            role Reader
                modelPermission: read

                tablePermission Employee = [Department] = "Sales"
                    columnPermission Salary = none
                    annotation PBI_Id = fixture-id

                tablePermission Confidential
                    metadataPermission: none
            """);
        var role = Assert.Single(Assert.Single(inventory.SemanticModels).Roles);

        Assert.Equal(1, role.TablePermissionCount);
        Assert.Equal(2, role.ObjectLevelPermissionCount);
        Assert.Equal("[Department] = \"Sales\"", Assert.Single(role.TablePermissions, item => item.Table == "Employee").FilterExpression);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, "Employee", "Department").UsageState);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, "Employee", "Salary").UsageState);
        Assert.Empty(role.UnanalyzedConstructs);
    }

    [Fact]
    public void UnsupportedRoleMetadataRemainsVisibleToAnalysisCoverage()
    {
        var inventory = Scan("""
            role Reader
                tablePermission Employee
                    columnPermission Salary = none
                futureRoleThing whatever
            """);

        var role = Assert.Single(Assert.Single(inventory.SemanticModels).Roles);
        Assert.Contains("futureRoleThing", role.UnanalyzedConstructs, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            ConstructDependencyImpacts.MayCreateDependencies,
            Assert.Single(inventory.AnalysisLimitations, item => item.ConstructType == "role").DependencyImpact);
    }

    [Fact]
    public void SecurityRolesHtmlShowsObjectLevelPermissionsWithoutAServiceSecurityVerdict()
    {
        var html = HtmlReportRenderer.Render(ScanDesktopFixture());

        Assert.Contains(">Security roles<", html, StringComparison.Ordinal);
        Assert.Contains("Roles, filters and object permissions", html, StringComparison.Ordinal);
        Assert.Contains("Table protected</span>Confidential", html, StringComparison.Ordinal);
        Assert.Contains("Column protected</span>Employee[Salary]", html, StringComparison.Ordinal);
        Assert.Contains("Metadata access: None", html, StringComparison.Ordinal);
        Assert.Contains("cannot see who is assigned to roles in Power BI Service", html, StringComparison.Ordinal);
        Assert.DoesNotContain("security passed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compliant", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityRolesHtmlEncodesObjectLevelPermissionNames()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource(
            "OLS encoding",
            [
                File("Ols.pbip", "{}"),
                File("Ols.SemanticModel/definition.pbism", "{}"),
                File("Ols.SemanticModel/definition/tables/Employee.tmdl", """
                    table 'Employee <script>'
                        column 'Salary <script>'
                            dataType: decimal
                    """),
                File("Ols.SemanticModel/definition/roles/Reader.tmdl", """
                    role 'Restricted <script>'
                        tablePermission 'Employee <script>'
                            columnPermission 'Salary <script>' = none
                    """),
            ]));

        var html = HtmlReportRenderer.Render(inventory);

        Assert.Contains("Employee &lt;script&gt;[Salary &lt;script&gt;]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Employee <script>[Salary <script>]", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OlsInventoryIsAdditiveJsonAndDoesNotChangeTheSemanticUsageCsvShape()
    {
        var inventory = ScanDesktopFixture();
        var json = JsonSerializer.Serialize(inventory);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Contains("\"MetadataPermission\":\"none\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ColumnPermissions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Column\":\"Salary\"", json, StringComparison.Ordinal);
        Assert.StartsWith("Report,Table,Object,ObjectType,SemanticUsage", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Object-level permission", csv, StringComparison.OrdinalIgnoreCase);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, item => item.Table == table && item.ObjectName == name);

    private static ProjectInventory ScanDesktopFixture() =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", "desktop-ols-evidence"));

    private static ProjectInventory Scan(string role) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource(
            "OLS synthetic",
            [
                File("Ols.pbip", "{}"),
                File("Ols.SemanticModel/definition.pbism", "{}"),
                File("Ols.SemanticModel/definition/tables/Employee.tmdl", """
                    table Employee

                        column EmployeeID
                            dataType: int64

                        column Name
                            dataType: string

                        column Department
                            dataType: string

                        column Salary
                            dataType: decimal
                    """),
                File("Ols.SemanticModel/definition/tables/Confidential.tmdl", """
                    table Confidential

                        column RecordID
                            dataType: int64

                        column Category
                            dataType: string

                        column Notes
                            dataType: string
                    """),
                File("Ols.SemanticModel/definition/roles/Reader.tmdl", role),
            ]));

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

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
