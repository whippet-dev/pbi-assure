// Shared presentation primitives; all inventory text is escaped at the boundary.
export const escape = (value) =>
  String(value ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
const icons = {
  anchor: '<path d="m8 3 8 0-1 6 4 4H5l4-4-1-6m4 10v8"/>',
  relationships:
    '<rect x="2" y="5" width="7" height="14" rx="1"/><rect x="15" y="5" width="7" height="14" rx="1"/><path d="M9 12h6M5 9h1m12 6h1"/>',
  security:
    '<path d="M12 3 3 7v6c0 5 9 8 9 8s9-3 9-8V7z"/><rect x="8" y="10" width="8" height="6" rx="1"/><path d="M10 10V8a2 2 0 0 1 4 0v2"/>',
  lineage:
    '<circle cx="12" cy="12" r="3"/><path d="M9 12H3m12 0h6M12 9V3m0 12v6M3 9v6m18-6v6M9 3h6M9 21h6"/>',
  queries: '<path d="M4 4h6v6H4zM14 14h6v6h-6zM7 10v7h7M10 7h7v7"/>',
  overview: '<path d="m3 10 9-7 9 7v11h-6v-7H9v7H3z"/>',
  reviews: '<path d="M5 3h14v18H5zM9 8h6M9 12h6M9 16h3"/>',
  objects:
    '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
  pages:
    '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18M9 9v11"/>',
  check:
    '<path d="m7 12 3 3 7-7"/><path d="M12 3 3 7v6c0 5 9 8 9 8s9-3 9-8V7z"/>',
  search: '<circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/>',
  arrow: '<path d="m9 5 7 7-7 7"/>',
  back: '<path d="m15 5-7 7 7 7"/>',
  folder: '<path d="M3 7V5h6l2 2h10v13H3z"/>',
  open: '<path d="M14 3h7v7m0-7L10 14M10 3H3v18h18v-7"/>',
};
export const icon = (name) =>
  `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[name] || icons.objects}</svg>`;
const objectIcons = {
  Measure:
    '<path d="M16 4h-3c-2 0-3 2-3 4v8c0 3-1 4-4 4M6 10h10"/><path d="m15 15 5 5m0-5-5 5"/>',
  Column:
    '<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M4 8h16M9 8v13m6-13v13"/><path d="M10 9h4v11h-4z"/>',
  Table:
    '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18M9 9v11M3 14h18"/>',
  Function: '<path d="m8 5-5 7 5 7m8-14 5 7-5 7m-3-14-2 14"/>',
  HierarchyLevel:
    '<path d="M5 3v14h5m-5-9h9"/><rect x="14" y="5" width="6" height="6" rx="1"/><rect x="10" y="14" width="10" height="6" rx="1"/>',
  CalculationItem:
    '<rect x="5" y="3" width="14" height="18" rx="2"/><path d="M8 7h8m-8 5h3m-3 4h3m4-4h1m-1 4h1"/>',
};
export const glyph = (type) =>
  `<span class="object-icon" aria-hidden="true"><svg viewBox="0 0 24 24">${objectIcons[type === "ReportMeasure" ? "Measure" : type] || icons[type === "Relationship" ? "relationships" : type === "Role" ? "security" : "objects"]}</svg></span>`;
