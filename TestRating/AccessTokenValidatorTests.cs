using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rating.Auth;
using WorkerHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace TestRating
{
    [TestClass]
    public class AccessTokenValidatorTests
    {
        [TestMethod]
        public async Task ValidateAsyncReturnsUnauthorizedWhenAuthorizationHeaderIsMissing()
        {
            var validator = CreateValidator(CreateConfiguration());
            var request = CreateRequest();

            var outcome = await validator.ValidateAsync(request);

            Assert.IsFalse(outcome.IsAuthenticated);
            Assert.AreEqual(HttpStatusCode.Unauthorized, outcome.StatusCode);
            Assert.AreEqual("A valid Bearer token is required.", outcome.ErrorMessage);
        }

        [TestMethod]
        public async Task ValidateAsyncReturnsServerErrorWhenB2CSettingsAreMissing()
        {
            var validator = new AccessTokenValidator(
                new B2CAuthenticationOptions(),
                new StaticConfigurationManager(CreateConfiguration()));
            var request = CreateRequest("Bearer token");

            var outcome = await validator.ValidateAsync(request);

            Assert.IsFalse(outcome.IsAuthenticated);
            Assert.AreEqual(HttpStatusCode.InternalServerError, outcome.StatusCode);
            Assert.AreEqual("Authentication is not configured.", outcome.ErrorMessage);
        }

        [TestMethod]
        public async Task ValidateAsyncReturnsUnauthorizedWhenTokenIsInvalid()
        {
            var validator = CreateValidator(CreateConfiguration());
            var request = CreateRequest("Bearer not-a-jwt");

            var outcome = await validator.ValidateAsync(request);

            Assert.IsFalse(outcome.IsAuthenticated);
            Assert.AreEqual(HttpStatusCode.Unauthorized, outcome.StatusCode);
            Assert.AreEqual("Bearer token is invalid.", outcome.ErrorMessage);
        }

        [TestMethod]
        public async Task ValidateAsyncReturnsServerErrorWhenRefreshConfigurationLoadFails()
        {
            var initialConfiguration = CreateConfiguration();
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("FEDCBA9876543210FEDCBA9876543210"));
            var token = CreateToken(initialConfiguration.Issuer, "rating-api", signingKey, new Claim("sub", "user-123"));
            var validator = new AccessTokenValidator(
                new B2CAuthenticationOptions
                {
                    Authority = initialConfiguration.Issuer.TrimEnd('/'),
                    Audience = "rating-api"
                },
                new RefreshFailingConfigurationManager(initialConfiguration, new InvalidOperationException("network failure")));
            var request = CreateRequest($"Bearer {token}");

            var outcome = await validator.ValidateAsync(request);

            Assert.IsFalse(outcome.IsAuthenticated);
            Assert.AreEqual(HttpStatusCode.InternalServerError, outcome.StatusCode);
            Assert.AreEqual("Unable to load authentication configuration.", outcome.ErrorMessage);
        }

        [TestMethod]
        public async Task ValidateAsyncReturnsPrincipalWhenTokenIsValid()
        {
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF"));
            var configuration = CreateConfiguration(signingKey);
            var validator = CreateValidator(configuration);
            var token = CreateToken(configuration.Issuer, "rating-api", signingKey, new Claim("sub", "user-123"));
            var request = CreateRequest($"Bearer {token}");

            var outcome = await validator.ValidateAsync(request);
            var subject = outcome.Principal.FindFirst("sub")?.Value ??
                outcome.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Assert.IsTrue(outcome.IsAuthenticated);
            Assert.AreEqual(HttpStatusCode.OK, outcome.StatusCode);
            Assert.IsNotNull(outcome.Principal);
            Assert.AreEqual("user-123", subject);
        }

        private static AccessTokenValidator CreateValidator(OpenIdConnectConfiguration configuration)
        {
            return new AccessTokenValidator(
                new B2CAuthenticationOptions
                {
                    Authority = configuration.Issuer.TrimEnd('/'),
                    Audience = "rating-api"
                },
                new StaticConfigurationManager(configuration));
        }

        private static OpenIdConnectConfiguration CreateConfiguration(SecurityKey signingKey = null)
        {
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = "https://example.b2clogin.com/example.onmicrosoft.com/B2C_1_signin/v2.0/"
            };

            configuration.SigningKeys.Add(signingKey ?? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF")));
            return configuration;
        }

        private static string CreateToken(string issuer, string audience, SecurityKey signingKey, params Claim[] claims)
        {
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static WorkerHttpRequestData CreateRequest(string authorizationHeader = null)
        {
            var context = CreateFunctionContext();
            var requestMock = new Mock<WorkerHttpRequestData>(context);
            var headers = new HttpHeadersCollection();

            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                headers.Add("Authorization", authorizationHeader);
            }

            requestMock.SetupGet(x => x.Body).Returns(new System.IO.MemoryStream(Array.Empty<byte>()));
            requestMock.SetupGet(x => x.Headers).Returns(headers);
            requestMock.SetupGet(x => x.Cookies).Returns(Array.Empty<IHttpCookie>());
            requestMock.SetupGet(x => x.Url).Returns(new Uri("https://localhost/api/rating"));
            requestMock.SetupGet(x => x.Identities).Returns(Array.Empty<ClaimsIdentity>());
            requestMock.SetupGet(x => x.Method).Returns("PUT");

            return requestMock.Object;
        }

        private static FunctionContext CreateFunctionContext()
        {
            var services = new ServiceCollection();
            services
                .AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());
            var serviceProvider = services.BuildServiceProvider();

            var contextMock = new Mock<FunctionContext>();
            contextMock.SetupProperty(x => x.InstanceServices, serviceProvider);

            return contextMock.Object;
        }

        private sealed class StaticConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
        {
            private readonly OpenIdConnectConfiguration _configuration;

            public StaticConfigurationManager(OpenIdConnectConfiguration configuration)
            {
                _configuration = configuration;
            }

            public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            {
                return Task.FromResult(_configuration);
            }

            public void RequestRefresh()
            {
            }
        }

        private sealed class RefreshFailingConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
        {
            private readonly OpenIdConnectConfiguration _configuration;
            private readonly Exception _refreshException;
            private bool _wasRefreshed;

            public RefreshFailingConfigurationManager(OpenIdConnectConfiguration configuration, Exception refreshException)
            {
                _configuration = configuration;
                _refreshException = refreshException;
            }

            public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            {
                if (_wasRefreshed)
                {
                    throw _refreshException;
                }

                return Task.FromResult(_configuration);
            }

            public void RequestRefresh()
            {
                _wasRefreshed = true;
            }
        }
    }
}
