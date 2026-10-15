using Microsoft.AspNetCore.Mvc;
using BlazorFluent.Api.Auth;

namespace BlazorFluent.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthGatewayService _authGateway;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthGatewayService authGateway,
        ILogger<AuthController> logger)
    {
        _authGateway = authGateway;
        _logger = logger;
    }

    public record AuthorizeRequest(string Email, string ClientId, string RedirectUri);
    public record TokenRequest(string Code, string ClientId, string RedirectUri);

    [HttpPost("authorize")]
    public async Task<IActionResult> Authorize([FromBody] AuthorizeRequest request)
    {
        var result = await _authGateway.GenerateAuthorizationCodeAsync(request.Email, request.ClientId);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = "AuthorizationFailed", details = result.Errors });
        }

        return Ok(new { code = result.Data, redirectUri = request.RedirectUri });
    }

    [HttpPost("token")]
    public async Task<IActionResult> ExchangeToken([FromBody] TokenRequest request)
    {
        var result = await _authGateway.ExchangeCodeForTokenAsync(request.Code, request.ClientId);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = "TokenExchangeFailed", details = result.Errors });
        }

        return Ok(result.Data);
    }
}
