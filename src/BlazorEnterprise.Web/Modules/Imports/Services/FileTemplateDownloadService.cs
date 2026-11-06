using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using BlazorEnterprise.Jobs.FileImports.Configuration;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Modules.Imports.Services;

public sealed class FileTemplateDownloadService : IFileTemplateDownloadService
{
    private readonly IFileTemplateConfigRepository _configRepository;
    private readonly ILogger<FileTemplateDownloadService> _logger;

    private static readonly IReadOnlyList<ImportFileTypeMetadata> AvailableTypes = new List<ImportFileTypeMetadata>
    {
        new()
        {
            FileType = ImportFileTypeEnum.ProgramCalendarExcel,
            DisplayName = "Program Calendar (Excel)",
            Extension = ".xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AcceptAttribute = ".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Description = "Excel workbook containing a 'Calendar' worksheet with Program timelines, milestones, owner emails, and status.",
            SampleFileName = "ProgramCalendar_Template.xlsx"
        },
        new()
        {
            FileType = ImportFileTypeEnum.ProjectCalendarExcel,
            DisplayName = "Project Calendar (Excel)",
            Extension = ".xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AcceptAttribute = ".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Description = "Excel workbook containing a 'Calendar' worksheet with Project milestones, key events, and phase schedules.",
            SampleFileName = "ProjectCalendar_Template.xlsx"
        },
        new()
        {
            FileType = ImportFileTypeEnum.TaskExcel,
            DisplayName = "Tasks (Excel)",
            Extension = ".xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AcceptAttribute = ".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Description = "Excel workbook containing project tasks with title, priority, status, and assignee details.",
            SampleFileName = "Tasks_Template.xlsx"
        },
        new()
        {
            FileType = ImportFileTypeEnum.TaskCsv,
            DisplayName = "Tasks (CSV)",
            Extension = ".csv",
            ContentType = "text/csv",
            AcceptAttribute = ".csv,text/csv",
            Description = "Comma-delimited text file containing task records with standard header fields.",
            SampleFileName = "Tasks_Template.csv"
        },
        new()
        {
            FileType = ImportFileTypeEnum.TaskJson,
            DisplayName = "Tasks (JSON)",
            Extension = ".json",
            ContentType = "application/json",
            AcceptAttribute = ".json,application/json",
            Description = "JSON array containing task objects conforming to the task import specification.",
            SampleFileName = "Tasks_Template.json"
        },
        new()
        {
            FileType = ImportFileTypeEnum.TaskXml,
            DisplayName = "Tasks (XML)",
            Extension = ".xml",
            ContentType = "application/xml",
            AcceptAttribute = ".xml,application/xml,text/xml",
            Description = "XML document containing root <Tasks> with child <Task> elements.",
            SampleFileName = "Tasks_Template.xml"
        }
    };

    public FileTemplateDownloadService(
        IFileTemplateConfigRepository configRepository,
        ILogger<FileTemplateDownloadService> logger)
    {
        _configRepository = configRepository;
        _logger = logger;
    }

    public IReadOnlyList<ImportFileTypeMetadata> GetAvailableFileTypes() => AvailableTypes;

    public ImportFileTypeMetadata? GetMetadata(ImportFileTypeEnum fileType) =>
        AvailableTypes.FirstOrDefault(t => t.FileType == fileType);

    public async Task<FileTemplateDownloadResult> GenerateTemplateAsync(ImportFileTypeEnum fileType, CancellationToken ct = default)
    {
        var meta = GetMetadata(fileType) ?? new ImportFileTypeMetadata
        {
            FileType = fileType,
            DisplayName = fileType.ToString(),
            Extension = ".csv",
            ContentType = "text/csv",
            SampleFileName = $"{fileType}_Template.csv"
        };

        var config = await _configRepository.GetConfigAsync(fileType.ToString(), ct);
        var columns = config?.Columns?.Select(c => c.Name).ToList() ?? GetDefaultColumns(fileType);
        var sheetName = config?.Sheets?.FirstOrDefault()?.Name ?? "Calendar";

        byte[] bytes;
        if (meta.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            bytes = GenerateOpenXmlWorkbook(sheetName, columns);
        }
        else if (meta.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            bytes = GenerateJsonTemplate(columns);
        }
        else if (meta.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            bytes = GenerateXmlTemplate(columns);
        }
        else
        {
            bytes = GenerateCsvTemplate(columns);
        }

        return new FileTemplateDownloadResult
        {
            FileName = meta.SampleFileName,
            ContentType = meta.ContentType,
            ContentBytes = bytes
        };
    }

    private static List<string> GetDefaultColumns(ImportFileTypeEnum fileType)
    {
        return fileType switch
        {
            ImportFileTypeEnum.ProgramCalendarExcel => new List<string>
            {
                "EntryType", "Title", "StartDate", "EndDate", "Status", "OwnerEmails", "Comments", "ProgramCode"
            },
            ImportFileTypeEnum.ProjectCalendarExcel => new List<string>
            {
                "EntryType", "Title", "StartDate", "EndDate", "Status", "OwnerEmails", "Comments"
            },
            ImportFileTypeEnum.TaskExcel or ImportFileTypeEnum.TaskCsv or ImportFileTypeEnum.TaskJson or ImportFileTypeEnum.TaskXml => new List<string>
            {
                "TaskCode", "Title", "Priority", "Status", "AssigneeEmail", "Description"
            },
            _ => new List<string> { "Code", "Name", "Description" }
        };
    }

