namespace PbiAssure.Core.Inventory;

/// <summary>
/// Incremental-refresh policy metadata persisted on a semantic-model table.
///
/// These are authored settings only. They do not establish query folding, successful service refreshes,
/// generated service partitions or refresh efficiency.
/// </summary>
public sealed record SemanticRefreshPolicyInventory(
    string? PolicyType,
    string? Mode,
    string? RollingWindowGranularity,
    int? RollingWindowPeriods,
    string? IncrementalGranularity,
    int? IncrementalPeriods,
    int? IncrementalPeriodsOffset,
    string? PollingExpression,
    string? SourceExpression,
    string? ChangeDetectionColumn);
