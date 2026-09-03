using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PbiAssure.Core.Inventory;
using PbiAssure.Reporting.Exports;

namespace PbiAssure.Desktop;

public partial class ExportCsvWindow : Window
{
    private readonly ProjectInventory inventory;
    private readonly string projectDisplayName;
    private readonly HashSet<string> selectedColumnIds = new(StringComparer.Ordinal);
    private ExportPreset selectedPreset = ExportPreset.DataCatalogue;

    public ExportCsvWindow(ProjectInventory inventory, string projectDisplayName)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.projectDisplayName = projectDisplayName ?? throw new ArgumentNullException(nameof(projectDisplayName));

        InitializeComponent();
        DataCataloguePresetRadioButton.IsChecked = true;
        ResetToDefaults();
    }

    internal ExportRequest CreateRequest() => new(selectedPreset, selectedColumnIds.ToArray());

    private void DataCataloguePreset_Checked(object sender, RoutedEventArgs e) => SelectPreset(ExportPreset.DataCatalogue);

    private void UsageMappingPreset_Checked(object sender, RoutedEventArgs e) => SelectPreset(ExportPreset.UsageMapping);

    private void SelectPreset(ExportPreset preset)
    {
        selectedPreset = preset;
        ResetToDefaults();
    }

    private void SelectDefaults_Click(object sender, RoutedEventArgs e) => ResetToDefaults();

    private void ResetToDefaults()
    {
        selectedColumnIds.Clear();
        selectedColumnIds.UnionWith(ExportPresetCatalog.GetDefaultColumnIds(selectedPreset));
        PresetDescriptionTextBlock.Text = PresetDescription(selectedPreset);
        ValidationTextBlock.Text = string.Empty;
        RenderColumnChoices();
    }

    private void RenderColumnChoices()
    {
        ColumnsPanel.Children.Clear();
        foreach (var column in ExportPresetCatalog.GetAllowedColumns(selectedPreset))
        {
            var checkBox = new CheckBox
            {
                Content = column.Header,
                Tag = column.Id,
                IsChecked = selectedColumnIds.Contains(column.Id),
                Margin = new Thickness(0, 2, 0, 2),
            };
            checkBox.Checked += Column_Checked;
            checkBox.Unchecked += Column_Unchecked;
            ColumnsPanel.Children.Add(checkBox);
        }

        SaveCsvButton.IsEnabled = selectedColumnIds.Count > 0;
    }

    private void Column_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: string id })
        {
            selectedColumnIds.Add(id);
            ValidationTextBlock.Text = string.Empty;
            SaveCsvButton.IsEnabled = true;
        }
    }

    private void Column_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: string id })
        {
            selectedColumnIds.Remove(id);
            SaveCsvButton.IsEnabled = selectedColumnIds.Count > 0;
        }
    }

    private void SaveCsv_Click(object sender, RoutedEventArgs e)
    {
        if (selectedColumnIds.Count == 0)
        {
            ValidationTextBlock.Text = "Choose at least one column before saving.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save export CSV",
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = ExportCsvFileNames.Create(projectDisplayName, selectedPreset),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            DesktopExportCsvWriter.Write(inventory, CreateRequest(), dialog.FileName);
            SavedPath = dialog.FileName;
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            ValidationTextBlock.Text = "The selected columns are not available for this export. Select defaults and try again.";
        }
        catch (Exception)
        {
            ValidationTextBlock.Text = "The CSV could not be generated or saved.";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    public string? SavedPath { get; private set; }

    private static string PresetDescription(ExportPreset preset) => preset switch
    {
        ExportPreset.DataCatalogue => "One row per model column or measure, including usage state, user-facing evidence and report/page/visual counts.",
        ExportPreset.UsageMapping => "One row per logical direct report usage, showing where and how a model column or measure is used.",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported export preset."),
    };
}
