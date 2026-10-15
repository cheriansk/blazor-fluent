using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Models;

namespace BlazorFluent.Jobs.Engine;

/// <summary>
/// Pre-flight security validator for incoming uploaded files.
/// Enforces MIME magic bytes checks, file size ceilings (50MB), path traversal defense, and extension validation.
/// </summary>
public static class PreFlightFileValidator
{
    public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly byte[] ZipMagicBytes = [0x50, 0x4B, 0x03, 0x04]; // PK.. for .xlsx

    /// <summary>
    /// Validates file header magic bytes, size limits, and filename sanitization.
    /// </summary>
    public static FileValidationResult Validate(
        string rawFileName,
        long fileSizeBytes,
        Stream contentStream,
        ImportFileType expectedType)
    {
        var errors = new List<FileValidationError>();

        // 1. Size ceiling check
        if (fileSizeBytes <= 0)
        {
            errors.Add(new FileValidationError("File", 0, "Size", "File cannot be empty (0 bytes)."));
        }
        else if (fileSizeBytes > MaxFileSizeBytes)
        {
            errors.Add(new FileValidationError("File", 0, "Size", 
                $"File size ({fileSizeBytes / (1024 * 1024)}MB) exceeds maximum permitted limit of 50MB."));
        }

        // 2. Path sanitization check
        var sanitizedName = SanitizeFileName(rawFileName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            errors.Add(new FileValidationError("File", 0, "FileName", "Invalid or unsafe file name provided."));
        }

        // 3. Extension check
        var extension = Path.GetExtension(sanitizedName).ToLowerInvariant();
        if (expectedType == ImportFileType.TaskExcel && extension != ".xlsx")
        {
            errors.Add(new FileValidationError("File", 0, "Extension", "Excel import requires a valid .xlsx file."));
        }
        else if (expectedType == ImportFileType.TaskCsv && extension != ".csv")
        {
            errors.Add(new FileValidationError("File", 0, "Extension", "CSV import requires a valid .csv file."));
        }

        // 4. MIME Magic Bytes inspection
        if (contentStream.CanRead)
        {
            var initialPosition = contentStream.CanSeek ? contentStream.Position : 0;

            if (expectedType == ImportFileType.TaskExcel)
            {
                var header = new byte[4];
                var bytesRead = contentStream.Read(header, 0, 4);

                if (contentStream.CanSeek)
                {
                    contentStream.Position = initialPosition;
                }

                if (bytesRead < 4 || !header.SequenceEqual(ZipMagicBytes))
                {
                    errors.Add(new FileValidationError("File", 0, "MagicBytes", 
                        "File signature mismatch. The uploaded file is not a valid OpenXML/Excel spreadsheet."));
                }
            }
            else if (expectedType == ImportFileType.TaskCsv)
            {
                // Verify CSV has no binary null bytes in first 512 bytes
                var sample = new byte[512];
                var bytesRead = contentStream.Read(sample, 0, sample.Length);

                if (contentStream.CanSeek)
                {
                    contentStream.Position = initialPosition;
                }

                if (sample.Take(bytesRead).Any(b => b == 0))
                {
                    errors.Add(new FileValidationError("File", 0, "MagicBytes", 
                        "File signature mismatch. Binary null bytes detected in plain-text CSV file."));
                }
            }
        }

        return errors.Count == 0 
            ? FileValidationResult.Success() 
            : FileValidationResult.Failed(errors);
    }

    /// <summary>
    /// Strips path traversal characters (../, ..\), control characters, and invalid filesystem characters.
    /// </summary>
    public static string SanitizeFileName(string? rawFileName)
    {
        if (string.IsNullOrWhiteSpace(rawFileName)) return string.Empty;

        // Strip directories and path traversal
        var fileName = Path.GetFileName(rawFileName.Trim());

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = fileName.Where(c => !invalidChars.Contains(c) && !char.IsControl(c)).ToArray();

        return new string(cleanChars);
    }
}