    private static byte[] GenerateCsvTemplate(List<string> columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns));
        // Add one sample row
        var sampleValues = columns.Select(c => c switch
        {
            "EntryType" => "Milestone",
            "Title" => "Phase 1 Delivery",
            "StartDate" => DateTime.UtcNow.ToString("yyyy-MM-dd"),
            "EndDate" => DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-dd"),
            "Status" => "Scheduled",
            "OwnerEmails" => "lead@example.com",
            "Comments" => "Initial planning milestone",
            "ProgramCode" => "PRG-01",
            "TaskCode" => "TSK-001",
            "Priority" => "High",
            "AssigneeEmail" => "dev@example.com",
            "Description" => "Sample task description",
            _ => "Sample"
        });
        sb.AppendLine(string.Join(",", sampleValues.Select(v => v.Contains(',') ? $"\"{v}\"" : v)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] GenerateJsonTemplate(List<string> columns)
    {
        var dict = new Dictionary<string, string>();
        foreach (var col in columns)
        {
            dict[col] = col switch
            {
                "TaskCode" => "TSK-001",
                "Title" => "Sample Task",
                "Priority" => "High",
                "Status" => "New",
                "AssigneeEmail" => "user@example.com",
                _ => "Value"
            };
        }
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { dict }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] GenerateXmlTemplate(List<string> columns)
    {
        var doc = new XDocument(
            new XElement("Tasks",
                new XElement("Task",
                    columns.Select(c => new XElement(c, c switch
                    {
                        "TaskCode" => "TSK-001",
                        "Title" => "Sample Task",
                        "Priority" => "High",
                        "Status" => "New",
                        "AssigneeEmail" => "user@example.com",
                        _ => "Value"
                    }))
                )
            )
        );
        return Encoding.UTF8.GetBytes(doc.ToString());
    }

    private static byte[] GenerateOpenXmlWorkbook(string sheetName, List<string> columns)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // 1. [Content_Types].xml
            var contentTypesEntry = zip.CreateEntry("[Content_Types].xml");
            using (var writer = new StreamWriter(contentTypesEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                        <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                        <Default Extension="xml" ContentType="application/xml"/>
                        <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                        <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                    </Types>
                    """);
            }

            // 2. _rels/.rels
            var relsEntry = zip.CreateEntry("_rels/.rels");
            using (var writer = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                    </Relationships>
                    """);
            }

            // 3. xl/_rels/workbook.xml.rels
            var wbRelsEntry = zip.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var writer = new StreamWriter(wbRelsEntry.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                    </Relationships>
                    """);
            }

            // 4. xl/workbook.xml
            var safeSheetName = System.Security.SecurityElement.Escape(sheetName);
            var wbEntry = zip.CreateEntry("xl/workbook.xml");
            using (var writer = new StreamWriter(wbEntry.Open(), Encoding.UTF8))
            {
                writer.Write($"""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                        <sheets>
                            <sheet name="{safeSheetName}" sheetId="1" r:id="rId1"/>
                        </sheets>
                    </workbook>
                    """);
            }

            // 5. xl/worksheets/sheet1.xml
            var sheetEntry = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using (var writer = new StreamWriter(sheetEntry.Open(), Encoding.UTF8))
            {
                var sb = new StringBuilder();
                sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

                // Header Row (Row 1)
                sb.Append("<row r=\"1\">");
                for (int i = 0; i < columns.Count; i++)
                {
                    var colRef = GetColumnLetter(i + 1);
                    var colName = System.Security.SecurityElement.Escape(columns[i]);
                    sb.Append($"<c r=\"{colRef}1\" t=\"inlineStr\"><is><t>{colName}</t></is></c>");
                }
                sb.Append("</row>");

                // Sample Row (Row 2)
                sb.Append("<row r=\"2\">");
                for (int i = 0; i < columns.Count; i++)
                {
                    var colRef = GetColumnLetter(i + 1);
                    var sampleVal = columns[i] switch
                    {
                        "EntryType" => "Milestone",
                        "Title" => "Phase 1 Delivery",
                        "StartDate" => DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        "EndDate" => DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-dd"),
                        "Status" => "Scheduled",
                        "OwnerEmails" => "lead@example.com",
                        "Comments" => "Initial planning milestone",
                        "ProgramCode" => "PRG-01",
                        "TaskCode" => "TSK-001",
                        "Priority" => "High",
                        "AssigneeEmail" => "dev@example.com",
                        "Description" => "Sample task description",
                        _ => "Sample"
                    };
                    sb.Append($"<c r=\"{colRef}2\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(sampleVal)}</t></is></c>");
                }
                sb.Append("</row>");

                sb.Append("</sheetData></worksheet>");
                writer.Write(sb.ToString());
            }
        }
        return ms.ToArray();
    }

    private static string GetColumnLetter(int columnNumber)
    {
        string col = "";
        while (columnNumber > 0)
        {
            int rem = (columnNumber - 1) % 26;
            col = (char)('A' + rem) + col;
            columnNumber = (columnNumber - 1) / 26;
        }
        return col;
    }
}
