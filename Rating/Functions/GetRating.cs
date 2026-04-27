using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Rating;
using Rating.Data;
using Rating.Model;

namespace Rating.Functions
{
    public class GetRating
    {
        private readonly IDataStore _dataStore;
        private readonly ILogger<GetRating> _logger;

        public GetRating(
            IDataStore dataStore,
            ILogger<GetRating> logger)
        {
            _dataStore = dataStore;
            _logger = logger;
        }

        [Function(nameof(GetRating))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                // Validate PersonId
                if (id <= 0)
                {
                    _logger.LogWarning("Invalid PersonId requested: {PersonId}", id);
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteAsJsonAsync(new { error = "PersonId must be greater than 0" });
                    return badRequestResponse;
                }

                var result = await _dataStore.FindRatingsByPersonIdAsync(id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(RatingSummaries.CreateForPerson(id, result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve Rating for PersonId: {PersonId}.", id);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
