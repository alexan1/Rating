using System.Net;
using System.Security.Claims;

namespace Rating.Auth
{
    public sealed record TokenValidationOutcome(
        bool IsAuthenticated,
        HttpStatusCode StatusCode,
        string ErrorMessage,
        ClaimsPrincipal Principal = null)
    {
        public static TokenValidationOutcome Success(ClaimsPrincipal principal) =>
            new(true, HttpStatusCode.OK, null, principal);

        public static TokenValidationOutcome Unauthorized(string errorMessage) =>
            new(false, HttpStatusCode.Unauthorized, errorMessage);

        public static TokenValidationOutcome ServerError(string errorMessage) =>
            new(false, HttpStatusCode.InternalServerError, errorMessage);
    }
}
