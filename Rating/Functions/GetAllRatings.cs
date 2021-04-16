using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using MongoMusic.API.Helpers;
using MongoDB.Bson;

namespace Rating
{
    public class GetAllRatings
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Rating> _ratings;

        public GetAllRatings(
            MongoClient mongoClient,
            ILogger<GetAllRatings> logger,
            IConfiguration config)
        {
            _mongoClient = mongoClient;
            _logger = logger;
            _config = config;

            var database = _mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(GetAllRatings))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Ratings")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                var num = _ratings.CountDocuments(new BsonDocument());
                   
                var result = _ratings.Find(rating => true).ToList();

                if (result == null)
                {
                    _logger.LogInformation($"There are no ratings in the collection");
                    returnValue = new NotFoundResult();
                }
                else
                {
                    returnValue = new OkObjectResult(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }
    }
}
