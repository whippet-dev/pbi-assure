using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

/// <summary>Established deterministic visual labels for presentation; never use these as identities.</summary>
internal static class VisualPresentation
{
    public static string DisplayName(VisualInventory visual)
    {
        if (visual.Accessibility.TitleIsVisible != false &&
            !visual.Accessibility.TitleTextIsDynamic &&
            !string.IsNullOrWhiteSpace(visual.Accessibility.TitleText))
        {
            return visual.Accessibility.TitleText;
        }

        if (!visual.OnCanvasTextIsDynamic && IsUsefulVisualText(visual.OnCanvasText))
        {
            return visual.OnCanvasText!;
        }

        return HumanizeVisualType(visual.VisualType);
    }

    private static bool IsUsefulVisualText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Count(char.IsLetterOrDigit) >= 2;

    private static string HumanizeVisualType(string? visualType)
    {
        if (string.IsNullOrWhiteSpace(visualType))
        {
            return "Unknown visual type";
        }

        return visualType switch
        {
            "barChart" => "Bar chart",
            "card" => "Card",
            "columnChart" => "Column chart",
            "pivotTable" => "Matrix",
            "slicer" => "Slicer",
            "tableEx" => "Table",
            _ => HumanizeIdentifier(visualType),
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        var words = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '_' or '-')
            {
                if (words.Length > 0 && words[^1] != ' ') words.Append(' ');
                continue;
            }

            if (index > 0 && char.IsUpper(character) && char.IsLower(value[index - 1])) words.Append(' ');
            words.Append(words.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return words.ToString();
    }
}
