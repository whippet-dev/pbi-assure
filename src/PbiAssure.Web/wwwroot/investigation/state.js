import { MODEL_AREA_DEFAULTS } from "./model-area-data.js";
import { SCOPE_DEFAULTS } from "./scope-data.js";
import { BRANCH_SIZE } from "./lineage-data.js";
export function createState() {
  return {
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
    history: [],
    cursor: -1,
    page: null,
    report: null,
    reportVisuals: {},
    reportScroll: {},
    visualSearch: "",
    visualTab: "Uses",
    reportPageDetails: false,
    reportOrigin: null,
    reviewMode: "main",
    reviewCategory: "",
    reviewSeverity: "",
    reviewRequired: false,
    reviewSearch: "",
    reviewSelected: null,
    reviewReturn: false,
    reviewReport: "",
    reviewPage: "",
    reviewModel: "",
    reviewTab: "Review",
    reviewLimitOpen: false,
    reviewViews: {},
    reviewOrigin: null,
    queryModel: "",
    queryState: "",
    queryKind: "",
    querySearch: "",
    querySelected: null,
    queryTab: "Summary",
    queryHistory: [],
    queryCursor: -1,
    queryOrigin: null,
    lineageCentre: null,
    lineageSessions: {},
    lineageHistory: [],
    lineageCursor: -1,
    lineageModel: "",
    lineageSearch: "",
    lineageSearchLimit: BRANCH_SIZE,
    lineageOrigin: null,
    ...MODEL_AREA_DEFAULTS,
    ...SCOPE_DEFAULTS,
    coreOrigin: null,
    areaVisited: {},
    checkViews: {},
    workspaceScroll: {},
    source: "",
  };
}

// Return links are immutable checkpoints, not recursive copies of the application.
export function copyContextValue(key, value) {
  return key.endsWith("Origin") ? value : structuredClone(value);
}
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
  check: Object.keys(SCOPE_DEFAULTS),
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
  ],
  overview: [],
};
export function checkpoint(state, view = state.view) {
  const keys = [
    ...workspaceKeys[view],
    "coreOrigin",
    "lineageOrigin",
    "reportOrigin",
    "queryOrigin",
    "reviewOrigin",
    "reviewReturn",
  ];
  return {
    ...Object.fromEntries(keys.map((k) => [k, copyContextValue(k, state[k])])),
    workspaceScroll: {
      [view]: structuredClone(state.workspaceScroll[view] || []),
    },
  };
}
export function restoreContext(state, context) {
  const { workspaceScroll, ...values } = context;
  Object.assign(state, values);
  if (workspaceScroll) Object.assign(state.workspaceScroll, workspaceScroll);
}
