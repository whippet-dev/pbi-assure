using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Row-level security dependency analysis.
///
/// A column referenced only by a role filter is required to enforce security, so it must not be reported
/// as a deletion candidate. Role filters are treated as model-structure roots — the same mechanism
/// relationship endpoints and field-parameter metadata already use — so ordinary graph traversal does the
/// work and no new usage state is introduced.
///
/// Tests marked "Desktop" run against genuine Power BI Desktop output. The rest are parser and semantics
/// tests over synthetic TMDL, which prove behaviour but are not evidence of what Desktop serialises.
/// </summary>
public sealed class RoleDependencyTests
{
    // ---- A. Real Desktop role serialization -------------------------------------------------

    [Fact]
    public void DesktopRoleFilesAreDiscoveredWithTheirTablePermissions()
    {
        var model = Assert.Single(ScanDesktopFixture().SemanticModels);

        Assert.Equal(2, model.RoleCount);
        var regional = Assert.Single(model.Roles, role => role.Name == "RegionalManager");
        var dynamicUser = Assert.Single(model.Roles, role => role.Name == "DynamicUser");

        Assert.Equal("read", regional.ModelPermission);
        Assert.Equal("read", dynamicUser.ModelPermission);

        var regionalPermission = Assert.Single(regional.TablePermissions);
        Assert.Equal("Sales", regionalPermission.Table);
        Assert.Equal("[Region] = \"West\"", regionalPermission.FilterExpression);

        var dynamicPermission = Assert.Single(dynamicUser.TablePermissions);
        Assert.Equal("Sales", dynamicPermission.Table);
        Assert.Equal("[UserEmail] = USERPRINCIPALNAME()", dynamicPermission.FilterExpression);
    }

    /// <summary>
    /// Desktop serialises the column reference unqualified. The owning table comes from the
    /// tablePermission declaration, and that is what makes the reference resolvable.
    /// </summary>
    [Theory]
    [InlineData("RegionalManager", "Region")]
    [InlineData("DynamicUser", "UserEmail")]
    public void DesktopUnqualifiedRoleReferencesResolveAgainstTheTablePermissionOwner(
        string roleName,
        string columnName)
    {
        Assert.Contains(
            ScanDesktopFixture().SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.TablePermission &&
                edge.FromObjectName == roleName &&
                edge.FromObjectType == SemanticObjectTypes.Role &&
                edge.ToTable == "Sales" &&
                edge.ToObjectName == columnName &&
                edge.ToObjectType == SemanticObjectTypes.Column);
    }

