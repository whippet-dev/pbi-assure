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
export const glyph = (type) =>
  `<span class="object-icon ${type === "Measure" || type === "ReportMeasure" ? "measure" : ""}" aria-hidden="true">${type === "Measure" || type === "ReportMeasure" ? "ƒ" : type === "Column" ? "▥" : "⋮"}</span>`;
