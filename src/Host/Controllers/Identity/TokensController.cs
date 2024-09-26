using FSH.WebApi.Application.Identity.Tokens;
using FluentValidation;

namespace FSH.WebApi.Host.Controllers.Identity
{
    public sealed class TokensController : VersionNeutralApiController
    {
        private readonly ITokenService _tokenService;

        public TokensController(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        [HttpPost]
        [AllowAnonymous]
        [TenantIdHeader]
        [OpenApiOperation("Request an access token using credentials.", "")]
        public async Task<IActionResult> GetTokenAsync([FromBody] TokenRequest request, CancellationToken cancellationToken)
        {
            var validationResult = new TokenRequestValidator().Validate(request);

            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            var tokenResponse = await _tokenService.GetTokenAsync(request, GetIpAddress()!, cancellationToken);
            return Ok(tokenResponse);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [TenantIdHeader]
        [OpenApiOperation("Request an access token using a refresh token.", "")]
        [ApiConventionMethod(typeof(FSHApiConventions), nameof(FSHApiConventions.Search))]
        public async Task<IActionResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
        {
            var validationResult = new RefreshTokenRequestValidator().Validate(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            var tokenResponse = _tokenService.RefreshTokenAsync(request, GetIpAddress()!);
            return Ok(tokenResponse);
        }

        public string? GetIpAddress() =>
            Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"]
                : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
    }
}
