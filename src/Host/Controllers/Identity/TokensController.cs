using FSH.WebApi.Application.Identity.Tokens;
using FluentValidation;

namespace FSH.WebApi.Host.Controllers.Identity
{
    public sealed class TokensController : VersionNeutralApiController
    {
        private readonly ITokenService _tokenService;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IWebHostEnvironment _environment;

        public TokensController(ITokenService tokenService, IHttpClientFactory clientFactory, IWebHostEnvironment environment)
        {
            _tokenService = tokenService;
            _clientFactory = clientFactory;
            _environment = environment;
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

        [HttpGet("address")]
        [AllowAnonymous]
        [TenantIdHeader]
        [OpenApiOperation("Request an GoongApiKey using credentials.", "")]
        public async Task<IActionResult> Get([FromQuery] string input, [FromQuery] string sessiontoken)
        {
            string apiKey = string.Empty;
            if (_environment.IsDevelopment())
            {
                apiKey = "DhBdDGolfsnCGSObdipkyX3QpQNONdp5up8Ux4EH";
            }
            else
            {
                apiKey = "DXGBVWabBPbJ615lIqoh52BQoCMvdrWcLAt6XVRe";
            }
            var client = _clientFactory.CreateClient();

            var response = await client.GetAsync($"https://rsapi.goong.io/Place/AutoComplete?api_key={apiKey}&input={input}&sessiontoken={sessiontoken}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            return BadRequest("Unable to fetch address suggestions");
        }

        public string? GetIpAddress() =>
            Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"]
                : HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
    }
}
