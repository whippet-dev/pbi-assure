using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CoverageFixtureManifestTests
{
    [Fact]
    public void CanonicalCoverageManifestReconcilesStableMachineContract()
    {
        var fixturePath = FixturePath();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixturePath, "coverage-manifest.json")));
        var root = manifest.RootElement;
        var inventory = ProjectScanner.Scan(fixturePath);

        var constructIds = root.GetProperty("constructs").EnumerateArray()
            .Select(construct => construct.GetProperty("id").GetString()!)
            .ToArray();
        Assert.Equal(constructIds.Length, constructIds.Distinct(StringComparer.Ordinal).Count());

        foreach (var expected in root.GetProperty("machineContract").GetProperty("semanticUsages").EnumerateArray())
        {
            var semanticModel = Text(expected, "semanticModel");
            var table = Text(expected, "table");
            var objectName = Text(expected, "object");
            var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
                candidate.SemanticModel == semanticModel && candidate.Table == table && candidate.ObjectName == objectName);
            Assert.Equal(Text(expected, "state"), usage.UsageState);
            if (expected.TryGetProperty("confidence", out var confidence))
            {
                Assert.Equal(confidence.GetString(), usage.ClassificationConfidence);
            }
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("dependencies").EnumerateArray())
        {
            Assert.Contains(inventory.SemanticDependencies, dependency =>
                dependency.SemanticModel == Text(expected, "semanticModel") &&
                dependency.DependencyKind == Text(expected, "kind") &&
                dependency.FromTable == Text(expected, "fromTable") &&
                dependency.FromObjectName == Text(expected, "fromObject") &&
                dependency.ToTable == Text(expected, "toTable") &&
                dependency.ToObjectName == Text(expected, "toObject"));
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("exclusiveIncomingDependencies").EnumerateArray())
        {
            var incoming = Assert.Single(inventory.SemanticDependencies, dependency =>
                dependency.SemanticModel == Text(expected, "semanticModel") &&
                dependency.ToTable == Text(expected, "toTable") &&
                dependency.ToObjectName == Text(expected, "toObject"));
            Assert.Equal(Text(expected, "kind"), incoming.DependencyKind);
            Assert.Equal(Text(expected, "fromTable"), incoming.FromTable);
            Assert.Equal(Text(expected, "fromObject"), incoming.FromObjectName);
        }

        var global = root.GetProperty("globalExpectations");
        var expectedFindings = global.GetProperty("findingRuleMultiplicities").EnumerateObject()
            .Select(property => (RuleId: property.Name, Count: property.Value.GetInt32()))
            .OrderBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();
        var actualFindings = inventory.Findings.GroupBy(finding => finding.RuleId)
            .Select(group => (RuleId: group.Key, Count: group.Count()))
            .OrderBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedFindings, actualFindings);

        Assert.Equal(
            Strings(global, "dependencyKinds"),
            inventory.SemanticDependencies.Select(dependency => dependency.DependencyKind)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());

        var emittedCoverageStates = inventory.SemanticObjectUsages
            .Select(usage => usage.ClassificationConfidence)
            .Concat(inventory.Reports.SelectMany(report => report.SchemaObservations).Select(observation => observation.State))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(Strings(global, "analysisCoverageStates"), emittedCoverageStates);
        Assert.DoesNotContain(inventory.Reports.SelectMany(report => report.SchemaObservations), observation =>
            observation.State == ReportSchemaObservationStates.MetadataMissing);
    }

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    private static string[] Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(value => value.GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}
