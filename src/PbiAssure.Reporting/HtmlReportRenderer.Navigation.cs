using System.Text;

namespace PbiAssure.Reporting;

public static partial class HtmlReportRenderer
{
    private static void AppendWorkspaceStart(
        StringBuilder html, string id, string label, string defaultView, string description,
        (string Target, string Label)[] views)
    {
        html.Append("    <div id=\"").Append(Encode(id)).Append("\" class=\"review-workspace\" data-workspace-group=\"")
            .Append(Encode(id)).Append("\" data-context-route=\"").Append(Encode(defaultView)).AppendLine("\">");
        html.AppendLine("      <header class=\"workspace-heading\">");
        html.Append("        <p class=\"workspace-title\">").Append(Encode(label)).AppendLine("</p>");
        html.Append("        <p class=\"secondary\">").Append(Encode(description)).AppendLine("</p>");
        html.Append("        <nav class=\"object-view-nav\" aria-label=\"").Append(Encode(label)).AppendLine(" views\">");
        foreach (var (target, viewLabel) in views)
        {
            html.Append("          <a href=\"#").Append(Encode(target)).Append("\" data-workspace-target=\"")
                .Append(Encode(target)).Append("\">").Append(Encode(viewLabel)).AppendLine("</a>");
        }

        html.AppendLine("        </nav>");
        html.AppendLine("      </header>");
    }
}
