using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

#region ImportFileType Definitions

public static class ImportFileTypeDefinitions
{
    public static class Categories
    {
        public const string Spreadsheet = "Spreadsheet";
        public const string Structured = "Structured";
        public const string Text = "Text";

        [Display(Name = "Spreadsheet Formats", Description = "Workbook and tabular spreadsheet formats.")]
        public const string SpreadsheetDisplay = Spreadsheet;

        [Display(Name = "Structured Formats", Description = "Hierarchical and schema-based document formats.")]
        public const string StructuredDisplay = Structured;

        [Display(Name = "Plain Text Formats", Description = "Delimited text and CSV formats.")]
        public const string TextDisplay = Text;
    }

    public static class Filters
    {
        public const string Tabular = "Tabular";
        public const string Hierarchical = "Hierarchical";

        [Display(Name = "Tabular Data", Description = "Rows and columns based data.")]
        public const string TabularDisplay = Tabular;

        [Display(Name = "Hierarchical Data", Description = "Nested object tree structures.")]
        public const string HierarchicalDisplay = Hierarchical;
    }
}

/// <summary>
/// Supported import file formats for data ingestion.
/// </summary>
public enum ImportFileType
{
    [Display(Name = "Project Tasks (Excel .xlsx)", Description = "Project tasks workbook with Tasks and Comments sheets.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Spreadsheet)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Tabular)]
    TaskExcel = 1,

    [Display(Name = "Project Tasks (JSON .json)", Description = "Project tasks JSON structured document.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Structured)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Hierarchical)]
    TaskJson = 2,

    [Display(Name = "Project Tasks (XML .xml)", Description = "Project tasks XML document.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Structured)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Hierarchical)]
    TaskXml = 3,

    [Display(Name = "Project Tasks (CSV .csv)", Description = "Project tasks comma-delimited plain text file.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Text)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Tabular)]
    TaskCsv = 4,

    [Display(Name = "Catalog Products (Excel .xlsx)", Description = "Catalog products workbook with Items and Inventory.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Spreadsheet)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Tabular)]
    ProductExcel = 5,

    [Display(Name = "Catalog Products (JSON .json)", Description = "Catalog products JSON document.")]
    [DataListCategory(ImportFileTypeDefinitions.Categories.Structured)]
    [DataListFilterCriterias(ImportFileTypeDefinitions.Filters.Hierarchical)]
    ProductJson = 6
}

#endregion

#region ImportStatus Definitions

/// <summary>
/// Processing lifecycle state of an imported file.
/// Follows the linear 3-stage pipeline: Uploaded -> Validating -> Staging -> Processing -> Completed.
/// </summary>
public enum ImportStatus
{
    [Display(Name = "Uploaded", Description = "Raw file uploaded, encrypted, and stored in blob storage.")]
    Uploaded = 1,

    [Display(Name = "Validating", Description = "Background worker is validating file structure and schema.")]
    Validating = 2,

    [Display(Name = "Validation Succeeded", Description = "File passed all schema and invariant validations.")]
    ValidationSuccess = 3,

    [Display(Name = "Validation Failed", Description = "File failed schema or data invariant validation.")]
    ValidationFailed = 4,

    [Display(Name = "Staging", Description = "Writing validated records to staging tables.")]
    Staging = 5,

    [Display(Name = "Staging Succeeded", Description = "All records were successfully written to staging tables.")]
    StagingSuccess = 6,

    [Display(Name = "Staging Failed", Description = "Encountered an error while writing records to staging tables.")]
    StagingFailed = 7,

    [Display(Name = "Processing", Description = "Committing staged records into live domain tables.")]
    Processing = 8,

    [Display(Name = "Completed", Description = "Staged records successfully processed into live business entities.")]
    Completed = 9,

    [Display(Name = "Processing Failed", Description = "Encountered an error while committing staged records to domain tables.")]
    ProcessingFailed = 10
}

#endregion
