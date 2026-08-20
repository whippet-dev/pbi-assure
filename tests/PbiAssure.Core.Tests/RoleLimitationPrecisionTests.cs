using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Artifact-sensitive precision for role limitations.
///
/// The registry states what a role construct *can* contain, and must stay conservative. An emitted
/// limitation describes what a scan *did* encounter, so a role file whose entire content was either
/// analysed or is known to hold no model-object reference should not claim skipped dependency evidence
/// that is not there.
///
/// This narrows dependency impact only. It never claims roles are fully supported: the limitation is
/// still emitted, and the support state stays partial.
/// </summary>
public sealed class RoleLimitationPrecisionTests
{
    // ---- 1. Real Desktop RLS-only roles ------------------------------------------------------

    /// <summary>
    /// The Desktop fixture's roles contain a model permission, a table permission and an annotation.
    /// The table permission is analysed; the other two carry no model-object reference. Nothing in these
    /// particular files is both unanalysed and capable of referencing an object.
    /// </summary>
    [Fact]
    public void DesktopRolesWithOnlyAnalysedContentReportNoKnownDependencyEffect()
    {
        var roleLimitations = ScanDesktopFixture().AnalysisLimitations
            .Where(item => item.ConstructType == "role")
            .ToArray();

        Assert.Equal(2, roleLimitations.Length);
        Assert.All(roleLimitations, limitation =>
        {
            // Still reported, still only partially supported.
            Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
            Assert.Contains(AnalysisConcerns.Security, limitation.Concerns);
            // But nothing skipped here can invalidate a usage conclusion.
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
        });
    }

    [Fact]
    public void DesktopRoleContentIsFullyAccountedFor()
    {
        var model = Assert.Single(ScanDesktopFixture().SemanticModels);

        Assert.Equal(2, model.RoleCount);
        Assert.All(model.Roles, role => Assert.Empty(role.UnanalyzedConstructs));
    }

    // ---- 2. RLS dependency behaviour must not regress ---------------------------------------

