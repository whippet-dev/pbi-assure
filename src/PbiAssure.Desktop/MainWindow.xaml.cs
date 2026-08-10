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
        ProjectPathTextBox.Text = projectPath;
        OutputPathTextBox.Text = string.Empty;
        OpenReportButton.IsEnabled = false;
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
                var outputPlan = DefaultScanOutputPath.ResolvePlan(null, projectPath, DateTime.Now, OutputFormat.Html);
                await ScanOutputWriter.WriteAsync(outputPlan, HtmlReportRenderer.Render(inventory));
                return outputPlan;
            });

            latestReportPath = result.LatestPath ?? result.HistoricalPath;
            OutputPathTextBox.Text = Path.GetFullPath(latestReportPath);
            StatusTextBlock.Text = "Assurance report created successfully.";
            OpenReportButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            latestReportPath = null;
            OutputPathTextBox.Text = string.Empty;
            StatusTextBlock.Text = $"Could not run assurance: {exception.Message}";
        }
        finally
        {
            SetRunningState(false);
        }
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        var reportPath = latestReportPath;
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            StatusTextBlock.Text = "The latest report could not be found. Run assurance again.";
            OpenReportButton.IsEnabled = false;
            return;
        }

        Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
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
