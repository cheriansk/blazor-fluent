// [NEED-HUMAN-REVIEW]
using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Persistence.Context;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Modules.Imports.Pages;

public partial class StagedImportsPage : AuthorizedPageComponentBase
{
    [Inject] private IDbContextFactory<AppDbContext> DbFactory { get; set; } = default!;

    private bool _isLoading;
    private bool _isPayloadDialogOpen;
    private StagedImportRowDto? _selectedRow;
    private List<StagedImportRowDto> _items = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadStagedImportsAsync();
    }

    private async Task LoadStagedImportsAsync()
    {
        _isLoading = true;
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var rows = await db.StagedImports
                .AsNoTracking()
                .OrderByDescending(s => s.Created)
                .Take(200)
                .Select(s => new StagedImportRowDto(
                    s.Id,
                    s.OriginalFileName,
                    s.FileType,
                    s.SheetName,
                    s.RowCount,
                    s.Status,
                    s.ProcessedAtUtc,
                    s.ErrorMessage,
                    s.PayloadJson))
                .ToListAsync();

            _items = rows;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load staged imports.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenPayloadDialog(StagedImportRowDto row)
    {
        _selectedRow = row;
        _isPayloadDialogOpen = true;
    }

    private sealed record StagedImportRowDto(
        Guid Id,
        string OriginalFileName,
        string FileType,
        string SheetName,
        int RowCount,
        string Status,
        DateTime? ProcessedAtUtc,
        string? ErrorMessage,
        string PayloadJson);
}
