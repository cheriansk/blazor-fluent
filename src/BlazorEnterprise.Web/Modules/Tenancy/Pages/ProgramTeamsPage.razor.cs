// [NEED-HUMAN-REVIEW]
using System.Text.Json;
using BlazorEnterprise.Components.Common;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Persistence.Context;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Modules.Tenancy.Pages;

public partial class ProgramTeamsPage : AuthorizedPageComponentBase
{
    [Inject] private IDbContextFactory<AppDbContext> DbFactory { get; set; } = default!;

    private bool _isLoading;
    private bool _isEditorOpen;
    private bool _isHistoryOpen;
    private string? _errorMessage;
    private string? _dialogError;
    private Guid? _editingTeamId;
    private string _formName = string.Empty;
    private string? _formDescription;
    private List<string> _formOwners = [string.Empty];
    private string _historyTeamName = string.Empty;
    private List<ProgramTeamRowDto> _teams = [];
    private List<TeamHistoryRowDto> _historyItems = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadTeamsAsync();
    }

    private async Task LoadTeamsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var rows = await db.ProgramTeams
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.ProgramId,
                    t.Name,
                    t.Description,
                    t.OwnerNames,
                    t.IsDisabled
                })
                .ToListAsync();

            _teams = rows.Select(r => new ProgramTeamRowDto(
                r.Id,
                r.ProgramId,
                r.Name,
                r.Description,
                r.OwnerNames,
                string.Join(", ", r.OwnerNames),
                r.IsDisabled)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load program teams.");
            _errorMessage = "Unable to load program teams. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OpenCreateDialog()
    {
        _editingTeamId = null;
        _formName = string.Empty;
        _formDescription = null;
        _formOwners = [string.Empty];
        _dialogError = null;
        _isEditorOpen = true;
    }

    private void OpenEditDialog(ProgramTeamRowDto team)
    {
        _editingTeamId = team.Id;
        _formName = team.Name;
        _formDescription = team.Description;
        _formOwners = team.OwnerNames.Count > 0 ? [.. team.OwnerNames] : [string.Empty];
        _dialogError = null;
        _isEditorOpen = true;
    }

    private async Task SaveTeamAsync()
    {
        _dialogError = null;
        var cleanName = _formName?.Trim() ?? string.Empty;
        var cleanOwners = _formOwners.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            _dialogError = "Team name is required.";
            return;
        }

        if (cleanOwners.Count is < 1 or > 4)
        {
            _dialogError = "Please provide between 1 and 4 owner names.";
            return;
        }

        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var programId = Guid.Empty;
            var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted);
            if (project is not null)
            {
                programId = project.ProgramId ?? Guid.Empty;
            }

            var duplicate = await db.ProgramTeams
                .AsNoTracking()
                .AnyAsync(t => !t.IsDeleted && t.ProgramId == programId && t.Name == cleanName && t.Id != _editingTeamId);

            if (duplicate)
            {
                _dialogError = $"A team named '{cleanName}' already exists in this program.";
                return;
            }

            ProgramTeamEntity entity;
            if (_editingTeamId.HasValue)
            {
                entity = await db.ProgramTeams.AsTracking().FirstAsync(t => t.Id == _editingTeamId.Value);
                entity.Name = cleanName;
                entity.Description = _formDescription?.Trim();
                entity.OwnerNames = cleanOwners;
            }
            else
            {
                entity = new ProgramTeamEntity
                {
                    ProgramId = programId,
                    Name = cleanName,
                    Description = _formDescription?.Trim(),
                    OwnerNames = cleanOwners,
                    IsDisabled = false
                };
                db.ProgramTeams.Add(entity);
            }

            db.IsTransactionScopeCommitAuthorized = true;
            try
            {
                await db.SaveChangesAsync();
            }
            finally
            {
                db.IsTransactionScopeCommitAuthorized = false;
            }

            _isEditorOpen = false;
            await LoadTeamsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save program team.");
            _dialogError = "Unable to save team. Please try again.";
        }
    }

    private async Task ToggleDisableAsync(ProgramTeamRowDto team)
    {
        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var entity = await db.ProgramTeams.AsTracking().FirstOrDefaultAsync(t => t.Id == team.Id);
            if (entity is null) return;

            entity.IsDisabled = !entity.IsDisabled;

            db.IsTransactionScopeCommitAuthorized = true;
            try
            {
                await db.SaveChangesAsync();
            }
            finally
            {
                db.IsTransactionScopeCommitAuthorized = false;
            }

            await LoadTeamsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to toggle team status for {TeamId}.", team.Id);
            _errorMessage = "Unable to update team status.";
        }
    }

    private async Task OpenHistoryDialogAsync(ProgramTeamRowDto team)
    {
        _historyTeamName = team.Name;
        _historyItems = [];
        _isHistoryOpen = true;

        try
        {
            await using var db = await DbFactory.CreateDbContextAsync();
            var teamKey = team.Id.ToString();
            _historyItems = await db.AuditRecords
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(ProgramTeamEntity) && a.EntityId == teamKey)
                .OrderByDescending(a => a.Created)
                .Select(a => new TeamHistoryRowDto(a.Operation != null ? a.Operation.ToString()! : a.EventType.ToString(), a.UserEmail ?? a.UserId ?? "System", a.Created, a.ChangesJson ?? string.Empty))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load history for team {TeamId}.", team.Id);
        }
    }

    private sealed record ProgramTeamRowDto(
        Guid Id,
        Guid ProgramId,
        string Name,
        string? Description,
        List<string> OwnerNames,
        string OwnersDisplay,
        bool IsDisabled);

    private sealed record TeamHistoryRowDto(
        string Action,
        string ChangedBy,
        DateTime ChangedAtUtc,
        string SnapshotJson);
}
