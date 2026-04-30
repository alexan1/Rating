using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WorkerHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace Rating.Auth
{
    public sealed class AccessTokenValidator : IAccessTokenValidator
    {
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private readonly JwtSecurityTokenHandler _tokenHandler = new();
        private readonly B2CAuthenticationOptions _options;

        public AccessTokenValidator(string authority, string audience)
            : this(
                new B2CAuthenticationOptions
                {
                    Authority = NormalizeAuthority(authority),
                    Audience = audience?.Trim()
                },
                CreateConfigurationManager(authority))
        {
        }

        public AccessTokenValidator(
            B2CAuthenticationOptions options,
            IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
        {
            _options = options;
            _configurationManager = configurationManager;
        }

        public async Task<TokenValidationOutcome> ValidateAsync(WorkerHttpRequestData request, CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured)
            {
                return TokenValidationOutcome.ServerError("Authentication is not configured.");
            }

            if (!TryGetBearerToken(request, out var accessToken))
            {
                return TokenValidationOutcome.Unauthorized("A valid Bearer token is required.");
            }

            OpenIdConnectConfiguration configuration;
            try
            {
                configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
            }
            catch (Exception)
            {
                return TokenValidationOutcome.ServerError("Unable to load authentication configuration.");
            }

            try
            {
                return TokenValidationOutcome.Success(ValidateToken(accessToken, configuration));
            }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                _configurationManager.RequestRefresh();
                try
                {
                    configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
                    return TokenValidationOutcome.Success(ValidateToken(accessToken, configuration));
                }
                catch (SecurityTokenException)
                {
                    return TokenValidationOutcome.Unauthorized("Bearer token is invalid.");
                }
                catch (ArgumentException)
                {
                    return TokenValidationOutcome.Unauthorized("Bearer token is invalid.");
                }
            }
            catch (SecurityTokenException)
            {
                return TokenValidationOutcome.Unauthorized("Bearer token is invalid.");
            }
            catch (ArgumentException)
            {
                return TokenValidationOutcome.Unauthorized("Bearer token is invalid.");
            }
        }

        private ClaimsPrincipal ValidateToken(string accessToken, OpenIdConnectConfiguration configuration)
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                ValidIssuers = new[]
                {
                    configuration.Issuer,
                    _options.Authority
                }.Where(value => !string.IsNullOrWhiteSpace(value)),
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            return _tokenHandler.ValidateToken(accessToken, validationParameters, out _);
        }

        private static bool TryGetBearerToken(WorkerHttpRequestData request, out string accessToken)
        {
            accessToken = null;

            if (!request.Headers.TryGetValues("Authorization", out var authorizationHeaders))
            {
                return false;
            }

            var authorizationHeader = authorizationHeaders.FirstOrDefault();
            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue) ||
                !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(headerValue.Parameter))
            {
                return false;
            }

            accessToken = headerValue.Parameter;
            return true;
        }

        private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string authority)
        {
            var normalizedAuthority = NormalizeAuthority(authority) ?? "https://invalid.local";
            var metadataAddress = $"{normalizedAuthority}/.well-known/openid-configuration";
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
        }

        private static string NormalizeAuthority(string authority)
        {
            return authority?.Trim().TrimEnd('/');
        }
    }
}
