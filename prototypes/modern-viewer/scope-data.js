import { path, areaId } from "./model-area-data.js";
import { queryId } from "./query-data.js";
import { isAccessibility } from "./review-data.js";
export const SCOPE_DEFAULTS = {
  checkMode: "limits",
  checkArea: "",
  checkModel: "",
  checkSearch: "",
  checkSelected: null,
  checkTab: "Meaning",
};
export const areaLabel = {
  Model: "Model",
  Report: "Report",
  PowerQuery: "Power Query",
  Security: "Security",
};
export const constructLabel = (type) =>
  ({
    function: "DAX functions",
    modelDefinition: "Model-level definition file",
    perspective: "Perspective presentation",
    semanticReference: "Unresolved model reference",
    unrecognizedReportDefinitionFile: "Unrecognised report metadata",
    reportMeasure: "Report measure references",
    powerQueryExpression: "Power Query references",
    entityPartition: "Partition source",
    role: "Security role",
    table: "Table definition",
  })[type] || type;
export const limitArea = (l) =>
  l.DependencyImpact === "MayCreateQueryDependencies"
    ? "PowerQuery"
    : /role/i.test(l.ConstructType) || l.Concerns?.includes("Security")
      ? "Security"
      : l.Scope === "Report"
        ? "Report"
        : "Model";
export const supportLabel = (value) =>
  ({
    PartiallyAnalyzed: "Partially analysed",
    NotYetAnalyzed: "Not analysed by this version",
    Unrecognized: "Unrecognised construct",
    Analyzed: "Analysed aspects",
  })[value] || value;
export const confidenceMeaning = (l) =>
  ({
    MayCreateDependencies:
      "Dependencies may be missing, so an apparently unused result may be incomplete. Use each object’s Checks limited label when interpreting its result.",
    MayCreateQueryDependencies:
      "Query dependencies may be missing. This concerns Power Query lineage; it does not itself qualify semantic-object usage results.",
    NoKnownDependencyEffect:
      "No known effect on object dependencies is recorded for this construct. The stated analysis limitation still applies.",
    DependencyEffectUnknown:
      "The effect on dependencies has not been determined. Missing evidence should not be treated as proof of absence.",
    MayInvalidateExistingEvidence:
      "This construct may change how existing evidence should be interpreted. Read the recorded reason before relying on the result.",
  })[l.DependencyImpact] ||
  "Use the recorded reason to interpret this limitation within the saved analysis.";
