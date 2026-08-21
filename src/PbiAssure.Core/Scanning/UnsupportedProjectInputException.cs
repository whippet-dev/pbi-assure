namespace PbiAssure.Core.Scanning;

/// <summary>
/// Thrown before analysis when a selected project contains a recognised local semantic-model format
/// that this version cannot safely analyse.
/// </summary>
public sealed class UnsupportedProjectInputException : Exception
{
    private UnsupportedProjectInputException(string message)
        : base(message)
    {
    }

    public static UnsupportedProjectInputException TmslSemanticModel(string semanticModelDirectory) =>
        new($"PBI Assure cannot safely analyse this project because its local semantic model '{semanticModelDirectory}' is stored in TMSL format (model.bim). " +
            "This version supports local PBIP semantic models stored in TMDL format. No assurance output was generated. " +
            "Keep a backup, enable 'Store semantic model using TMDL format' in Power BI Desktop, then choose Upgrade when saving. This conversion cannot be undone.");

    public static UnsupportedProjectInputException AmbiguousSemanticModelFormat(string semanticModelDirectory) =>
        new($"PBI Assure cannot safely analyse this project because its local semantic model '{semanticModelDirectory}' contains both TMSL (model.bim) and TMDL (definition/) files. " +
            "The semantic-model format is ambiguous, so no assurance output was generated.");
}
