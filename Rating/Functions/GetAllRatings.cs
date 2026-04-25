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
            MongoClient mongoClient,
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
                var result = await _ratings.Find(rating => true).ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(RatingSummaries.CreateAll(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
