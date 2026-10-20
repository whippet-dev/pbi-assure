import { MODEL_AREA_DEFAULTS } from "./model-area-data.js";
import { SCOPE_DEFAULTS } from "./scope-data.js";
import { BRANCH_SIZE } from "./lineage-data.js";
export const workspaceKeys = {
  objects: [
    "model",
    "table",
    "type",
    "usage",
    "search",
    "system",
    "limited",
    "sort",
    "tab",
    "selected",
    "collectionSelected",
    "history",
    "cursor",
  ],
  pages: [
    "report",
    "page",
    "reportVisuals",
    "reportScroll",
    "visualSearch",
    "visualTab",
    "reportPageDetails",
  ],
  queries: [
    "queryModel",
    "queryState",
    "queryKind",
    "querySearch",
    "querySelected",
    "queryTab",
    "queryHistory",
    "queryCursor",
  ],
  lineage: [
    "lineageCentre",
    "lineageSessions",
    "lineageHistory",
    "lineageCursor",
    "lineageModel",
    "lineageSearch",
    "lineageSearchLimit",
  ],
  relationships: Object.keys(MODEL_AREA_DEFAULTS).filter((k) =>
    k.startsWith("relationship"),
  ),
  security: Object.keys(MODEL_AREA_DEFAULTS).filter((k) =>
    k.startsWith("security"),
  ),
  check: [...Object.keys(SCOPE_DEFAULTS), "checkViews"],
  reviews: [
    "reviewMode",
    "reviewCategory",
    "reviewSeverity",
    "reviewRequired",
    "reviewSearch",
    "reviewSelected",
    "reviewReport",
    "reviewPage",
    "reviewModel",
    "reviewTab",
    "reviewLimitOpen",
    "reviewViews",
  ],
  overview: [],
};

// Explicit ownership; compatibility accessors keep templates simple without shared workspace fields.
export function createState() {
  const defaults = {
    view: "overview",
    model: "",
    table: "",
    type: "",
    usage: "",
    search: "",
    system: false,
    limited: false,
    sort: "name",
    tab: "Summary",
    selected: null,
    collectionSelected: null,
    history: [],
    cursor: -1,
    page: null,
    report: null,
    reportVisuals: {},
    reportScroll: {},
    visualSearch: "",
    visualTab: "Uses",
    reportPageDetails: false,
    reviewMode: "main",
    reviewCategory: "",
    reviewSeverity: "",
    reviewRequired: false,
    reviewSearch: "",
    reviewSelected: null,
    reviewReport: "",
    reviewPage: "",
    reviewModel: "",
    reviewTab: "Review",
    reviewLimitOpen: false,
    reviewViews: {},
    queryModel: "",
    queryState: "",
    queryKind: "",
    querySearch: "",
    querySelected: null,
    queryTab: "Summary",
    queryHistory: [],
    queryCursor: -1,
    lineageCentre: null,
    lineageSessions: {},
    lineageHistory: [],
    lineageCursor: -1,
    lineageModel: "",
    lineageSearch: "",
    lineageSearchLimit: BRANCH_SIZE,
    ...MODEL_AREA_DEFAULTS,
    ...SCOPE_DEFAULTS,
    checkViews: {},
    workspaceScroll: {},
    source: "",
  };
  const state = {
    view: defaults.view,
    source: defaults.source,
    workspaces: {},
    workspaceScroll: {},
    workspaceDetails: {},
    returnTrail: [],
  };
  for (const [view, keys] of Object.entries(workspaceKeys)) {
    state.workspaces[view] = Object.fromEntries(
      keys.map((key) => [key, structuredClone(defaults[key])]),
    );
    for (const key of keys)
      Object.defineProperty(state, key, {
        enumerable: true,
        get: () => state.workspaces[view][key],
        set: (value) => {
          state.workspaces[view][key] = value;
        },
      });
  }
  return state;
}
export function checkpoint(state, view = state.view) {
  return {
    view,
    values: structuredClone(state.workspaces[view]),
    scroll: structuredClone(state.workspaceScroll[view] || []),
    details: structuredClone(state.workspaceDetails[view] || null),
  };
}
export function restoreContext(state, context) {
  Object.assign(
    state.workspaces[context.view],
    structuredClone(context.values),
  );
  state.workspaceScroll[context.view] = structuredClone(context.scroll);
  state.workspaceDetails[context.view] = structuredClone(context.details);
}
export function beginVisit(state, destination, label) {
  // Starting an unrelated investigation does not inherit another area's return chain.
  if (
    state.returnTrail.length &&
    state.returnTrail.at(-1).destination !== state.view
  )
    state.returnTrail = [];
  const origin = checkpoint(state);
  state.returnTrail.push({
    origin,
    destination,
    before: destination === state.view ? null : checkpoint(state, destination),
    label,
  });
}
export function returnVisit(state, index = state.returnTrail.length - 1) {
  if (index < 0 || index >= state.returnTrail.length) return false;
  while (state.returnTrail.length > index) {
    const frame = state.returnTrail.pop();
    if (frame.before) restoreContext(state, frame.before);
    restoreContext(state, frame.origin);
    state.view = frame.origin.view;
  }
  return true;
}
export function unusedCheckpointIndex(state) {
  return state.returnTrail.findIndex(
    (f) =>
      f.origin.view === "objects" &&
      f.origin.values.usage === "ApparentlyUnused",
  );
}
