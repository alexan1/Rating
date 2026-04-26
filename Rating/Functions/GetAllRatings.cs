using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Rating.Model;

namespace Rating.Functions
{
    public class GetAllRatings
    {
        private readonly IMongoCollection<Model.Rating> _ratings;
        private readonly ILogger<GetAllRatings> _logger;

        public GetAllRatings(
            IMongoClient mongoClient,
            ILogger<GetAllRatings> logger)
        {
            _logger = logger;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [Function(nameof(GetAllRatings))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ratings")] HttpRequestData req)
        {
            try
            {
                using var cursor = await _ratings.FindAsync(Builders<Model.Rating>.Filter.Empty);
                var result = await cursor.ToListAsync();
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
