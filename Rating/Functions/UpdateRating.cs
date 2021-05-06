using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace Rating.Functions
{
    public class UpdateRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Model.Rating> _ratings;

        public UpdateRating(
            IMongoClient mongoClient,
            ILogger<UpdateRating> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(UpdateRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rating/{id}")] HttpRequest req,
            int id)
        {
            IActionResult returnValue = null;

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var updatedResult = JsonConvert.DeserializeObject<Model.Rating>(requestBody);

            updatedResult.PersonId = id;

            try
            {
                var replacedItem = await _ratings.ReplaceOneAsync(rating => rating.PersonId == id, updatedResult);

                if (replacedItem == null)
                {
                    returnValue = new NotFoundResult();
                }
                else
                {
                    returnValue = new OkObjectResult(updatedResult);
                }              
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not update Rating with id: {id}. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return returnValue;
        }
    }
}
