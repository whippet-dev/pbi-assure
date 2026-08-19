namespace PbiAssure.Core.Inventory;

/// <summary>
/// Whether one node in a model's dependency graph is reachable from a report root or from a
/// model-structure root.
///
/// This is the evidence the classifier already computes on its way to a <see cref="SemanticUsageStates"/>
/// value, published rather than discarded. It exists because an incoming dependency edge and the
/// evidence supporting a classification are not the same thing: an uncalled function genuinely
/// references a column, yet is not why that column is <c>IndirectlyUsed</c>. Answering "why does this
/// object have <em>this</em> state?" needs to know which of its predecessors are themselves live.
///
/// Nodes that carry no <see cref="SemanticObjectUsage"/> row are included, because a live path can run
/// through one — a report measure or a DAX user-defined function sits on the graph without ever being a
/// user-facing model object. That is precisely why a rule based only on the states of public objects
/// cannot follow such a path.
///
/// Additive and presentation-neutral: it states reachability and draws no conclusion from it.
/// </summary>
public sealed record SemanticNodeReachability(
    string SemanticModel,
    string Table,
    string ObjectName,
    string ObjectType,
    string? HierarchyName,
    bool ReachableFromReport,
    bool ReachableFromModelStructure);
