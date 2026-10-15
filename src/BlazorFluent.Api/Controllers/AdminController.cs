using Microsoft.AspNetCore.Mvc;
using BlazorFluent.Api.Admin;
using BlazorFluent.Api.Auth;

namespace BlazorFluent.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminSecurityService _adminSecurity;
    private readonly IEntitlementsRepository _entitlementsRepo;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminSecurityService adminSecurity,
        IEntitlementsRepository entitlementsRepo,
        ILogger<AdminController> logger)
    {
        _adminSecurity = adminSecurity;
        _entitlementsRepo = entitlementsRepo;
        _logger = logger;
    }

    private bool CheckAdminAccess(out string userEmail)
    {
        userEmail = Request.Headers["X-Admin-Email"].FirstOrDefault()
            ?? User.Identity?.Name
            ?? string.Empty;

        return _adminSecurity.IsAuthorizedAdmin(userEmail);
    }

    [HttpGet("entitlements")]
    public async Task<IActionResult> GetEntitlements()
    {
        if (!CheckAdminAccess(out var email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "Admin access restricted to verified Key Vault administrators." });
        }

        var users = await _entitlementsRepo.GetAllAsync();
        return Ok(users);
    }

    [HttpPost("entitlements")]
    public async Task<IActionResult> UpsertEntitlement([FromBody] EntitlementUser user)
    {
        if (!CheckAdminAccess(out var email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "Admin access restricted to verified Key Vault administrators." });
        }

        if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.TenantId))
        {
            return BadRequest("Email and TenantId are required fields.");
        }

        await _entitlementsRepo.UpsertAsync(user);
        _logger.LogInformation("Administrator {AdminEmail} updated entitlement for {TargetUser}", email, user.Email);

        return Ok(user);
    }

    [HttpDelete("entitlements/{targetEmail}")]
    public async Task<IActionResult> DeleteEntitlement(string targetEmail)
    {
        if (!CheckAdminAccess(out var email))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = "Admin access restricted to verified Key Vault administrators." });
        }

        var removed = await _entitlementsRepo.DeleteAsync(targetEmail);
        _logger.LogInformation("Administrator {AdminEmail} deleted entitlement for {TargetUser} (Result: {Removed})", email, targetEmail, removed);

        return Ok(new { removed });
    }
}
