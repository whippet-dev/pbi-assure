# Prepare a Power BI project for PBI Assure

PBI Assure provides full assurance for a Power BI Project (PBIP) that uses the Power BI enhanced report format (PBIR) and Tabular Model Definition Language (TMDL) semantic model format. These formats store the report and model as structured project files that PBI Assure can analyse locally.

## Start with a PBIX

1. Open the PBIX in Power BI Desktop.
2. Go to **File > Options and settings > Options > Preview features**.
3. Enable **Power BI Project (.pbip) save option** if it is shown in your version of Desktop.
4. Enable **Store reports using enhanced metadata format (PBIR)**.
5. Enable **Store semantic model using TMDL format**.
6. Save the report as a Power BI Project and select the resulting project root folder in PBI Assure.

Power BI Desktop preview features and their labels can change. If the options differ in your version, use the Microsoft guidance below to find the current setting.

## Select the project root folder

Choose the folder containing the `.pbip` file and its report and semantic-model folders:

```text
MyReport/
├── MyReport.pbip
├── MyReport.Report/
└── MyReport.SemanticModel/
```

Do not select the `.Report` or `.SemanticModel` folder by itself.

The supported local semantic-model layout includes both `definition.pbism` and a `definition/` folder
containing TMDL files. A local semantic model stored as `model.bim` uses TMSL and is not supported yet.
PBI Assure stops without generating output rather than producing an incomplete result. Keep a backup,
enable **Store semantic model using TMDL format**, then choose **Upgrade** when Power BI Desktop prompts
you to save the conversion. That conversion cannot be undone.

## What PBI Assure analyses

PBI Assure reads the structured report, semantic-model, DAX, relationship, and Power Query metadata in the selected project. It processes selected files locally in your browser; project files and analysis results are not uploaded to PBI Assure.

A standard PBIX is not the full-assurance input format. PBIR-enabled PBIX can expose report content, but does not provide the complete structured semantic-model and Power Query metadata used by PBI Assure's full analysis. Save as PBIP with PBIR and a local TMDL semantic model to use the complete workflow.

## Microsoft guidance

- [Power BI Desktop projects (PBIP)](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-overview)
- [Power BI enhanced report format (PBIR)](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report)
- [Power BI Desktop project semantic model folder and TMDL](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-dataset)
