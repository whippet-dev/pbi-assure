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
            var expectedConfidence = Text(expected, "confidence");
            Assert.True(expectedConfidence == usage.ClassificationConfidence,
                $"Expected {semanticModel}/{table}/{objectName} confidence '{expectedConfidence}', " +
                $"but found '{usage.ClassificationConfidence}'.");
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

        foreach (var expected in root.GetProperty("machineContract").GetProperty("semanticTableUsages").EnumerateArray())
        {
            var usage = Assert.Single(inventory.SemanticTableUsages, candidate =>
                candidate.SemanticModel == Text(expected, "semanticModel") &&
                candidate.Table == Text(expected, "table"));
            Assert.Equal(Text(expected, "state"), usage.UsageState);
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("powerQueryDependencies").EnumerateArray())
        {
            Assert.Contains(inventory.PowerQueryDependencies, dependency =>
                dependency.SemanticModel == Text(expected, "semanticModel") &&
                dependency.FromQueryName == Text(expected, "fromQuery") &&
                dependency.FromSourceKind == Text(expected, "fromSourceKind") &&
                dependency.ToQueryName == Text(expected, "toQuery") &&
                dependency.ToSourceKind == Text(expected, "toSourceKind"));
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("powerQueryColumnUsages").EnumerateArray())
        {
            Assert.Contains(inventory.PowerQueryColumnUsages, usage =>
                usage.SemanticModel == Text(expected, "semanticModel") &&
                usage.SourceQuery == Text(expected, "sourceQuery") &&
                usage.SourceTable == Text(expected, "sourceTable") &&
                usage.SourceColumn == Text(expected, "sourceColumn") &&
                usage.ConsumerQuery == Text(expected, "consumerQuery") &&
                usage.UsageKind == Text(expected, "usageKind") &&
                usage.MFunction == Text(expected, "mFunction") &&
                usage.StepName == Text(expected, "stepName"));
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("dataSources").EnumerateArray())
        {
            Assert.Contains(inventory.DataSources, source =>
                source.QueryName == Text(expected, "query") &&
                source.ConnectorFamily == Text(expected, "connectorFamily") &&
                source.LocationKind == Text(expected, "locationKind"));
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("reportMeasureReachability").EnumerateArray())
        {
            var node = Assert.Single(inventory.SemanticNodeReachability, candidate =>
                candidate.SemanticModel == Text(expected, "semanticModel") &&
                candidate.Table == Text(expected, "table") &&
                candidate.ObjectName == Text(expected, "object") &&
                candidate.ObjectType == SemanticObjectTypes.ReportMeasure);
            Assert.Equal(expected.GetProperty("reachableFromReport").GetBoolean(), node.ReachableFromReport);
        }

        var directUsage = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        foreach (var expected in root.GetProperty("machineContract").GetProperty("directUsageNegativeControls").EnumerateArray())
        {
            var semanticModel = Text(expected, "semanticModel");
            var table = Text(expected, "table");
            var objectName = Text(expected, "object");
            Assert.DoesNotContain(directUsage.Usages, usage => usage.SemanticModel == semanticModel &&
                usage.Table == table && usage.ObjectName == objectName);
            var summary = Assert.Single(directUsage.ObjectSummaries, candidate => candidate.SemanticModel == semanticModel &&
                candidate.Table == table && candidate.ObjectName == objectName);
            Assert.Equal(Text(expected, "userFacing"), summary.UserFacing);
        }

        foreach (var expected in root.GetProperty("machineContract").GetProperty("analysisLimitations").EnumerateArray())
        {
            Assert.Contains(inventory.AnalysisLimitations, limitation =>
                limitation.LimitationId == Text(expected, "limitationId") &&
                limitation.Scope == Text(expected, "scope") &&
                limitation.SemanticModel == Text(expected, "semanticModel") &&
                limitation.ConstructType == Text(expected, "constructType") &&
                limitation.ArtifactPath == Text(expected, "artifactPath") &&
                limitation.SupportState == Text(expected, "supportState") &&
                limitation.DependencyImpact == Text(expected, "dependencyImpact"));
        }

        // Formatting contract equality is intentionally scoped to the principal report.
        var formattingClassifications = inventory.Reports.Single(report => report.Name == "PbiAssureCoverage")
            .Pages.SelectMany(page => page.Visuals).SelectMany(visual => visual.PersistedFormatting)
            .Select(formatting => formatting.Classification).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Assert.Equal(Strings(root.GetProperty("machineContract"), "themeFormattingClassifications"),
            formattingClassifications);

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

        var classificationConfidences = inventory.SemanticObjectUsages
            .Select(usage => usage.ClassificationConfidence)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(Strings(global, "classificationConfidences"), classificationConfidences);

        var reportSchemaObservations = inventory.Reports.SelectMany(report => report.SchemaObservations).ToArray();
        var reportSchemaObservationStates = reportSchemaObservations.Select(observation => observation.State)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(Strings(global, "reportSchemaObservationStates"), reportSchemaObservationStates);
        Assert.DoesNotContain(reportSchemaObservations, observation =>
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
