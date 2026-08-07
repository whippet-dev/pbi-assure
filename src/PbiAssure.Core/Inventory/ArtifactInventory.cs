namespace PbiAssure.Core.Inventory;

public sealed record ArtifactInventory(
    string Kind,
    string Name,
    string RelativePath,
    int DefinitionFileCount);
