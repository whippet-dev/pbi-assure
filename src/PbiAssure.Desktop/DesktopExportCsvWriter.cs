using System.Text;
using System.IO;
using PbiAssure.Core.Inventory;
using PbiAssure.Reporting.Exports;

namespace PbiAssure.Desktop;

/// <summary>Renders an approved export request and writes its UTF-8 BOM CSV unchanged.</summary>
public static class DesktopExportCsvWriter
{
    public static void Write(ProjectInventory inventory, ExportRequest request, string path)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var csv = ExportCsvRenderer.Render(inventory, request);
        File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
