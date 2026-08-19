using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// User-facing presentation of analysis limitations and classification confidence.
///
/// Two concepts are deliberately separate in the domain and must stay separate on screen: the usage
/// state says what PBI Assure found, the classification confidence says how complete the evidence behind
/// that answer is. Neither may be inferred from the other, and qualification must never read as a defect
/// in the object.
///
/// The load-bearing constraint is noise. One unanalysed construct can qualify most of a model, so the
/// explanation is summarised once at model scope rather than repeated beside every affected object.
/// </summary>
public sealed class AnalysisCoveragePresentationTests
{
    // ---- 1 / 2 / 3. Established versus qualified, without inventing a state --------------------

    [Fact]
    public void AnEstablishedClassificationRendersNoQualifiedIndicator()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established),
            limitations: []);

        // The class exists in the stylesheet either way; what must be absent is a rendered marker.
        Assert.DoesNotContain("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.Contains(">Apparently unused</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AQualifiedClassificationRendersADiscoverableIndicator()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        Assert.Contains("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.Contains(">Qualified<", html, StringComparison.Ordinal);
        // Discoverable rather than tooltip-only: the marker navigates to the explanation.
        Assert.Contains("href=\"#analysis-coverage-model-1\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same usage state with different confidence must render differently, and the difference must
    /// not be a different usage label — that would be a sixth state by the back door.
    /// </summary>
    [Fact]
    public void StateAndConfidenceRemainSeparate()
    {
        var established = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established),
            limitations: []);
        var qualified = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        Assert.NotEqual(established, qualified);
        foreach (var html in new[] { established, qualified })
        {
            Assert.Contains("<span class=\"badge badge-unused\">Apparently unused</span>", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Qualified apparently unused", qualified, StringComparison.Ordinal);
        Assert.DoesNotContain("Apparently unused (qualified)", qualified, StringComparison.Ordinal);
    }

    // ---- 4. The other absence state ----------------------------------------------------------

    [Fact]
    public void AQualifiedUnusedBranchClassificationRendersTheIndicator()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.UsedOnlyByUnusedBranch, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        Assert.Contains("data-usage-state=\"UsedOnlyByUnusedBranch\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        // The state itself is not a warning, so the marker must not be rendered as one.
        Assert.DoesNotContain("badge-warning\">Qualified", html, StringComparison.Ordinal);
    }

    // ---- 5. Positive-state future proofing ----------------------------------------------------

    /// <summary>
    /// Today only absence states are qualified, because every unanalysed construct can only add
    /// references. The reserved MayInvalidateExistingEvidence impact would change that, so the renderer
    /// must read the confidence the domain object carries rather than encode today's registry.
    ///
    /// This constructs the combination directly rather than changing any propagation rule.
    /// </summary>
    [Fact]
    public void AQualifiedPositiveStateRendersTheIndicatorGenerically()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.StructurallyRequired, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        Assert.Contains("data-usage-state=\"StructurallyRequired\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"confidence-flag\"", html, StringComparison.Ordinal);
    }

    // ---- 6. Model-level summary ---------------------------------------------------------------

    [Fact]
    public void AModelWithADependencyAffectingLimitationExplainsItself()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        Assert.Contains("id=\"analysis-coverage\"", html, StringComparison.Ordinal);
        Assert.Contains("Analysis coverage", html, StringComparison.Ordinal);
        // Support state and dependency implication in product language, not enum names.
        Assert.Contains("Partially analysed", html, StringComparison.Ordinal);
        Assert.Contains("May affect usage classification", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PartiallyAnalyzed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("MayCreateDependencies", html, StringComparison.Ordinal);
        // The count of affected classifications, as a count and not as a score.
        Assert.Contains("1 of 1 object classification", html, StringComparison.Ordinal);
        Assert.DoesNotContain("%", html.AsSpan(
            html.IndexOf("id=\"analysis-coverage\"", StringComparison.Ordinal),
            html.IndexOf("</section>", html.IndexOf("id=\"analysis-coverage\"", StringComparison.Ordinal), StringComparison.Ordinal) -
            html.IndexOf("id=\"analysis-coverage\"", StringComparison.Ordinal)).ToString(), StringComparison.Ordinal);
    }

    // ---- 7. One limitation, many qualified objects --------------------------------------------

    [Fact]
    public void OneLimitationExplainingManyObjectsIsNotRepeatedPerObject()
    {
        var limitation = QualifyingLimitation();
        var usages = Enumerable.Range(1, 12)
            .Select(index => Usage("Sales", $"Column{index}", SemanticUsageStates.ApparentlyUnused,
                ClassificationConfidences.QualifiedByLimitation))
            .ToArray();

        var html = RenderWithUsages(usages, [limitation]);

        Assert.Equal(12, Occurrences(html, "class=\"confidence-flag\""));
        Assert.Equal(1, Occurrences(html, limitation.Reason));
    }

    // ---- 8. Multiple limitations ---------------------------------------------------------------

    [Fact]
    public void MultipleLimitationsRenderDeterministicallyAndSeparateQualifyingOnes()
    {
        var html = RenderWithUsages(
            [Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation)],
            [
                Limitation("culture", ConstructSupportStates.NotYetAnalyzed, ConstructDependencyImpacts.NoKnownDependencyEffect,
                    "definition/cultures/en-US.tmdl", "Cultures are not analysed."),
                QualifyingLimitation(),
                Limitation("unknownThing", ConstructSupportStates.Unrecognized, ConstructDependencyImpacts.DependencyEffectUnknown,
                    "definition/unknownThing.tmdl", "This file is not recognised."),
            ]);

        // Both qualifying constructs are shown up front; the harmless one is filed under the disclosure.
        var qualifyingBlock = Between(html, "coverage-qualifying", "coverage-other");
        Assert.Contains("DAX user-defined functions", qualifyingBlock, StringComparison.Ordinal);
        Assert.Contains("Unknown thing", qualifyingBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Cultures and translations", qualifyingBlock, StringComparison.Ordinal);
        Assert.Contains("Cultures and translations", html, StringComparison.Ordinal);
        Assert.Contains("2 analysis limitations may affect usage classification", html, StringComparison.Ordinal);
    }

    // ---- 9. No limitations ----------------------------------------------------------------------

    [Fact]
    public void AProjectWithNoLimitationsShowsNoCoverageSection()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established),
            limitations: []);

        Assert.DoesNotContain("id=\"analysis-coverage\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-section-target=\"analysis-coverage\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Analysis coverage", html, StringComparison.Ordinal);
    }

    // ---- 10. A limitation that cannot affect confidence -----------------------------------------

    /// <summary>
    /// Every model emits a few files PBI Assure does not read but has established carry no object
    /// references. They are still disclosed, because hiding them would misrepresent coverage, but they
    /// must not imply that any classification is qualified.
    /// </summary>
    [Fact]
    public void ANonQualifyingLimitationDoesNotImplyQualifiedConfidence()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established),
            limitations:
            [
                Limitation("culture", ConstructSupportStates.NotYetAnalyzed, ConstructDependencyImpacts.NoKnownDependencyEffect,
                    "definition/cultures/en-US.tmdl", "Cultures are not analysed."),
            ]);

        Assert.Contains("id=\"analysis-coverage\"", html, StringComparison.Ordinal);
        Assert.Contains("None of them affect usage classification", html, StringComparison.Ordinal);
        Assert.Contains("No known effect on usage classification", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("may affect usage classification", html, StringComparison.Ordinal);
        // Nothing is "other" when nothing qualifies, and the marker is not explained where none appears.
        Assert.Contains("<summary>What was not fully analysed</summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("other files not fully analysed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"usage-guide-note\"", html, StringComparison.Ordinal);
    }

    // ---- 11. Model isolation ---------------------------------------------------------------------

    /// <summary>
    /// Built as a real two-model project rather than a hand-assembled inventory, so the scoping is
    /// proven end to end: only one model declares a function, so only that model may be qualified.
    /// </summary>
    [Fact]
    public void LimitationsAndQualifiedCountsDoNotLeakBetweenModels()
    {
        var files = new List<ProjectFileContent> { InMemoryFile("Two.pbip", "{}") };
        files.AddRange(ModelFiles("WithFunctions", "function TotalOf = () => SUM(Sales[Amount])\n"));
        files.AddRange(ModelFiles("Plain", functions: null));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));
        var coverage = AnalysisCoveragePresentation.Build(inventory);

        // Only the model that declares a function has anything unanalysed, so it is the only model with
        // a coverage block at all. The other model is absent rather than shown with a nil explanation.
        var qualified = Assert.Single(coverage.Models);
        Assert.Equal("WithFunctions", qualified.ModelName);
        Assert.Contains(qualified.QualifyingGroups, group => group.ConstructType == "function");
        Assert.All(
            qualified.QualifyingGroups.SelectMany(group => group.ArtifactPaths),
            path => Assert.StartsWith("WithFunctions", path, StringComparison.Ordinal));

        // Counts are scoped to the owning model, so the other model's objects are not swept in.
        Assert.Equal(
            inventory.SemanticObjectUsages.Count(usage =>
                usage.SemanticModel == "WithFunctions" &&
                usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation),
            qualified.QualifiedObjectCount);
        Assert.DoesNotContain(
            inventory.SemanticObjectUsages.Where(usage => usage.SemanticModel == "Plain"),
            usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);

        var html = HtmlReportRenderer.Render(inventory);
        Assert.Equal(1, Occurrences(html, "class=\"coverage-model\""));
    }

    // ---- 12. Encoding ------------------------------------------------------------------------------

    [Fact]
    public void ArtifactPathsAndReasonsAreHtmlEncoded()
    {
        var html = RenderWithUsages(
            [Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation)],
            [
                Limitation("function", ConstructSupportStates.PartiallyAnalyzed, ConstructDependencyImpacts.MayCreateDependencies,
                    "definition/<script>alert(1)</script>.tmdl",
                    "Reason with <b>markup</b> & an ampersand."),
            ]);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>markup</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp; an ampersand", html, StringComparison.Ordinal);
    }

    // ---- 13. Accessibility -------------------------------------------------------------------------

    [Fact]
    public void TheCoverageSectionAndConfidenceMarkerAreAccessible()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation(), Limitation("culture", ConstructSupportStates.NotYetAnalyzed,
                ConstructDependencyImpacts.NoKnownDependencyEffect, "definition/cultures/en-US.tmdl", "Cultures are not analysed.")]);

        Assert.Contains("aria-labelledby=\"analysis-coverage-heading\"", html, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"analysis-coverage-heading\" tabindex=\"-1\">Analysis coverage</h2>", html, StringComparison.Ordinal);
        // The disclosure for harmless limitations is a native details element, so it is keyboard operable.
        Assert.Contains("<details class=\"coverage-other\"><summary>", html, StringComparison.Ordinal);
        // The marker's meaning does not depend on colour or on hovering.
        Assert.Contains("<span class=\"visually-hidden\">", html, StringComparison.Ordinal);
        Assert.Contains("classification qualified by analysis limitations in this model", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Qualification is context about the evidence, not a defect in the object, so it must not borrow
    /// the report's error or warning treatment.
    /// </summary>
    [Fact]
    public void QualificationIsNotPresentedAsAFinding()
    {
        var html = RenderWithUsages(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            limitations: [QualifyingLimitation()]);

        var section = Between(html, "id=\"analysis-coverage\"", "id=\"semantic-usage\"");
        foreach (var alarm in new[] { "badge-error", "badge-warning", "metric-error", "Unsafe", "unreliable", "Invalid analysis" })
        {
            Assert.DoesNotContain(alarm, section, StringComparison.Ordinal);
        }
    }

    // ---- 14. No regression to existing report content ---------------------------------------------

    [Fact]
    public void ExistingReportContentIsUnaffected()
    {
        var html = RenderFixture("desktop-semantic-constructs");

        Assert.Contains("id=\"summary\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"findings\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"semantic-usage\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"semantic-table\"", html, StringComparison.Ordinal);
        Assert.Contains("How usage classification works", html, StringComparison.Ordinal);
        Assert.Contains("Things PBI Assure cannot always detect", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The usage guide explains five states. Adding a confidence marker must not read as a sixth.
    /// </summary>
    [Fact]
    public void TheUsageGuideStillDescribesFiveStatesAndSeparatesConfidence()
    {
        var html = RenderFixture("desktop-semantic-constructs");

        Assert.Contains("5 statuses explained", html, StringComparison.Ordinal);
        Assert.Contains("not a sixth status", html, StringComparison.Ordinal);
    }

    // ---- Real Desktop fixtures ---------------------------------------------------------------------

    /// <summary>
    /// The fixture that makes the noise problem real: one limitation qualifies most of the model. Counts
    /// are measured from the inventory rather than hardcoded, so this cannot silently drift into
    /// asserting a stale number, while still failing if the rendered figures disagree with the domain.
    /// </summary>
    [Fact]
    public void TheDesktopConstructsFixtureRendersOneQualifyingCauseForManyObjects()
    {
        var inventory = ScanFixture("desktop-semantic-constructs");
        var html = HtmlReportRenderer.Render(inventory);

        var objectCount = inventory.SemanticObjectUsages.Count;
        var qualifiedCount = inventory.SemanticObjectUsages.Count(usage =>
            usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);
        Assert.Equal(27, objectCount);
        Assert.Equal(21, qualifiedCount);

        var qualifying = inventory.AnalysisLimitations
            .Where(limitation => limitation.DependencyImpact != ConstructDependencyImpacts.NoKnownDependencyEffect)
            .ToArray();
        var soleCause = Assert.Single(qualifying);
        Assert.Equal("function", soleCause.ConstructType);

        Assert.Contains("1 analysis limitation may affect usage classification", html, StringComparison.Ordinal);
        Assert.Contains($"{qualifiedCount} of {objectCount} object classifications are qualified", html, StringComparison.Ordinal);
        Assert.Contains("DAX user-defined functions", html, StringComparison.Ordinal);
        Assert.Contains("Partially analysed", html, StringComparison.Ordinal);
        Assert.Equal(qualifiedCount, Occurrences(html, "class=\"confidence-flag\""));
        // The explanation appears once, not once per affected object.
        Assert.Equal(1, Occurrences(html, soleCause.Reason));
        // The six harmless limitations are disclosed without competing for attention.
        Assert.Contains("<details class=\"coverage-other\"><summary>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDesktopUdfFixtureRendersItsQualifiedClassifications()
    {
        var inventory = ScanFixture("desktop-udf-references");
        var html = HtmlReportRenderer.Render(inventory);

        var qualified = inventory.SemanticObjectUsages
            .Where(usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation)
            .ToArray();
        Assert.Equal(3, qualified.Length);
        // Both absence states occur here, so both must carry the marker.
        Assert.Contains(qualified, usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.Contains(qualified, usage => usage.UsageState == SemanticUsageStates.UsedOnlyByUnusedBranch);

        Assert.Equal(qualified.Length, Occurrences(html, "class=\"confidence-flag\""));
        Assert.Contains("3 of 3 object classifications are qualified", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A model whose only unanalysed files are the always-present ones. Coverage is still disclosed, but
    /// nothing is qualified — the case that proves the summary is driven by impact rather than by the
    /// mere existence of a limitation.
    /// </summary>
    [Fact]
    public void AFixtureWithOnlyHarmlessLimitationsQualifiesNothing()
    {
        var inventory = ScanFixture("grouped-tab-order");
        var html = HtmlReportRenderer.Render(inventory);

        Assert.NotEmpty(inventory.AnalysisLimitations);
        Assert.DoesNotContain(inventory.SemanticObjectUsages, usage =>
            usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);

        Assert.Contains("id=\"analysis-coverage\"", html, StringComparison.Ordinal);
        Assert.Contains("None of them affect usage classification", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"usage-guide-note\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards the boundary the design rests on: the renderer reports the confidence the domain computed
    /// and never re-derives it. If the qualifier's rule changed, the rendered marker count would follow
    /// automatically rather than needing a matching renderer edit.
    /// </summary>
    [Fact]
    public void TheRendererDoesNotReimplementTheQualifierRule()
    {
        var source = System.IO.File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "PbiAssure.Reporting", "AnalysisCoveragePresentation.cs"));

        Assert.DoesNotContain("ApparentlyUnused", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UsedOnlyByUnusedBranch", source, StringComparison.Ordinal);
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static string RenderWithUsages(SemanticObjectUsage usage, AnalysisLimitation[] limitations) =>
        RenderWithUsages([usage], limitations);

    /// <summary>
    /// Renders a real scanned project with its usages and limitations replaced, so a combination the
    /// current registry cannot produce can still be rendered without changing any semantic rule.
    /// </summary>
    private static string RenderWithUsages(
        SemanticObjectUsage[] usages,
        AnalysisLimitation[] limitations)
    {
        var files = new List<ProjectFileContent> { InMemoryFile("Synthetic.pbip", "{}") };
        files.AddRange(ModelFiles(ModelName, functions: null, usages));
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));

        return HtmlReportRenderer.Render(inventory with
        {
            SemanticObjectUsages = usages,
            AnalysisLimitations = limitations,
        });
    }

    private const string ModelName = "Synthetic";

    private static SemanticObjectUsage Usage(string table, string name, string state, string confidence) =>
        new(ModelName, table, name, SemanticObjectTypes.Column, null, [], state)
        {
            ClassificationConfidence = confidence,
        };

    private static AnalysisLimitation QualifyingLimitation() => Limitation(
        "function",
        ConstructSupportStates.PartiallyAnalyzed,
        ConstructDependencyImpacts.MayCreateDependencies,
        "definition/functions.tmdl",
        "DAX user-defined function definitions are analysed. Where a function is called from is not.");

    private static AnalysisLimitation Limitation(
        string constructType,
        string supportState,
        string dependencyImpact,
        string artifactPath,
        string reason) =>
        new(
            LimitationId: $"PBI-LIMIT-{constructType.ToUpperInvariant()}",
            Cause: AnalysisLimitationCauses.ConstructNotSupported,
            SupportState: supportState,
            ConstructType: constructType,
            Scope: AnalysisLimitationScopes.SemanticModel,
            SemanticModel: ModelName,
            Table: null,
            ObjectName: null,
            ArtifactPath: $"{ModelName}.SemanticModel/{artifactPath}",
            EvidencePath: AnalysisLimitation.WholeFileEvidence,
            DependencyImpact: dependencyImpact,
            Concerns: [AnalysisConcerns.Dependency],
            Reason: reason);

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        string? functions,
        IReadOnlyList<SemanticObjectUsage>? columns = null)
    {
        var table = "table Sales\n" + string.Concat((columns ?? [])
            .Select(column => $"\n\tcolumn {column.ObjectName}\n\t\tdataType: int64\n\t\tsourceColumn: {column.ObjectName}\n"));
        yield return InMemoryFile($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return InMemoryFile($"{modelName}.SemanticModel/definition/tables/Sales.tmdl",
            columns is null ? "table Sales\n\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" : table);
        if (functions is not null)
        {
            yield return InMemoryFile($"{modelName}.SemanticModel/definition/functions.tmdl", functions);
        }
    }

    private static ProjectFileContent InMemoryFile(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Between(string html, string start, string end)
    {
        var from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Expected to find '{start}' in the rendered report.");
        var to = html.IndexOf(end, from, StringComparison.Ordinal);
        return to < 0 ? html[from..] : html[from..to];
    }

    private static string RenderFixture(string name) => HtmlReportRenderer.Render(ScanFixture(name));

    private static ProjectInventory ScanFixture(string name) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", name));

    private static string RepositoryRoot()
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
