using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PbiAssure.Cli;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Desktop;

public partial class MainWindow : Window
{
    private string? projectPath;
    private string? latestReportPath;
    private string? latestSemanticUsageCsvPath;
    private string? outputFolderPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder containing a PBIP or PBIR project"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        projectPath = dialog.FolderName;
        latestReportPath = null;
        latestSemanticUsageCsvPath = null;
        outputFolderPath = null;
        ProjectPathTextBox.Text = projectPath;
        OutputPathTextBox.Text = string.Empty;
        CsvPathTextBox.Text = string.Empty;
        OutputFolderTextBox.Text = string.Empty;
        OpenReportButton.IsEnabled = false;
        OpenSemanticCsvButton.IsEnabled = false;
        OpenOutputFolderButton.IsEnabled = false;
        RunAssuranceButton.IsEnabled = true;
        StatusTextBlock.Text = "Ready to run assurance.";
    }

    private async void RunAssurance_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        if (!ContainsPowerBiProject(projectPath))
        {
            StatusTextBlock.Text = "That folder does not contain a PBIP or PBIR project. Select the project folder and try again.";
            return;
        }

        SetRunningState(true);
        StatusTextBlock.Text = "Scanning project metadata and generating the HTML report…";

        try
        {
            var result = await Task.Run(async () =>
            {
                var inventory = ProjectScanner.Scan(projectPath);
                var outputResult = await AssuranceOutputWriter.WriteDefaultOutputsAsync(inventory, projectPath, DateTime.Now);
                return (inventory, outputResult);
            });

            latestReportPath = result.outputResult.HtmlOutput.LatestPath ?? result.outputResult.HtmlOutput.HistoricalPath;
            OutputPathTextBox.Text = Path.GetFullPath(latestReportPath);
            outputFolderPath = Path.GetDirectoryName(latestReportPath);
            OpenReportButton.IsEnabled = true;
            OpenOutputFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(outputFolderPath);
            if (result.outputResult.SemanticUsageCsvOutput is not null)
            {
                latestSemanticUsageCsvPath = result.outputResult.SemanticUsageCsvOutput.LatestPath
                    ?? result.outputResult.SemanticUsageCsvOutput.HistoricalPath;
                CsvPathTextBox.Text = Path.GetFullPath(latestSemanticUsageCsvPath);
                OutputFolderTextBox.Text = Path.GetFullPath(outputFolderPath!);
                OpenSemanticCsvButton.IsEnabled = true;
                StatusTextBlock.Text = $"Assurance completed: {result.inventory.ErrorFindingCount} errors · {result.inventory.WarningFindingCount} warnings · {result.inventory.ReviewRequiredCount} reviews. HTML and semantic CSV created.";
            }
            else
            {
                latestSemanticUsageCsvPath = null;
                CsvPathTextBox.Text = string.Empty;
                OutputFolderTextBox.Text = outputFolderPath is null ? string.Empty : Path.GetFullPath(outputFolderPath);
                OpenSemanticCsvButton.IsEnabled = false;
                StatusTextBlock.Text = $"HTML report created, but the semantic CSV could not be created: {result.outputResult.SemanticUsageCsvError}";
            }
        }
        catch (Exception exception)
        {
            latestReportPath = null;
            latestSemanticUsageCsvPath = null;
            outputFolderPath = null;
            OutputPathTextBox.Text = string.Empty;
            CsvPathTextBox.Text = string.Empty;
            OutputFolderTextBox.Text = string.Empty;
            OpenReportButton.IsEnabled = false;
            OpenSemanticCsvButton.IsEnabled = false;
            OpenOutputFolderButton.IsEnabled = false;
            StatusTextBlock.Text = $"Could not run assurance: {exception.Message}";
        }
        finally
        {
            SetRunningState(false);
        }
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(latestReportPath, "report", OpenReportButton);
    }

    private void OpenSemanticCsv_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(latestSemanticUsageCsvPath, "semantic CSV", OpenSemanticCsvButton);
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(outputFolderPath) || !Directory.Exists(outputFolderPath))
        {
            StatusTextBlock.Text = "The output folder could not be found. Run assurance again.";
            OpenOutputFolderButton.IsEnabled = false;
            return;
        }

        Process.Start(new ProcessStartInfo(outputFolderPath) { UseShellExecute = true });
    }

    private void OpenFile(string? filePath, string description, System.Windows.Controls.Button button)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusTextBlock.Text = $"The latest {description} could not be found. Run assurance again.";
            button.IsEnabled = false;
            return;
        }

        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    private void SetRunningState(bool isRunning)
    {
        RunAssuranceButton.IsEnabled = !isRunning;
        SelectProjectFolderButton.IsEnabled = !isRunning;
        ProjectPathTextBox.IsEnabled = !isRunning;
    }

    private static bool ContainsPowerBiProject(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*.pbip", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(folderPath, "*.pbir", SearchOption.AllDirectories).Any();
    }
}
