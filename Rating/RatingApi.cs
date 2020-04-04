using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Rating
{
    public static class RatingApi
    {
        static List<Rating> ratings = new List<Rating>();

        [FunctionName("CreateRating")]
        public static async Task<IActionResult> CreateRating(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "rating")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("creating new rating");
            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic rate = JsonConvert.DeserializeObject<Rating>(requestBody);
            
            ratings.Add(rate);     

            return new OkObjectResult(rate);
        }

        [FunctionName("GetRatings")]
        public static IActionResult GetRatings(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating")]HttpRequest req, ILogger log)
        {
            log.LogInformation("Getting all ratings");
            return new OkObjectResult(ratings);
        }

        [FunctionName("GeRatingById")]
        public static IActionResult GetTodoById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rating/{id}")]HttpRequest req, ILogger log, int id)
        {
            var todo = ratings.FirstOrDefault(t => t.PersonID == id);
            if (todo == null)
            {
                return new NotFoundResult();
            }
            return new OkObjectResult(todo);
        }

        [FunctionName("UpdateRatin")]
        public static async Task<IActionResult> UpdateRating(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rating/{id}")]HttpRequest req, ILogger log, int id)
        {
            var rate = ratings.FirstOrDefault(t => t.PersonID == id);
            if (rate == null)
            {
                return new NotFoundResult();
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<Rating>(requestBody);
                        
            //ratings.r

            return new OkObjectResult(rate);
        }
    }
}
