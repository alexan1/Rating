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
    public class GetRating
    {
        private readonly IMongoCollection<Model.Rating> _ratings;
        private readonly ILogger<GetRating> _logger;

        public GetRating(
            MongoClient mongoClient,
            ILogger<GetRating> logger)
        {
            _logger = logger;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [Function(nameof(GetRating))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var result = await _ratings.Find(rating => rating.PersonId == id).ToListAsync();
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
