using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;
using Microsoft.AspNetCore.Authorization;

namespace BlazorFluent.Infrastructure.Security;

public class OperationAuthorizationRequirement : IAuthorizationRequirement
{
    public string Name { get; }
    public OperationAuthorizationRequirement(string name) => Name = name;
}

public class ProjectResourceAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, IProjectScopedEntity>
{
    private readonly IProjectAuthorizationService _projectAuth;

    public ProjectResourceAuthorizationHandler(IProjectAuthorizationService projectAuth)
    {
        _projectAuth = projectAuth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        IProjectScopedEntity resource)
    {
        var minRole = requirement.Name switch
        {
            nameof(ResourceOperations.Delete) => ProjectRole.Admin,
            nameof(ResourceOperations.Update) => ProjectRole.Dev,
            nameof(ResourceOperations.Create) => ProjectRole.Dev,
            _ => ProjectRole.ReadOnly
        };

        var canAccess = await _projectAuth.CanVisitAsync(resource.ProjectId, minRole);
        if (canAccess)
        {
            context.Succeed(requirement);
        }
    }
}