    [Theory]
    [InlineData("Region")]
    [InlineData("UserEmail")]
    public void RoleReferencedColumnsRemainStructurallyRequiredAndEstablished(string columnName)
    {
        var usage = Assert.Single(
            ScanDesktopFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == columnName);

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    // ---- 4. What still qualifies in the fixture ---------------------------------------------

    /// <summary>
    /// Absence states in the fixture are still qualified, but roles are not among the causes. Which
    /// constructs remain qualifying changes as parsing advances — perspectives left the list once their
    /// members were analysed — so this asserts only that roles are absent from it.
    /// </summary>
    [Fact]
    public void RolesAreNotAmongTheRemainingQualifyingCauses()
    {
        var inventory = ScanDesktopFixture();

        var qualifying = inventory.AnalysisLimitations
            .Where(item => item.DependencyImpact
                is ConstructDependencyImpacts.MayCreateDependencies
                or ConstructDependencyImpacts.DependencyEffectUnknown)
            .Select(item => item.ConstructType)
            .ToArray();

        Assert.NotEmpty(qualifying);
        Assert.DoesNotContain("role", qualifying);
        Assert.Contains(
            inventory.SemanticObjectUsages,
            usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);
    }

    [Fact]
    public void CultureDatabaseAndModelLimitationsRemainWithoutDependencyEffect()
    {
        var inventory = ScanDesktopFixture();

        foreach (var constructType in new[] { "culture", "database", "modelDefinition" })
        {
            var limitation = Assert.Single(
                inventory.AnalysisLimitations,
                item => item.ConstructType == constructType);
            Assert.Equal(
                ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
        }
    }

    // ---- 5 / 6. Unsupported and unknown role content stays conservative ----------------------

    /// <summary>
    /// A role carrying a construct this version does not recognise keeps the conservative impact. The
    /// construct here is deliberately invented: the point is that an unrecognised name is never assumed
    /// harmless, whatever it happens to be.
    /// </summary>
    [Fact]
    public void AnUnrecognisedRoleConstructKeepsTheConservativeImpact()
    {
        var inventory = ScanSynthetic(
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n\n\tsomethingNobodyHasInventedYet Foo\n");

        var limitation = Assert.Single(
            inventory.AnalysisLimitations, item => item.ConstructType == "role");
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.NotEmpty(Assert.Single(Assert.Single(inventory.SemanticModels).Roles).UnanalyzedConstructs);
    }

    /// <summary>
    /// The bounded OLS parser recognises Desktop's inline columnPermission form. That supported content
    /// must not keep a role limitation qualifying unrelated absence states.
    /// </summary>
    [Fact]
    public void ARoleContainingSupportedColumnPermissionsDoesNotKeepTheConservativeImpact()
    {
        var inventory = ScanSynthetic(
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n\t\tcolumnPermission Region = None\n");

        var limitation = Assert.Single(
            inventory.AnalysisLimitations, item => item.ConstructType == "role");
        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
    }

    // ---- 7. Known non-dependency metadata does not force conservatism ------------------------

    [Theory]
    [InlineData("\tmodelPermission: read")]
    [InlineData("\tannotation PBI_Id = 9949dfdbc56843c186a081639d68d821")]
    [InlineData("\textendedProperty Custom = {\"value\":1}")]
    public void KnownNonDependencyRoleMetadataDoesNotForceAQualifyingImpact(string extraLine)
    {
        var inventory = ScanSynthetic(
            $"role Reader\n{extraLine}\n\n\ttablePermission Sales = [Region] = \"West\"\n");

        var limitation = Assert.Single(
            inventory.AnalysisLimitations, item => item.ConstructType == "role");
        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
    }

    // ---- 8. Roles are refined individually ---------------------------------------------------

    [Fact]
    public void RolesAreRefinedIndividuallyRatherThanCollapsedToOneStatus()
    {
        var inventory = ScanSynthetic(
            [
                "role Clean\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n",
                "role Murky\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n\n\tmysteryBlock Thing\n",
            ]);

        var clean = Assert.Single(
            inventory.AnalysisLimitations, item => item.ArtifactPath.EndsWith("Clean.tmdl", StringComparison.Ordinal));
        var murky = Assert.Single(
            inventory.AnalysisLimitations, item => item.ArtifactPath.EndsWith("Murky.tmdl", StringComparison.Ordinal));

        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, clean.DependencyImpact);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, murky.DependencyImpact);
    }

    /// <summary>
    /// One murky role is enough to keep the model's absence states qualified, because the confidence rule
    /// is model-wide. The refinement makes the cause precise, not the outcome softer.
    /// </summary>
    [Fact]
    public void OneRoleWithUnanalysedContentStillQualifiesTheModel()
    {
        var inventory = ScanSynthetic(
            [
                "role Clean\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n",
                "role Murky\n\tmodelPermission: read\n\n\tmysteryBlock Thing\n",
            ],
            table: TableWithRegionAndSpare);

        Assert.Equal(
            ClassificationConfidences.QualifiedByLimitation,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Spare").ClassificationConfidence);
    }

    // ---- 9 / 10. No roles, and model isolation ----------------------------------------------

    [Fact]
    public void AProjectWithoutRolesIsUnchanged()
    {
        var inventory = ScanSynthetic([]);

        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.ConstructType == "role");
        Assert.Empty(Assert.Single(inventory.SemanticModels).Roles);
    }

    [Fact]
    public void RefinementInOneModelDoesNotAffectAnother()
    {
        var files = new List<ProjectFileContent> { File("Two.pbip", "{}") };
        files.AddRange(ModelFiles(
            "Clean", TableWithRegion,
            "role Reader\n\tmodelPermission: read\n\n\ttablePermission Sales = [Region] = \"West\"\n"));
        files.AddRange(ModelFiles(
            "Murky", TableWithRegion,
            "role Reader\n\tmodelPermission: read\n\n\tmysteryBlock Thing\n"));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        Assert.Equal(
            ConstructDependencyImpacts.NoKnownDependencyEffect,
            Assert.Single(inventory.AnalysisLimitations,
                item => item.SemanticModel == "Clean" && item.ConstructType == "role").DependencyImpact);
        Assert.Equal(
            ConstructDependencyImpacts.MayCreateDependencies,
            Assert.Single(inventory.AnalysisLimitations,
                item => item.SemanticModel == "Murky" && item.ConstructType == "role").DependencyImpact);
    }

    // ---- 3. The qualifier stays generic ------------------------------------------------------

    /// <summary>
    /// Guards the architecture: the confidence qualifier must not know what a role is. A refined role
    /// limitation qualifies nothing for the same reason any other NoKnownDependencyEffect limitation
    /// does.
    /// </summary>
    [Fact]
    public void TheConfidenceQualifierContainsNoRoleSpecificLogic()
    {
        var source = System.IO.File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "PbiAssure.Core", "Scanning", "SemanticUsageConfidenceQualifier.cs"));

        Assert.DoesNotContain("role", source, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private const string TableWithRegion =
        "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n";

    private const string TableWithRegionAndSpare =
        "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n" +
        "\n\tcolumn Spare\n\t\tdataType: string\n\t\tsourceColumn: Spare\n";

    private static ProjectInventory ScanDesktopFixture() =>
        ProjectScanner.Scan(Path.Combine(
            RepositoryRoot(), "tests", "fixtures", "desktop-semantic-constructs"));

    private static ProjectInventory ScanSynthetic(string roleFile) => ScanSynthetic([roleFile]);

    private static ProjectInventory ScanSynthetic(
        IReadOnlyList<string> roleFiles,
        string table = TableWithRegion)
    {
        var files = new List<ProjectFileContent> { File("Sales.pbip", "{}") };
        files.AddRange(ModelFiles("Sales", table, roleFiles.ToArray()));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        string table,
        params string[] roleFiles)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return File($"{modelName}.SemanticModel/definition/tables/Sales.tmdl", table);
        foreach (var roleFile in roleFiles)
        {
            var name = roleFile.Split('\n')[0].Replace("role ", string.Empty).Trim();
            yield return File($"{modelName}.SemanticModel/definition/roles/{name}.tmdl", roleFile);
        }
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

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
