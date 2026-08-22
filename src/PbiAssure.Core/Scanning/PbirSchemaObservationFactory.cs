using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Classifies the schema declaration on a PBIR artifact without affecting how that artifact is parsed.
/// The baseline records PBI Assure's Desktop-fixture evidence, not Microsoft's support lifecycle.
/// </summary>
internal static class PbirSchemaObservationFactory
{
    private const string MicrosoftSchemaHost = "developer.microsoft.com";
    private static readonly string[] UriPrefix = ["json-schemas", "fabric", "item", "report"];

    private static readonly Dictionary<string, SchemaBaseline> Baselines =
        new Dictionary<string, SchemaBaseline>(StringComparer.Ordinal)
        {
            [ReportSchemaArtifactKinds.DefinitionProperties] = new("definitionProperties", "2.0.0"),
            [ReportSchemaArtifactKinds.VersionMetadata] = new("versionMetadata", "1.0.0"),
            [ReportSchemaArtifactKinds.Report] = new("report", "3.3.0"),
            [ReportSchemaArtifactKinds.PagesMetadata] = new("pagesMetadata", "1.1.0"),
            [ReportSchemaArtifactKinds.Page] = new("page", "2.1.0"),
            // Both versions below are retained by committed, Desktop-authored PBIP fixtures. Exact
            // evidence is deliberately version-specific; a different version is still unverified.
            [ReportSchemaArtifactKinds.VisualContainer] = new("visualContainer", "2.11.0", "2.12.0"),
            // The mobile-only title in the Desktop-authored mobile semantic-reference fixture
            // establishes this exact persisted state schema. Its layout metadata remains inventory-free.
            [ReportSchemaArtifactKinds.VisualContainerMobileState] = new("visualContainerMobileState", "2.7.0"),
            // Desktop-authored bookmark evidence fixtures establish these exact versions. A different
            // version remains recognised but unverified; no semantic-version compatibility is assumed.
            [ReportSchemaArtifactKinds.BookmarksMetadata] = new("bookmarksMetadata", "1.0.0"),
            [ReportSchemaArtifactKinds.Bookmark] = new("bookmark", "2.1.0"),
            [ReportSchemaArtifactKinds.ReportExtension] = new("reportExtension"),
        };

    public static ReportSchemaObservation Create(string artifactKind, string relativePath, JsonElement root)
    {
        if (!Baselines.TryGetValue(artifactKind, out var baseline))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactKind), artifactKind, "Unknown report schema artifact kind.");
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("$schema", out var schemaElement))
        {
            return Observation(
                artifactKind, baseline, relativePath, null, null, null,
                ReportSchemaObservationStates.MetadataMissing);
        }

        if (schemaElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(schemaElement.GetString()))
        {
            return Observation(
                artifactKind, baseline, relativePath, schemaElement.GetRawText(), null, null,
                ReportSchemaObservationStates.MetadataMalformed);
        }

        var rawSchemaUri = schemaElement.GetString()!;
        if (!TryParseCanonicalUri(rawSchemaUri, out var family, out var version))
        {
            return Observation(
                artifactKind, baseline, relativePath, rawSchemaUri, null, null,
                ReportSchemaObservationStates.MetadataMalformed);
        }

        if (!string.Equals(family, baseline.Family, StringComparison.Ordinal))
        {
            return Observation(
                artifactKind, baseline, relativePath, rawSchemaUri, family, version.ToString(3),
                ReportSchemaObservationStates.UnknownFamily);
        }

        var verifiedVersion = baseline.VerifiedVersions.FirstOrDefault(candidate =>
            Version.Parse(candidate).Equals(version));
        var state = verifiedVersion is not null
            ? ReportSchemaObservationStates.VerifiedExact
            : ReportSchemaObservationStates.RecognisedUnverifiedVersion;
        return Observation(
            artifactKind,
            baseline,
            relativePath,
            rawSchemaUri,
            family,
            version.ToString(3),
            state,
            verifiedVersion ?? baseline.VerifiedVersions.FirstOrDefault());
    }

    private static ReportSchemaObservation Observation(
        string artifactKind,
        SchemaBaseline baseline,
        string relativePath,
        string? rawSchemaUri,
        string? family,
        string? version,
        string state,
        string? verifiedBaselineVersion = null) =>
        new(
            ArtifactKind: artifactKind,
            ExpectedSchemaFamily: baseline.Family,
            RelativePath: relativePath,
            RawSchemaUri: rawSchemaUri,
            SchemaFamily: family,
            SchemaVersion: version,
            State: state,
            VerifiedBaselineVersion: verifiedBaselineVersion ?? baseline.VerifiedVersions.FirstOrDefault());

    private static bool TryParseCanonicalUri(string rawSchemaUri, out string family, out Version version)
    {
        family = string.Empty;
        version = new Version();
        if (!Uri.TryCreate(rawSchemaUri, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, MicrosoftSchemaHost, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is not (7 or 8) ||
            !segments.Take(UriPrefix.Length).SequenceEqual(UriPrefix, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = UriPrefix.Length;
        if (segments.Length == 8)
        {
            if (!string.Equals(segments[index], "definition", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            index++;
        }

        if (!string.Equals(segments[index + 2], "schema.json", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(segments[index + 1], out var parsedVersion) ||
            parsedVersion.Build < 0 ||
            parsedVersion.Revision >= 0)
        {
            return false;
        }

        family = segments[index];
        version = parsedVersion;
        return !string.IsNullOrWhiteSpace(family);
    }

    private sealed record SchemaBaseline(string Family, params string[] VerifiedVersions);
}
