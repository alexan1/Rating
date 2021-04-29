using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Rating.Functions
{
    public class GetRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Rating> _ratings;

        public GetRating(
            MongoClient mongoClient,
            ILogger<GetRating> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(GetRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating/{id}")] HttpRequest req,
            int id)
        {
            IActionResult returnValue = null;

            try
            {
                var result  = await _ratings.Find(rating => rating.PersonId == id).FirstOrDefaultAsync();

                if (result == null)
                {
                    _logger.LogWarning("That item doesn't exist!");
                    returnValue = new NotFoundResult();
                }
                else
                {
                    returnValue = new OkObjectResult(result);
                }               
            }
            catch (Exception ex)
            {
                _logger.LogError($"Couldn't find Rating with id: {id}. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }
    }
}
