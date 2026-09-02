using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting.Exports;

/// <summary>Routes a fixed export request without making Core depend on Reporting.</summary>
public static class ExportCsvRenderer
{
    public static string Render(ProjectInventory inventory, ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(request);

        return request.Preset switch
        {
            ExportPreset.DataCatalogue => DataCatalogueCsvRenderer.Render(inventory, request),
            ExportPreset.UsageMapping => UsageMappingCsvRenderer.Render(inventory, request),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Preset, "Unsupported export preset."),
        };
    }
}
