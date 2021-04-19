using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using MongoMusic.API.Helpers;

namespace Rating
{
    public class CreateRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Rating> _ratings;
        
        public CreateRating(
            MongoClient mongoClient,
            ILogger<CreateRating> logger,
            IConfiguration config)
        {
            _mongoClient = mongoClient;
            _logger = logger;
            _config = config;

            var database = _mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(CreateRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rating")] HttpRequest req)
        {
            IActionResult returnValue = null;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var rating = JsonConvert.DeserializeObject<Rating>(requestBody);            

            try
            {
                _ratings.InsertOne(rating);
                returnValue = new OkObjectResult(rating);
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
