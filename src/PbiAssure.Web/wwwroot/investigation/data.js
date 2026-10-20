// Presentation adapter only. Usage state and confidence always come from Core.
export const STATES = {
  DirectlyUsed: {
    label: "Directly used",
    tone: "direct",
    why: "Referenced directly in the analysed report metadata.",
  },
  IndirectlyUsed: {
    label: "Indirectly used",
    tone: "indirect",
    why: "Used by another object with recorded report use, directly or through further dependencies.",
  },
  StructurallyRequired: {
    label: "Structurally required",
    tone: "structural",
    why: "Required by the analysed model structure or its dependencies.",
  },
  UsedOnlyByUnusedBranch: {
    label: "Used only by unused branch",
    tone: "branch",
    why: "Other objects reference this object, but no use of that chain was found in the analysed reports or required model structure. Use outside this analysis may still exist.",
  },
  ApparentlyUnused: {
    label: "Apparently unused",
    tone: "unused",
    why: "No report usage or semantic-model requirement was found within the analysed scope.",
  },
};
export { projectSnapshot as adaptSnapshot } from "./snapshot-data.js";
export const objectId = (o) => o.Id || o.id;
export const edgeId = (e, side) => e[side + "Id"];
export const words = (value) =>
  String(value || "")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/\bDax\b/g, "DAX");
export function filterObjects(data, state) {
  const query = state.search.toLocaleLowerCase().trim();
  return data.objects
    .filter(
      (o) =>
        (!state.model || o.SemanticModel === state.model) &&
        (!state.table || o.Table === state.table) &&
        ((state.usage !== "ApparentlyUnused" && state.system) || !o.system) &&
        (!state.type || o.ObjectType === state.type) &&
        (!state.usage ||
          (state.usage === "ApparentlyUnused"
            ? o.InApparentlyUnusedReview
            : o.UsageState === state.usage)) &&
        (!state.limited ||
          o.ClassificationConfidence === "QualifiedByLimitation") &&
        (!query ||
          `${o.ObjectName} ${o.Table} ${o.SemanticModel} ${o.ObjectType}`
            .toLocaleLowerCase()
            .includes(query)),
    )
    .sort((a, b) =>
      state.sort === "usage"
        ? Object.keys(STATES).indexOf(a.UsageState) -
            Object.keys(STATES).indexOf(b.UsageState) ||
          a.ObjectName.localeCompare(b.ObjectName)
        : (a.ObjectType === "Measure" ? 0 : 1) -
            (b.ObjectType === "Measure" ? 0 : 1) ||
          a.ObjectName.localeCompare(b.ObjectName),
    );
}
export function supportingEdges(data, object) {
  return data.raw.SemanticDependencies.filter(
    (e) => e.Id === object.ReasonDependencyId,
  );
}
