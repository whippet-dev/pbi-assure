namespace PbiAssure.Core.Inventory;

/// <summary>
/// A DAX user-defined function. Functions are model-scoped and unique within the model — they are not
/// owned by a table, even when their body references one.
///
/// A function is a definition, not active model behaviour: nothing in the model requires it to exist.
/// It is therefore a dependency node rather than a dependency root, and what it references becomes
/// reachable only when something reachable calls it.
/// </summary>
public sealed record SemanticFunctionInventory(
    string Name,
    IReadOnlyList<SemanticFunctionParameterInventory> Parameters,
    string Expression,
    string RelativePath)
{
    public int ParameterCount => Parameters.Count;
}

/// <summary>
/// A function parameter. The name is a local symbol inside the body: it is not a model object, and must
/// never resolve to a same-named column.
/// </summary>
public sealed record SemanticFunctionParameterInventory(
    string Name,
    string? TypeHint);
