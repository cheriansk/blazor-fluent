using BlazorEnterprise.Shared.UI.Components;
﻿using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Imports;
using BlazorEnterprise.Core.Extensions;
using BlazorEnterprise.Modules.Imports.Components;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorEnterprise.Modules.Imports.Pages;

public partial class FileImportPage : ComponentBase
{
    [Inject] private IImportFileService ImportService { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

private ImportErrorsModal? _errorsModal;
    private ConfirmDialogModal? _confirmDialog;

    private List<ProjectEntity> _availableProjects = new();
    private ProjectEntity? _currentProject;

    private IReadOnlyList<DataListItem<ImportFileTypeEnum>> _fileTypeOptions = Array.Empty<DataListItem<ImportFileTypeEnum>>();
    private readonly List<PendingUploadItem> _pendingFiles = new();

    private bool _isUploading;
    private bool _isLoadingHistory = true;
    private string? _statusMessage;
    private MessageBarIntent _messageIntent = MessageBarIntent.Info;

    private List<ImportFileSummaryDto> _history = new();
    private Guid _pendingDeleteId;

    private sealed class PendingUploadItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public IBrowserFile File { get; set; } = default!;
        public string FileName => File.Name;
        public long Size => File.Size;
        public ImportFileTypeEnum SelectedFileType { get; set; } = ImportFileTypeEnum.TaskExcel;
    }

    protected override async Task OnInitializedAsync()
    {
        _fileTypeOptions = DataListExtensions.GetDataListItems<ImportFileTypeEnum>();

        await LoadProjectsAsync();
        await RefreshHistoryAsync();
    }

    private async Task LoadProjectsAsync()
    {
        _availableProjects = (await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug))

            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToList();

        if (_availableProjects.Count > 0)
        {
            _currentProject = _availableProjects[0];
        }
    }

    private async Task OnProjectChanged(string? newProjectIdStr)
    {
        _currentProject = Guid.TryParse(newProjectIdStr, out var projId)
            ? _availableProjects.FirstOrDefault(p => p.Id == projId)
            : null;
        await RefreshHistoryAsync();
    }

    private void HandleFilesSelected(InputFileChangeEventArgs e)
    {
        _statusMessage = null;
        var selected = e.GetMultipleFiles(50);
        foreach (var file in selected)
        {
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var detectedType = ext switch
            {
                ".xlsx" or ".xls" => ImportFileTypeEnum.TaskExcel,
                ".json" => ImportFileTypeEnum.TaskJson,
                ".xml" => ImportFileTypeEnum.TaskXml,
                ".csv" or ".txt" or ".tsv" => ImportFileTypeEnum.TaskCsv,
                _ => ImportFileTypeEnum.TaskExcel
            };

            _pendingFiles.Add(new PendingUploadItem
            {
                File = file,
                SelectedFileType = detectedType
            });
        }
    }

    private void OnItemFileTypeChanged(PendingUploadItem item, string? newCode)
    {
        if (newCode.TryToEnum<ImportFileTypeEnum>(out var fileType))
        {
            item.SelectedFileType = fileType;
        }
    }

    private void RemovePendingFile(Guid id)
    {
        _pendingFiles.RemoveAll(f => f.Id == id);
    }

    private void ClearPendingFiles()
    {
        _pendingFiles.Clear();
    }

    private async Task UploadBatchAsync()
    {
        if (_pendingFiles.Count == 0 || _currentProject == null) return;

        _isUploading = true;
        _statusMessage = null;

        try
        {
            const long maxFileSize = 52_428_800; // 50MB per file
            var fileItems = new List<UploadBatchFileItem>(_pendingFiles.Count);

            foreach (var pending in _pendingFiles)
            {
                var memoryStream = new MemoryStream();
                using var readStream = pending.File.OpenReadStream(maxFileSize);
                await readStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                fileItems.Add(new UploadBatchFileItem
                {
                    FileType = pending.SelectedFileType,
                    FileName = pending.FileName,
                    ContentType = pending.File.ContentType ?? "application/octet-stream",
                    FileSizeBytes = pending.Size,
                    ContentStream = memoryStream
                });
            }

            var request = new UploadBatchRequest
            {
                ProjectId = _currentProject.Id,
                Files = fileItems
            };

            var result = await ImportService.UploadBatchAndQueueAsync(request);
            if (result.IsSuccess)
            {
                _messageIntent = MessageBarIntent.Success;
                _statusMessage = $"Batch {result.Value:N} uploaded successfully ({fileItems.Count} file(s)). Pipeline queued for background validation, staging, and domain processing.";
                _pendingFiles.Clear();
                await RefreshHistoryAsync();
            }
            else
            {
                _messageIntent = MessageBarIntent.Error;
                _statusMessage = result.Error;
            }
        }
        catch (Exception ex)
        {
            _messageIntent = MessageBarIntent.Error;
            _statusMessage = $"Upload failed: {ex.Message}";
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task RefreshHistoryAsync()
    {
        if (_currentProject == null)
        {
            _history.Clear();
            _isLoadingHistory = false;
            return;
        }

        _isLoadingHistory = true;
        try
        {
            _history = (await ImportService.GetImportHistoryAsync(_currentProject.Id)).ToList();
        }
        finally
        {
            _isLoadingHistory = false;
        }
    }

    private async Task ViewDiagnosticsAsync(Guid importFileId)
    {
        if (_currentProject is null) return;
        var details = await ImportService.GetImportFileDetailsAsync(_currentProject.Id, importFileId);
        if (details != null && _errorsModal != null)
        {
            _errorsModal.Show(details);
        }
    }

    private void PromptDelete(Guid importFileId)
    {
        _pendingDeleteId = importFileId;
        _confirmDialog?.Show();
    }

    private async Task DeleteConfirmedAsync()
    {
        if (_pendingDeleteId != Guid.Empty && _currentProject is not null)
        {
            await ImportService.DeleteImportFileAsync(_currentProject.Id, _pendingDeleteId);
            _pendingDeleteId = Guid.Empty;
            await RefreshHistoryAsync();
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}