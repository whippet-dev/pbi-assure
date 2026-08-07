namespace PbiAssure.Core.Inventory;

public static class SemanticUsageStates
{
    public const string DirectlyUsed = "DirectlyUsed";

    public const string IndirectlyUsed = "IndirectlyUsed";

    public const string StructurallyRequired = "StructurallyRequired";

    public const string UsedOnlyByUnusedBranch = "UsedOnlyByUnusedBranch";

    public const string ApparentlyUnused = "ApparentlyUnused";
}
