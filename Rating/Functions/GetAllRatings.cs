using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Rating.Model;

namespace Rating.Functions
{
    public class GetAllRatings
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Model.Rating> _ratings;

        public GetAllRatings(
            MongoClient mongoClient,
            ILogger<GetAllRatings> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _mongoClient = mongoClient;

            var database = _mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(GetAllRatings))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ratings")] HttpRequest req)
        {
            IActionResult returnValue = null;

            try
            {
                //var num = await _ratings.CountDocumentsAsync(new BsonDocument());
                   
                var result = await _ratings.Find(rating => true).ToListAsync();

                if (result == null)
                {
                    _logger.LogInformation($"There are no ratings in the collection");
                    returnValue = new NotFoundResult();
                }
                else
                {
                    var viewresult = result.GroupBy(x => x.PersonId).Select(p => new ViewRating{PersonId = p.Key, AverageRate = p.Average(z => z.Rate)});
                    returnValue = new OkObjectResult(viewresult);
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
