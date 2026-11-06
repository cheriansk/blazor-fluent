using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Imports;
using BlazorEnterprise.Core.Extensions;
using BlazorEnterprise.Modules.Imports.Components;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Web.Modules.Imports.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace BlazorEnterprise.Modules.Imports.Pages;

public partial class FileImportPage : ComponentBase, IDisposable
{
    [Inject] private IImportFileService ImportService { get; set; } = default!;
    [Inject] private IFileTemplateDownloadService TemplateDownloadService { get; set; } = default!;
    [Inject] private ICircuitActivityTracker CircuitActivity { get; set; } = default!;
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private IProjectContext ProjectContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ImportErrorsModal? _errorsModal;
    private ConfirmDialogModal? _confirmDialog;

    private ProjectEntity? _currentProject;

    // Tabs
    private string _activeTabId = "tab-import";

    // File type configuration & state
    private IReadOnlyList<ImportFileTypeMetadata> _availableTypes = Array.Empty<ImportFileTypeMetadata>();
    private ImportFileTypeEnum _selectedFileType = ImportFileTypeEnum.ProgramCalendarExcel;
    private ImportFileTypeMetadata? _selectedMetadata;

    // Selected file
    private IBrowserFile? _selectedFile;
    private string? _selectedFileName;
    private long _selectedFileSize;

    // Progress, Lock & Status
    private bool _isStagingInProgress;
    private IDisposable? _activeMutationScope;
    private bool _isLoadingHistory = true;
    private bool _isDownloadingTemplate;
    private string? _statusMessage;
    private MessageBarIntent _messageIntent = MessageBarIntent.Info;

    // Staging / Batch metrics
    private List<ImportFileSummaryDto> _history = new();
    private Guid _pendingDeleteId;

    protected override async Task OnInitializedAsync()
    {
        _availableTypes = TemplateDownloadService.GetAvailableFileTypes();
        UpdateSelectedMetadata();

        ProjectContext.OnChange += HandleProjectContextChanged;

        await SyncActiveProjectAsync();
        await RefreshHistoryAsync();
    }

    private void UpdateSelectedMetadata()
    {
        _selectedMetadata = TemplateDownloadService.GetMetadata(_selectedFileType)
            ?? _availableTypes.FirstOrDefault(t => t.FileType == _selectedFileType)
            ?? _availableTypes.FirstOrDefault();
    }

