using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;
using Microsoft.AspNetCore.Authorization;

namespace BlazorEnterprise.Infrastructure.Security;

public class OperationAuthorizationRequirement : IAuthorizationRequirement
{
    public string Name { get; }
    public OperationAuthorizationRequirement(string name) => Name = name;
}

public class ProjectResourceAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ProjectScopedEntity>
{
    private readonly IProjectAuthorizationService _projectAuth;

    public ProjectResourceAuthorizationHandler(IProjectAuthorizationService projectAuth)
    {
        _projectAuth = projectAuth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ProjectScopedEntity resource)
    {
        var minRole = requirement.Name switch
        {
            nameof(ResourceOperations.Delete) => ProjectRoleEnum.Admin,
            nameof(ResourceOperations.Update) => ProjectRoleEnum.Contribute,
            nameof(ResourceOperations.Create) => ProjectRoleEnum.Contribute,
            _ => ProjectRoleEnum.ReadOnly
        };

        var canAccess = await _projectAuth.CanVisitAsync(resource.ProjectId ?? Guid.Empty, minRole);
        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
