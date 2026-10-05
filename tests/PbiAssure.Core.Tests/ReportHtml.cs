namespace PbiAssure.Core.Tests;

/// <summary>Helpers for tests that read the rendered interactive report.</summary>
internal static class ReportHtml
{
    /// <summary>
    /// The report without its lineage section. Lineage cards restate, per object, facts the rest of the
    /// report shows once — usage states, confidence markers, report roles — so a test counting how often
    /// the Semantic model or the visual cards show something counts in the report without them.
    /// The lineage section is the last section in the main content.
    /// </summary>
    public static string WithoutLineage(string html)
    {
        var start = html.IndexOf("<section id=\"lineage\"", StringComparison.Ordinal);
        if (start < 0)
        {
            return html;
        }

        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        return end < 0 ? html[..start] : html[..start] + html[end..];
    }

    /// <summary>The lineage section alone.</summary>
    public static string LineageSection(string html)
    {
        var start = html.IndexOf("<section id=\"lineage\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a lineage section in the rendered report.");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        return end < 0 ? html[start..] : html[start..end];
    }
}