export function scopeRows(data, state) {
  const search = state.checkSearch.trim().toLowerCase();
  return data.raw.AnalysisLimitations.map((limitation, index) => ({
    limitation,
    index,
  })).filter(
    ({ limitation: l }) =>
      (!state.checkArea || limitArea(l) === state.checkArea) &&
      (!state.checkModel || l.SemanticModel === state.checkModel) &&
      (!search ||
        [
          l.Reason,
          l.ConstructType,
          constructLabel(l.ConstructType),
          l.SemanticModel,
          l.Table,
          l.ObjectName,
          l.ArtifactPath,
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase()
          .includes(search)),
  );
}
export function scopeSummary(data) {
  const limited = data.objects.filter(
    (o) => o.ClassificationConfidence === "QualifiedByLimitation",
  );
  return {
    limited,
    limitations: data.raw.AnalysisLimitations,
    areaCounts: Object.fromEntries(
      Object.keys(areaLabel).map((area) => [
        area,
        data.raw.AnalysisLimitations.filter((l) => limitArea(l) === area)
          .length,
      ]),
    ),
  };
}
export function limitationTarget(data, l) {
  if (!l) return null;
  const model = data.raw.SemanticModels.find((m) => m.Name === l.SemanticModel);
  if (limitArea(l) === "PowerQuery") {
    const queries = (data.raw.PowerQueryUsages || []).filter(
      (q) =>
        q.SemanticModel === l.SemanticModel &&
        path(q.ArtifactPath) === path(l.ArtifactPath) &&
        (l.ObjectName
          ? q.QueryName === l.ObjectName
          : l.Table
            ? q.Table === l.Table
            : false),
    );
    if (queries.length === 1) {
      const q = queries[0];
      return {
        kind: "query",
        id: queryId(
          q.SemanticModel,
          q.QueryName,
          q.SourceKind,
          q.Table,
          q.Partition,
        ),
        model: q.SemanticModel,
        label: "Inspect Power Query",
      };
    }
    if (model)
      return {
        kind: "query-scope",
        model: model.Name,
        search: l.ObjectName || l.Table || "",
        label: "Browse Power Query context",
      };
  }
  if (l.Scope === "Report") {
    const reports = data.raw.Reports.filter((r) =>
      path(l.ArtifactPath).startsWith(path(r.RelativePath) + "/"),
    );
    if (reports.length !== 1) return null;
    const report = reports[0],
      pages = report.Pages.filter(
        (p) =>
          p.RelativePath &&
          path(l.ArtifactPath).startsWith(path(p.RelativePath) + "/"),
      );
    return {
      kind: "report",
      report: report.RelativePath,
      page: pages.length === 1 ? pages[0].Name : null,
      label: "Inspect report context",
    };
  }
  if (!model) return null;
  const roles = (model.Roles || []).filter(
    (role) =>
      !!role.RelativePath &&
      !!l.ArtifactPath &&
      path(role.RelativePath) === path(l.ArtifactPath) &&
      (!l.ObjectName || l.ObjectName === role.Name),
  );
  if (roles.length === 1)
    return {
      kind: "security",
      id: areaId(model.Name, roles[0].Name),
      model: model.Name,
      label: "Inspect security role",
    };
  const matches = data.objects.filter(
    (o) =>
      o.SemanticModel === model.Name &&
      o.Table === l.Table &&
      o.ObjectName === l.ObjectName,
  );
  if (matches.length === 1)
    return { kind: "object", id: matches[0].id, label: "Inspect model object" };
  return {
    kind: "model",
    model: model.Name,
    table: model.Tables.some((t) => t.Name === l.Table) ? l.Table : "",
    label: l.Table ? "Browse model table" : "Browse semantic model",
  };
}
export function availableEvidence(data) {
  const models = data.raw.SemanticModels,
    reports = data.raw.Reports,
    roles = models.flatMap((m) => m.Roles || []),
    queries = data.raw.PowerQueryUsages || [];
  return [
    [
      "Model definitions",
      `${models.length} models · ${models.reduce((n, m) => n + m.Tables.length, 0)} tables · ${data.objects.length} object usage results`,
      "Saved model inventory and scanner usage/confidence results.",
      "objects",
    ],
    [
      "Semantic dependencies",
      `${data.raw.SemanticDependencies.length} recorded dependencies`,
      "Definitions and model structure; unresolved references remain separate.",
      "lineage",
    ],
    [
      "Relationships",
      `${models.reduce((n, m) => n + (m.Relationships || []).length, 0)} definitions`,
      "Endpoints, cardinality, state and published inactive-relationship activation evidence.",
      "relationships",
    ],
    [
      "Security definitions",
      `${roles.length} roles · ${roles.reduce((n, r) => n + r.TablePermissions.filter((p) => p.FilterExpression.trim()).length, 0)} row filters`,
      "Saved row filters and explicit table/column permissions. Effective user access is not tested.",
      "security",
    ],
    [
      "Report metadata",
      `${reports.length} reports · ${reports.reduce((n, r) => n + r.Pages.length, 0)} pages · ${reports.reduce((n, r) => n + r.Pages.reduce((a, p) => a + p.Visuals.length, 0), 0)} visuals`,
      "Saved bindings, configuration and observations, without rendering or executing report interactions.",
      "pages",
    ],
    [
      "Power Query",
      `${queries.length} query records · ${queries.filter((q) => q.HasDynamicReferences).length} dynamic query-reference flags`,
      "Query dependencies, recognised connectors and bounded column-preparation evidence. Dynamic flags are separate from limitation records.",
      "queries",
    ],
    [
      "Findings & reviews",
      `${data.raw.Findings.filter((f) => !isAccessibility(f)).length} findings · ${data.raw.Findings.filter(isAccessibility).length} Accessibility observations`,
      "Severity, assessment type and evidence retained; Accessibility and Theme keep separate meanings.",
      "reviews",
    ],
  ];
}
