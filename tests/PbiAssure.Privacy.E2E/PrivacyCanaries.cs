namespace PbiAssure.Privacy.E2E;

internal static class PrivacyCanaries
{
    public const string ProjectName = "PBIASSURE_PRIVACY_PROJECT_7F3C2A";
    public const string ModelName = "PBIASSURE_CANARY_MODEL_7F3C2A";
    public const string PowerQueryValue = "PBIASSURE_CANARY_M_7F3C2A";
    public const string VisualTitle = "PBIASSURE_CANARY_VISUAL_7F3C2A";

    public static IReadOnlyList<string> All { get; } =
        [ProjectName, ModelName, PowerQueryValue, VisualTitle];
}
