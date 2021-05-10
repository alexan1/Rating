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
    public class GetRating
    {
        private readonly MongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<Model.Rating> _ratings;

        public GetRating(
            MongoClient mongoClient,
            ILogger<GetRating> logger,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _mongoClient = mongoClient;

            var database = _mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [FunctionName(nameof(GetRating))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating/{id}")] HttpRequest req,
            int id)
        {
            IActionResult returnValue;

            try
            {
                var result  = await _ratings.Find(rating => rating.PersonId == id).ToListAsync();

                if (result == null || result.Count < 1)
                {
                    _logger.LogWarning("That item doesn't exist!");
                    returnValue = new NotFoundResult();
                }
                else
                {
                    var viewresult = new ViewRating
                    {
                        PersonId = result.FirstOrDefault().PersonId,
                        AverageRate = result.Select(x => x.Rate).Average()
                    };
                    returnValue = new OkObjectResult(viewresult);
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
