using Microsoft.AspNetCore.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Web.Models.Dashboard;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Components.Pages;

public partial class HomePage : ComponentBase
{
    [Inject] internal ITenantService TenantService { get; set; } = default!;
    [Inject] internal IUserTaskService TaskService { get; set; } = default!;
    [Inject] internal IAuditService AuditService { get; set; } = default!;
    [Inject] internal ITenantContext TenantContext { get; set; } = default!;
    [Inject] internal ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] internal NavigationManager NavigationManager { get; set; } = default!;
    [Inject] internal IProgramService ProgramService { get; set; } = default!;
    [Inject] internal IProjectAuthorizationService ProjectAuthService { get; set; } = default!;
    [Inject] internal IProjectHealthService ProjectHealthService { get; set; } = default!;
    [Inject] internal IProjectContext ProjectContext { get; set; } = default!;
    [Inject] internal ILogger<HomePage> Logger { get; set; } = default!;

    private bool _isLoading = true;
    private bool _isRootAdminOnly;

    private List<ProgramEntity> _programs = new();
    private Guid? _selectedProgramId;
    private ProgramHealthSummaryDto? _programHealth;

    private List<ProjectCardItemDto> _userProjects = new();

    private string _projectCountText = "0";
    private string _completionRateText = "0%";
    private int _completedTasksCount = 0;
    private int _totalTasksCount = 0;

    private List<KanbanTaskItem> _kanbanTasks = new();
    private List<AuditActivityItem> _auditActivities = new();

    internal bool IsLoading => _isLoading;
    internal bool IsRootAdminOnly => _isRootAdminOnly;
    internal IReadOnlyList<ProgramEntity> Programs => _programs;
    internal ProgramHealthSummaryDto? ProgramHealth => _programHealth;
    internal IReadOnlyList<ProjectCardItemDto> UserProjects => _userProjects;
    internal Task InitializeForTestAsync() => LoadDashboardDataAsync();

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task OnProgramSelectedAsync(Guid programId)
    {
        _selectedProgramId = programId;
        _programHealth = await ProgramService.GetProgramHealthAsync(programId);
    }

    private void OpenProjectTasks(Guid projectId)
    {
        ProjectContext.SetActiveProject(projectId);
        NavigationManager.NavigateTo($"/projects/{projectId}/tasks");
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            _isLoading = true;

            // Zero-Trust check: Root Admin is for platform administration only
            if (!CurrentUser.IsAuthenticated_RegularTenant_User)
            {
                _isRootAdminOnly = CurrentUser.IsAuthenticated_RootTenant_User;
                _userProjects.Clear();
                _programs.Clear();
                _kanbanTasks.Clear();
                _auditActivities.Clear();
                _projectCountText = "0";
                _completionRateText = "0%";
                _totalTasksCount = 0;
                _completedTasksCount = 0;
                return;
            }

            _isRootAdminOnly = false;
            var tenantSlug = TenantContext.TenantSlug;

            // 1. Load Programs & Program Health Summary
            var programs = await ProgramService.GetProgramsAsync(tenantSlug);
            _programs = programs.ToList();

            if (_programs.Count > 0)
            {
                if (!_selectedProgramId.HasValue || !_programs.Any(p => p.Id == _selectedProgramId.Value))
                {
                    _selectedProgramId = _programs[0].Id;
                }

                _programHealth = await ProgramService.GetProgramHealthAsync(_selectedProgramId.Value);
            }
            else
            {
                _selectedProgramId = null;
                _programHealth = null;
            }

            // 2. Load User's Authorized Projects
            var authorizedProjectIds = await ProjectAuthService.GetAuthorizedProjectIdsAsync(ProjectRoleEnum.ReadOnly);
            var allTenantProjects = await TenantService.GetTenantProjectsAsync(tenantSlug);

            var userProjectEntities = allTenantProjects
                .Where(p => authorizedProjectIds.Contains(p.Id) && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToList();

            _projectCountText = userProjectEntities.Count.ToString();

            var projectCardList = new List<ProjectCardItemDto>();
            foreach (var proj in userProjectEntities)
            {
                var role = await ProjectAuthService.GetCurrentUserProjectRoleAsync(proj.Id);
                var healthSummary = await ProjectHealthService.GetProjectHealthAsync(proj.Id);

                projectCardList.Add(new ProjectCardItemDto
                {
                    Id = proj.Id,
                    Name = proj.Name,
                    ShortCode = proj.ShortCode,
                    Description = proj.Description,
                    ProgramId = proj.ProgramId,
                    ProgramName = proj.Program?.Name,
                    Status = proj.Status,
                    Health = healthSummary.Health,
                    UserRole = role,
                    ProgressPercentage = healthSummary.ProgressPercentage,
                    CompletedTasks = healthSummary.CompletedTasks,
                    TotalTasks = healthSummary.TotalTasks,
                    InProgressTasks = healthSummary.InProgressTasks,
                    OpenTasks = healthSummary.OpenTasks,
                    OverdueTasks = healthSummary.OverdueTasks,
                    ActiveBlockersCount = healthSummary.ActiveBlockersCount,
                    DueDate = proj.DueDate != default ? proj.DueDate : null
                });
            }
            _userProjects = projectCardList;

            // 3. Tasks Aggregation across authorized projects
            var tasks = await TaskService.GetRecentTasksAsync(30);
            var authorizedTasks = tasks.Where(t => authorizedProjectIds.Contains(t.ProjectId)).ToList();

            _totalTasksCount = authorizedTasks.Count;
            _completedTasksCount = authorizedTasks.Count(t => t.Status == TaskStatusEnum.Closed);
            var rate = _totalTasksCount > 0 ? (double)_completedTasksCount / _totalTasksCount * 100 : 0;
            _completionRateText = _totalTasksCount > 0 ? $"{rate:0.#}%" : "0%";

            _kanbanTasks = authorizedTasks.Take(9).Select(t => new KanbanTaskItem
            {
                Key = $"TSK-{t.Id.ToString()[..4].ToUpper()}",
                Title = t.Title,
                Column = t.Status == TaskStatusEnum.Closed ? "Done" : (t.Status == TaskStatusEnum.InProgress ? "InProgress" : "ToDo"),
                PriorityText = t.Priority.ToString(),
                PriorityVariant = t.Priority switch
                {
                    TaskPriorityEnum.Urgent => CskStatusChip.ChipVariant.Error,
                    TaskPriorityEnum.High => CskStatusChip.ChipVariant.Warning,
                    _ => CskStatusChip.ChipVariant.Neutral
                },
                Assignee = !string.IsNullOrWhiteSpace(t.AssigneeEmails) ? t.AssigneeEmails.Split(';')[0].Trim() : "Unassigned"
            }).ToList();

            // 4. Audit Records
            var auditTrail = await AuditService.GetAuditTrailAsync(new AuditTrailFilterReqDto { PageNumber = 1, PageSize = 5 });
            _auditActivities = auditTrail.Items.Select(a => new AuditActivityItem
            {
                Title = a.EventType.ToString(),
                Description = a.Description ?? $"{a.EntityName} updated",
                User = a.UserEmail ?? "system",
                TimeAgo = FormatTimeAgo(a.Created),
                BadgeText = a.Severity.ToString(),
                BadgeVariant = a.Severity switch
                {
                    AuditSeverityEnum.Critical => CskStatusChip.ChipVariant.Error,
                    AuditSeverityEnum.Warning => CskStatusChip.ChipVariant.Warning,
                    _ => CskStatusChip.ChipVariant.Info
                },
                Icon = CskIcons.ShieldCheck
            }).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load dashboard data for tenant '{TenantSlug}'", TenantContext.TenantSlug);
            _userProjects.Clear();
            _programs.Clear();
            _kanbanTasks.Clear();
            _auditActivities.Clear();
            _projectCountText = "0";
            _completionRateText = "0%";
            _totalTasksCount = 0;
            _completedTasksCount = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private void NavigateToTasks()
    {
        if (ProjectContext.ProjectId.HasValue)
        {
            NavigationManager.NavigateTo($"/projects/{ProjectContext.ProjectId.Value}/tasks");
        }
        else if (_userProjects.Count > 0)
        {
            OpenProjectTasks(_userProjects[0].Id);
        }
        else
        {
            NavigationManager.NavigateTo("tasks");
        }
    }

    private static CskStatusChip.ChipVariant GetProgramHealthVariant(ProgramHealthRatingEnum health) => health switch
    {
        ProgramHealthRatingEnum.OnTrack => CskStatusChip.ChipVariant.Success,
        ProgramHealthRatingEnum.AtRisk => CskStatusChip.ChipVariant.Warning,
        ProgramHealthRatingEnum.Critical => CskStatusChip.ChipVariant.Error,
        _ => CskStatusChip.ChipVariant.Neutral
    };

    private static CskStatusChip.ChipVariant GetProjectHealthVariant(ProjectHealthRatingEnum health) => health switch
    {
        ProjectHealthRatingEnum.OnTrack => CskStatusChip.ChipVariant.Success,
        ProjectHealthRatingEnum.AtRisk => CskStatusChip.ChipVariant.Warning,
        ProjectHealthRatingEnum.Critical => CskStatusChip.ChipVariant.Error,
        _ => CskStatusChip.ChipVariant.Neutral
    };

    private static CskStatusChip.ChipVariant GetProjectStatusVariant(ProjectStatusEnum status) => status switch
    {
        ProjectStatusEnum.Done => CskStatusChip.ChipVariant.Success,
        ProjectStatusEnum.Active => CskStatusChip.ChipVariant.Primary,
        ProjectStatusEnum.New => CskStatusChip.ChipVariant.Info,
        ProjectStatusEnum.OnHold => CskStatusChip.ChipVariant.Warning,
        _ => CskStatusChip.ChipVariant.Neutral
    };

    private static CskStatusChip.ChipVariant GetRoleVariant(ProjectRoleEnum? role) => role switch
    {
        ProjectRoleEnum.Admin => CskStatusChip.ChipVariant.Primary,
        ProjectRoleEnum.ElevatedLead or ProjectRoleEnum.Lead => CskStatusChip.ChipVariant.Warning,
        ProjectRoleEnum.Contribute => CskStatusChip.ChipVariant.Info,
        ProjectRoleEnum.PMO => CskStatusChip.ChipVariant.Secondary,
        _ => CskStatusChip.ChipVariant.Neutral
    };

    private sealed class KanbanTaskItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Column { get; set; } = "ToDo";
        public string PriorityText { get; set; } = "Medium";
        public CskStatusChip.ChipVariant PriorityVariant { get; set; } = CskStatusChip.ChipVariant.Neutral;
        public string Assignee { get; set; } = string.Empty;
    }

    private sealed class AuditActivityItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string BadgeText { get; set; } = "Info";
        public CskStatusChip.ChipVariant BadgeVariant { get; set; } = CskStatusChip.ChipVariant.Info;
        public string Icon { get; set; } = CskIcons.Info;
    }
}
