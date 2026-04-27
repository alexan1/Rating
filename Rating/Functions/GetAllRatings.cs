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
    public class GetAllRatings
    {
        private readonly IDataStore _dataStore;
        private readonly ILogger<GetAllRatings> _logger;

        public GetAllRatings(
            IDataStore dataStore,
            ILogger<GetAllRatings> logger)
        {
            _dataStore = dataStore;
            _logger = logger;
        }

        [Function(nameof(GetAllRatings))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ratings")] HttpRequestData req)
        {
            try
            {
                var result = await _dataStore.GetAllRatingsAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(RatingSummaries.CreateAll(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve all ratings.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
