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
    public class UpsertRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Model.Rating> _ratings;

        public UpsertRating(
            MongoClient mongoClient,
            ILogger<UpsertRating> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(UpsertRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rating")] HttpRequest req)
        {
            IActionResult returnValue = null;
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var rating = JsonConvert.DeserializeObject<Model.Rating>(requestBody);

            try
            {
                var exrating = await _ratings.Find(ex => ex.PersonId == rating.PersonId && ex.UserId == rating.UserId).FirstOrDefaultAsync();

                if (exrating == null)
                {
                    await _ratings.InsertOneAsync(rating);
                    returnValue = new OkObjectResult(rating);
                }
                else
                {
                    rating.Id = exrating.Id;
                    await _ratings.ReplaceOneAsync(r => r.Id == exrating.Id, rating);
                    returnValue = new OkObjectResult(rating);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not update Rating Person: {rating.PersonId} by User: {rating.UserId}. Exception thrown: {ex.Message}");
                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            finally
            {

            }

            return new OkObjectResult(returnValue);
        }
    }
}