    private void HandleProjectContextChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await SyncActiveProjectAsync();
            await RefreshHistoryAsync();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        ProjectContext.OnChange -= HandleProjectContextChanged;
        _activeMutationScope?.Dispose();
    }

    private async Task SyncActiveProjectAsync()
    {
        if (!ProjectContext.ProjectId.HasValue || ProjectContext.ProjectId.Value == Guid.Empty)
        {
            _currentProject = null;
            return;
        }

        var allProjects = await TenantService.GetTenantProjectsAsync(TenantContext.TenantSlug);
        _currentProject = allProjects.FirstOrDefault(p => p.Id == ProjectContext.ProjectId.Value && p.IsActive && !p.IsDeleted);
    }

    private void OnFileTypeChanged(string? newCode)
    {
        if (newCode.TryToEnum<ImportFileTypeEnum>(out var fileType))
        {
            _selectedFileType = fileType;
            UpdateSelectedMetadata();
            // Clear current file if its extension does not match the new type
            if (_selectedFile != null && _selectedMetadata != null)
            {
                var ext = Path.GetExtension(_selectedFile.Name);
                if (!string.Equals(ext, _selectedMetadata.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    ClearSelectedFile();
                }
            }
            StateHasChanged();
        }
    }

    private void HandleFileSelected(InputFileChangeEventArgs e)
    {
        _statusMessage = null;
        var file = e.File;
        if (file == null) return;

        if (_selectedMetadata != null)
        {
            var ext = Path.GetExtension(file.Name);
            if (!string.Equals(ext, _selectedMetadata.Extension, StringComparison.OrdinalIgnoreCase))
            {
                _messageIntent = MessageBarIntent.Error;
                _statusMessage = $"Invalid file format: '{file.Name}' does not match required extension '{_selectedMetadata.Extension}'.";
                return;
            }
        }

        _selectedFile = file;
        _selectedFileName = file.Name;
        _selectedFileSize = file.Size;
    }

    private void ClearSelectedFile()
    {
        _selectedFile = null;
        _selectedFileName = null;
        _selectedFileSize = 0;
        _statusMessage = null;
    }

    private async Task DownloadTemplateAsync()
    {
        if (_isDownloadingTemplate) return;
        _isDownloadingTemplate = true;
        try
        {
            var template = await TemplateDownloadService.GenerateTemplateAsync(_selectedFileType);
            var base64 = Convert.ToBase64String(template.ContentBytes);
            await JSRuntime.InvokeVoidAsync("cskFileDownload.downloadFromBase64", template.FileName, template.ContentType, base64);
        }
        catch (Exception ex)
        {
            _messageIntent = MessageBarIntent.Error;
            _statusMessage = $"Failed to download template: {ex.Message}";
        }
        finally
        {
            _isDownloadingTemplate = false;
        }
    }

    private void HandleBeforeInternalNavigation(LocationChangingContext context)
    {
        if (_isStagingInProgress)
        {
            context.PreventNavigation();
            _statusMessage = "Operation in progress: Navigation is locked until validation and staging complete.";
            _messageIntent = MessageBarIntent.Warning;
            StateHasChanged();
        }
    }

    private async Task ImportAndStageFileAsync()
    {
        if (_selectedFile == null || _currentProject == null || _isStagingInProgress) return;

        _isStagingInProgress = true;
        _activeMutationScope = CircuitActivity.BeginOperation(isWrite: true);
        _statusMessage = null;

        try
        {
            const long maxFileSize = 52_428_800; // 50MB per file
            var memoryStream = new MemoryStream();
            using (var readStream = _selectedFile.OpenReadStream(maxFileSize))
            {
                await readStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;

            var fileItem = new UploadBatchFileItem
            {
                FileType = _selectedFileType,
                FileName = _selectedFileName ?? _selectedFile.Name,
                ContentType = _selectedFile.ContentType ?? "application/octet-stream",
                FileSizeBytes = _selectedFileSize,
                ContentStream = memoryStream
            };

            var request = new UploadBatchRequest
            {
                ProjectId = _currentProject.Id,
                Files = new List<UploadBatchFileItem> { fileItem }
            };

            var result = await ImportService.UploadBatchAndQueueAsync(request);
            if (result.IsSuccess)
            {
                _messageIntent = MessageBarIntent.Success;
                _statusMessage = $"File '{fileItem.FileName}' successfully validated and staged! Background batch processing ({result.Value:N}) has been triggered.";
                ClearSelectedFile();
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
            _statusMessage = $"Failed to import and stage file: {ex.Message}";
        }
        finally
        {
            _activeMutationScope?.Dispose();
            _activeMutationScope = null;
            _isStagingInProgress = false;
            StateHasChanged();
        }
    }

    private async Task RefreshHistoryAsync()
    {
        if (!ProjectContext.ProjectId.HasValue || ProjectContext.ProjectId.Value == Guid.Empty)
        {
            _history = new();
            _isLoadingHistory = false;
            return;
        }

        _isLoadingHistory = true;
        try
        {
            var results = await ImportService.GetImportHistoryAsync(ProjectContext.ProjectId.Value);
            _history = results.ToList();
        }
        catch (Exception ex)
        {
            _messageIntent = MessageBarIntent.Error;
            _statusMessage = $"Failed to load import history: {ex.Message}";
        }
        finally
        {
            _isLoadingHistory = false;
        }
    }

    private async Task ViewDiagnosticsAsync(Guid importFileId)
    {
        if (_currentProject == null || _errorsModal == null) return;
        var details = await ImportService.GetImportFileDetailsAsync(_currentProject.Id, importFileId);
        if (details != null)
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
        if (_currentProject == null || _pendingDeleteId == Guid.Empty) return;

        var result = await ImportService.DeleteImportFileAsync(_currentProject.Id, _pendingDeleteId);
        if (result.IsSuccess)
        {
            await RefreshHistoryAsync();
        }
        else
        {
            _messageIntent = MessageBarIntent.Error;
            _statusMessage = result.Error;
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