    [Fact]
    public void DesktopRoleExpressionsProduceNoSpuriousReferences()
    {
        var inventory = ScanDesktopFixture();

        // A DAX function is not a model object, and neither is a string literal.
        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.ToObjectName.Contains("USERPRINCIPALNAME", StringComparison.OrdinalIgnoreCase) ||
                edge.ToObjectName == "West");
        Assert.DoesNotContain(
            inventory.UnresolvedSemanticDependencies,
            item => item.ReferenceText.Contains("USERPRINCIPALNAME", StringComparison.OrdinalIgnoreCase) ||
                item.ReferenceText.Contains("West", StringComparison.Ordinal));
    }

    // ---- B. Classification ------------------------------------------------------------------

    /// <summary>
    /// The essential invariant: an object required to enforce security is not a deletion candidate.
    /// StructurallyRequired is the state for objects the model itself requires rather than the report —
    /// the same state relationship endpoints and field-parameter metadata already produce.
    /// </summary>
    [Theory]
    [InlineData("Region")]
    [InlineData("UserEmail")]
    public void DesktopColumnsUsedOnlyByRoleFiltersAreStructurallyRequired(string columnName)
    {
        var usage = Assert.Single(
            ScanDesktopFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == columnName);

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
    }

    // ---- D. A role is not report usage ------------------------------------------------------

    [Fact]
    public void ARoleReferenceDoesNotMakeAnObjectDirectlyUsed()
    {
        var usage = Assert.Single(
            ScanSynthetic(RoleFile("Reader", "Sales", "[Region] = \"West\"")).SemanticObjectUsages,
            u => u.ObjectName == "Region");

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
        Assert.NotEqual(SemanticUsageStates.DirectlyUsed, usage.UsageState);
        Assert.Empty(usage.DirectReportReferences);
        Assert.False(usage.IsDirectlyReferencedByReport);
    }

    // ---- C. Traversal from the role root ----------------------------------------------------

    /// <summary>
    /// A role filter that references a measure must pull that measure's own dependencies in through
    /// ordinary traversal, rather than only promoting the object it names.
    /// </summary>
    [Fact]
    public void DependenciesOfARoleReferencedMeasureAreAlsoRequired()
    {
        var inventory = ScanSynthetic(
            RoleFile("Reader", "Sales", "[Threshold] > 0"),
            table: "table Sales\n" +
                   "\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
                   "\n\tmeasure Threshold = SUM(Sales[Amount])\n");

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Threshold").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Amount").UsageState);
    }

    // ---- E / F / G. Combination, isolation, absence -----------------------------------------

    [Fact]
    public void ReferencesFromSeveralRolesCombineRatherThanOverwrite()
    {
        var inventory = ScanSynthetic(
            [
                RoleFile("First", "Sales", "[Region] = \"West\""),
                RoleFile("Second", "Sales", "[UserEmail] = USERPRINCIPALNAME()"),
            ],
            table: "table Sales\n" +
                   "\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n" +
                   "\n\tcolumn UserEmail\n\t\tdataType: string\n\t\tsourceColumn: UserEmail\n");

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "UserEmail").UsageState);
    }

    [Fact]
    public void ARoleInOneModelDoesNotRootAnIdenticallyNamedObjectInAnother()
    {
        var files = new List<ProjectFileContent> { File("Two.pbip", "{}") };
        files.AddRange(ModelFiles("Secured", TableWithRegion, RoleFile("Reader", "Sales", "[Region] = \"West\"")));
        files.AddRange(ModelFiles("Open", TableWithRegion));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "Secured" && u.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "Open" && u.ObjectName == "Region").UsageState);
    }

    [Fact]
    public void AProjectWithoutRolesIsUnaffected()
    {
        var inventory = ScanSynthetic();

        Assert.Empty(Assert.Single(inventory.SemanticModels).Roles);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.TablePermission);
    }

    // ---- J. Unresolved references -----------------------------------------------------------

    /// <summary>
    /// A role filter naming an object that does not exist must follow the existing unresolved-dependency
    /// path: recorded as evidence, never invented as a semantic object, never a crash.
    /// </summary>
    [Fact]
    public void ARoleReferenceToAMissingObjectIsRetainedAsUnresolved()
    {
        var inventory = ScanSynthetic(RoleFile("Reader", "Sales", "[Missing] = \"West\""));

        var unresolved = Assert.Single(
            inventory.UnresolvedSemanticDependencies,
            item => item.DependencyKind == SemanticDependencyKinds.TablePermission);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Equal("Reader", unresolved.FromObjectName);
        Assert.Contains("Missing", unresolved.ReferenceText, StringComparison.Ordinal);
        Assert.DoesNotContain(inventory.SemanticObjectUsages, u => u.ObjectName == "Missing");
    }

    [Fact]
    public void ATablePermissionForAMissingTableIsRetainedAsUnresolved()
    {
        var inventory = ScanSynthetic(RoleFile("Reader", "Ghost", "[Region] = \"West\""));

        Assert.Contains(
            inventory.UnresolvedSemanticDependencies,
            item => item.DependencyKind == SemanticDependencyKinds.TablePermission &&
                item.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.NotFound);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    // ---- Parser robustness (synthetic, not Desktop evidence) --------------------------------

    [Fact]
    public void AQuotedTablePermissionOwnerResolvesUnqualifiedReferences()
    {
        var inventory = ScanSynthetic(
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission 'Sales Data' = [Region] = \"West\"\n",
            table: "table 'Sales Data'\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n");

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    [Fact]
    public void AMultiLineFilterExpressionIsRead()
    {
        var inventory = ScanSynthetic(
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission Sales =\n\t\t\t[Region] = \"West\"\n\t\t\t\t|| [Region] = \"East\"\n");

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    [Fact]
    public void SeveralTablePermissionsInOneRoleAreAllRead()
    {
        var inventory = ScanSynthetic(
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n\n\ttablePermission Other = [Code] = 1\n",
            table: TableWithRegion,
            extraTable: ("Other", "table Other\n\n\tcolumn Code\n\t\tdataType: int64\n\t\tsourceColumn: Code\n"));

        Assert.Equal(2, Assert.Single(inventory.SemanticModels).TablePermissionCount);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Code").UsageState);
    }

    [Fact]
    public void AQualifiedReferenceToAnotherTableIsResolved()
    {
        var inventory = ScanSynthetic(
            RoleFile("Reader", "Sales", "Other[Code] = 1"),
            table: TableWithRegion,
            extraTable: ("Other", "table Other\n\n\tcolumn Code\n\t\tdataType: int64\n\t\tsourceColumn: Code\n"));

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Code").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    // ---- H. Revised role limitation semantics -----------------------------------------------

    /// <summary>
    /// Fully accounted role files have no unsupported role content to surface in Analysis coverage.
    /// </summary>
    [Fact]
    public void FullyAccountedRoleFilesDoNotReportPartialSupport()
    {
        Assert.DoesNotContain(
            ScanDesktopFixture().AnalysisLimitations,
            item => item.ConstructType == "role");
    }

    [Fact]
    public void EachFullyAccountedRoleFileIsOmittedFromAnalysisCoverage()
    {
        Assert.Equal(
            0,
            ScanDesktopFixture().AnalysisLimitations.Count(item => item.ConstructType == "role"));
    }

    // ---- I. Confidence interaction ----------------------------------------------------------

    /// <summary>
    /// Confidence is still derived from DependencyImpact alone. A column promoted to a positive state by
    /// a role filter is Established because positive states are never qualified — not because anything
    /// special-cases roles.
    /// </summary>
    [Theory]
    [InlineData("Region")]
    [InlineData("UserEmail")]
    public void DesktopColumnsRequiredByRoleFiltersAreEstablished(string columnName)
    {
        var usage = Assert.Single(
            ScanDesktopFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == columnName);

        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    /// <summary>
    /// The fixture still contains unsupported perspective and function metadata, so absence states there
    /// remain qualified. RLS support was never expected to clear the whole fixture.
    /// </summary>
    [Fact]
    public void RemainingUnsupportedMetadataStillQualifiesAbsenceStates()
    {
        var inventory = ScanDesktopFixture();

        Assert.Contains(
            inventory.AnalysisLimitations,
            item => item.ConstructType is "perspective" or "function" &&
                item.DependencyImpact == ConstructDependencyImpacts.MayCreateDependencies);
        Assert.Contains(
            inventory.SemanticObjectUsages,
            usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private const string TableWithRegion =
        "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n";

    private static string RoleFile(string roleName, string table, string filter) =>
        $"role {roleName}\n\tmodelPermission: read\n\n\ttablePermission {table} = {filter}\n";

    private static ProjectInventory ScanDesktopFixture()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return ProjectScanner.Scan(Path.Combine(
                    directory.FullName, "tests", "fixtures", "desktop-semantic-constructs"));
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }

    private static ProjectInventory ScanSynthetic(
        string? roleFile = null,
        string table = TableWithRegion,
        (string Name, string Content)? extraTable = null) =>
        ScanSynthetic(roleFile is null ? [] : [roleFile], table, extraTable);

    private static ProjectInventory ScanSynthetic(
        IReadOnlyList<string> roleFiles,
        string table = TableWithRegion,
        (string Name, string Content)? extraTable = null)
    {
        var files = new List<ProjectFileContent> { File("Sales.pbip", "{}") };
        files.AddRange(ModelFiles("Sales", table, roleFiles.ToArray()));
        if (extraTable is { } extra)
        {
            files.Add(File($"Sales.SemanticModel/definition/tables/{extra.Name}.tmdl", extra.Content));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        string table,
        params string[] roleFiles)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return File($"{modelName}.SemanticModel/definition/tables/Sales.tmdl", table);
        var index = 0;
        foreach (var roleFile in roleFiles)
        {
            var name = roleFile.Split('\n')[0].Replace("role ", string.Empty).Trim();
            yield return File(
                $"{modelName}.SemanticModel/definition/roles/{(name.Length > 0 ? name : $"Role{index}")}.tmdl",
                roleFile);
            index++;
        }
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
