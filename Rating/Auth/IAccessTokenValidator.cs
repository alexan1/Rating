using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;

namespace Rating.Auth
{
    public interface IAccessTokenValidator
    {
        Task<TokenValidationOutcome> ValidateAsync(HttpRequestData request, CancellationToken cancellationToken = default);
    }
}
