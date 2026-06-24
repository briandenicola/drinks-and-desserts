using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhiskeyAndSmokes.Api;
using WhiskeyAndSmokes.Api.Models;
using WhiskeyAndSmokes.Api.Services;

namespace WhiskeyAndSmokes.Api.Controllers;

[ApiController]
public class OidcController : ControllerBase
{
    private readonly IOidcService _oidcService;
    private readonly ILogger<OidcController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ICosmosDbService _cosmosDb;

    public OidcController(
        IOidcService oidcService,
        ILogger<OidcController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ICosmosDbService cosmosDb)
    {
        _oidcService = oidcService;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _cosmosDb = cosmosDb;
    }

    [AllowAnonymous]
    [HttpGet("api/auth/oidc/providers")]
    public async Task<ActionResult<object>> ListPublicProviders()
    {
        var providers = await _oidcService.ListPublicProvidersAsync();
        return Ok(new { providers });
    }

    [AllowAnonymous]
    [HttpPost("api/auth/oidc/{providerId}/start")]
    public async Task<ActionResult<OidcStartLoginResult>> StartLogin(string providerId, [FromBody] OidcStartLoginRequest? request)
    {
        try
        {
            return Ok(await _oidcService.StartLoginAsync(
                providerId,
                request?.RedirectPath,
                request?.CallbackPath,
                await RequestOriginAsync()));
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [AllowAnonymous]
    [HttpGet("api/auth/oidc/{providerId}/callback")]
    public async Task<ActionResult<AuthResponse>> LoginCallback(string providerId, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        NoStore();
        if (!string.IsNullOrEmpty(error))
        {
            return HandleOidcError(new OidcException(OidcError.ProviderDenied));
        }

        try
        {
            return Ok(await _oidcService.CompleteLoginCallbackAsync(providerId, code, state, await RequestOriginAsync()));
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize]
    [HttpPost("api/auth/oidc/{providerId}/link/start")]
    public async Task<ActionResult<OidcStartLoginResult>> StartLink(string providerId, [FromBody] OidcStartLoginRequest? request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            return Ok(await _oidcService.StartLinkAsync(
                providerId,
                userId,
                request?.RedirectPath,
                request?.CallbackPath,
                await RequestOriginAsync()));
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize]
    [HttpGet("api/auth/oidc/{providerId}/link/callback")]
    public async Task<ActionResult<OidcLinkCallbackResult>> LinkCallback(string providerId, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        NoStore();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (!string.IsNullOrEmpty(error))
        {
            return HandleOidcError(new OidcException(OidcError.ProviderDenied));
        }

        try
        {
            return Ok(await _oidcService.CompleteLinkCallbackAsync(providerId, userId, code, state, await RequestOriginAsync()));
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize]
    [HttpGet("api/users/me/oidc-identities")]
    public async Task<ActionResult<object>> ListLinkedIdentities()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var identities = await _oidcService.ListLinkedIdentitiesAsync(userId);
        return Ok(new { identities });
    }

    [Authorize]
    [HttpDelete("api/users/me/oidc-identities/{identityId}")]
    public async Task<IActionResult> UnlinkIdentity(string identityId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            await _oidcService.UnlinkIdentityAsync(identityId, userId);
            return Ok(new { message = "OIDC identity unlinked" });
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("api/admin/oidc/providers")]
    public async Task<ActionResult<object>> ListAdminProviders()
    {
        var providers = await _oidcService.ListAdminProvidersAsync();
        return Ok(new { providers });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/admin/oidc/providers")]
    public async Task<ActionResult<OidcAdminProviderDto>> CreateAdminProvider([FromBody] OidcAdminProviderInput input)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var provider = await _oidcService.CreateAdminProviderAsync(input);
            return Created($"/api/admin/oidc/providers/{provider.Id}", provider);
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("api/admin/oidc/providers/{providerId}")]
    public async Task<ActionResult<OidcAdminProviderDto>> UpdateAdminProvider(string providerId, [FromBody] OidcAdminProviderInput input)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            return Ok(await _oidcService.UpdateAdminProviderAsync(providerId, input));
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("api/admin/oidc/providers/{providerId}")]
    public async Task<IActionResult> DeleteAdminProvider(string providerId)
    {
        try
        {
            await _oidcService.DeleteAdminProviderAsync(providerId);
            return Ok(new { message = "OIDC provider deleted successfully" });
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/admin/oidc/providers/{providerId}/test")]
    public async Task<ActionResult<OidcProviderTestResult>> TestAdminProvider(string providerId)
    {
        try
        {
            return Ok(await _oidcService.TestAdminProviderAsync(providerId));
        }
        catch (OidcException ex) when (ex.Error == OidcError.ProviderDiscovery)
        {
            return Ok(new OidcProviderTestResult { Available = false, Message = ex.Message });
        }
        catch (OidcException ex)
        {
            return HandleOidcError(ex);
        }
    }

    private ObjectResult HandleOidcError(OidcException ex)
    {
        var (status, message) = ex.Error switch
        {
            OidcError.ProviderNotFound => (404, "OIDC provider not found"),
            OidcError.ProviderInvalid => (400, "Invalid OIDC provider configuration"),
            OidcError.ProviderSecretMissing => (400, "OIDC client secret is required"),
            OidcError.ProviderConfiguration => (500, ProviderConfigurationMessage(ex)),
            OidcError.ProviderDenied => (400, "OIDC provider denied access"),
            OidcError.ProviderDuplicate => (409, "OIDC provider already exists"),
            OidcError.ProviderInUse => (409, "OIDC provider has linked identities"),
            OidcError.ProviderDisabled => (409, "OIDC provider is disabled"),
            OidcError.InvalidRedirect => (400, "Invalid redirect path"),
            OidcError.InvalidState => (400, "Invalid OIDC state"),
            OidcError.CodeExchangeFailed => (400, "OIDC authorization code was rejected"),
            OidcError.ValidationFailed => (401, "OIDC validation failed"),
            OidcError.IdentityNotLinked => (401, "OIDC identity is not linked"),
            OidcError.IdentityNotFound => (404, "OIDC identity not found"),
            OidcError.IdentityAlreadyLinked => (409, "OIDC identity is already linked to another account"),
            OidcError.AccountConflict => (409, "Sign in locally and link this OIDC identity from Account Settings"),
            OidcError.NoUsableSignInMethod => (409, "Cannot unlink the last usable sign-in method"),
            OidcError.TokenIssueFailed => (500, "Failed to issue app session"),
            _ => (500, "Failed to process OIDC provider request")
        };

        if (status >= 500)
        {
            _logger.LogError(ex, "OIDC request failed: {Error}", ex.Error);
        }
        else
        {
            _logger.LogWarning(ex, "OIDC request rejected: {Error}", ex.Error);
        }

        return StatusCode(status, new { error = message, detail = ex.Error == OidcError.CodeExchangeFailed ? ex.Message : null });
    }

    private static string ProviderConfigurationMessage(OidcException ex)
    {
        return ex.Message.StartsWith("OIDC public origin", StringComparison.OrdinalIgnoreCase)
            ? ex.Message
            : "OIDC provider is misconfigured";
    }

    private async Task<string> RequestOriginAsync()
    {
        AppAuthSettingsDocument? stored = null;
        try
        {
            stored = await _cosmosDb.GetAsync<AppAuthSettingsDocument>(
                "settings",
                AppAuthSettingsDocument.DocumentId,
                AppAuthSettingsDocument.PartitionKeyValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load admin-managed OIDC public origin; using configured fallback");
        }

        if (!string.IsNullOrWhiteSpace(stored?.Settings.OidcPublicOrigin))
        {
            return NormalizePublicOrigin(stored.Settings.OidcPublicOrigin);
        }

        var configuredOrigin = _configuration[$"{OidcOptions.Section}:PublicOrigin"];
        if (!string.IsNullOrWhiteSpace(configuredOrigin))
        {
            return NormalizePublicOrigin(configuredOrigin);
        }

        if (!_environment.IsDevelopment() ||
            !Request.Host.HasValue ||
            !OidcPublicOrigin.IsLocalhost(Request.Host.Host) ||
            (Request.Scheme != "http" && Request.Scheme != "https"))
        {
            throw new OidcException(OidcError.ProviderConfiguration, "OIDC public origin is not configured");
        }

        return $"{Request.Scheme}://{Request.Host.Value}";
    }

    private static string NormalizePublicOrigin(string origin)
    {
        try
        {
            return OidcPublicOrigin.Normalize(origin);
        }
        catch (FormatException ex)
        {
            throw new OidcException(OidcError.ProviderConfiguration, "OIDC public origin is invalid", ex);
        }
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
