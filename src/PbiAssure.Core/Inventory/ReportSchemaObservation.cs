namespace PbiAssure.Core.Inventory;

/// <summary>
/// A report-side PBIR schema declaration encountered during a scan. This describes what PBI Assure has
/// verified about its own report-format coverage; it does not assess whether the user's report is valid.
/// </summary>
public sealed record ReportSchemaObservation(
    string ArtifactKind,
    string ExpectedSchemaFamily,
    string RelativePath,
    string? RawSchemaUri,
    string? SchemaFamily,
    string? SchemaVersion,
    string State,
    string? VerifiedBaselineVersion);

public static class ReportSchemaArtifactKinds
{
    public const string DefinitionProperties = "DefinitionProperties";

    public const string VersionMetadata = "VersionMetadata";

    public const string Report = "Report";

    public const string PagesMetadata = "PagesMetadata";

    public const string Page = "Page";

    public const string VisualContainer = "VisualContainer";

    public const string VisualContainerMobileState = "VisualContainerMobileState";

    public const string BookmarksMetadata = "BookmarksMetadata";

    public const string Bookmark = "Bookmark";

    public const string ReportExtension = "ReportExtension";
}

public static class ReportSchemaObservationStates
{
    public const string VerifiedExact = "VerifiedExact";

    public const string RecognisedUnverifiedVersion = "RecognisedUnverifiedVersion";

    public const string UnknownFamily = "UnknownFamily";

    public const string MetadataMissing = "MetadataMissing";

    public const string MetadataMalformed = "MetadataMalformed";
}
