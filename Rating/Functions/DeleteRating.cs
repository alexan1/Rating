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
    public class DeleteRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Model.Rating> _ratings;

        public DeleteRating(
            IMongoClient mongoClient,
            ILogger<DeleteRating> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(DeleteRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "rating/{id}")] HttpRequest req,
            int id)
        {
            IActionResult returnValue = null;

            try
            {
                var ratingToDelete = await _ratings.DeleteOneAsync(rating => rating.PersonId == id);

                if (ratingToDelete == null)
                {
                    _logger.LogInformation($"Rating with id: {id} does not exist. Delete failed");
                    returnValue = new StatusCodeResult(StatusCodes.Status404NotFound);
                }

                returnValue = new StatusCodeResult(StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not delete item. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }
    }
}
