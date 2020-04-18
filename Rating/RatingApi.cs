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
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using static Rating.Products;
using System;

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

        [FunctionName("ResetDemo")]
        public static async Task<IActionResult> RunGet([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {
            try
            {

                var client = new MongoClient(System.Environment.GetEnvironmentVariable("MongoDBAtlasConnectionString"));
                var database = client.GetDatabase("MongoOnlineGrocery");
                var collection = database.GetCollection<Product>("inventory");
                //We could also just drop the collection
                await collection.DeleteManyAsync(new BsonDocument { });
                await collection.Indexes.DropAllAsync();

                //Create an index on the PLU, this will also enforce uniqueness
                IndexKeysDefinition<Product> keys = "{ PLU: 1 }";
                var indexModel = new CreateIndexModel<Product>(keys);
                await collection.Indexes.CreateOneAsync(indexModel);

                //Create a default rating of 3 for new products
                var MyRating = new ProductRating();
                MyRating.Rating = 3;
                var DefaultProductRating = new List<ProductRating>();
                DefaultProductRating.Add(MyRating);

                //Define some sample objects
                var Bananas = new Product
                {
                    Id = new ObjectId(),
                    PLU = 4011,
                    Description = "Bananas",
                    Ratings = DefaultProductRating
                };
                var Apples = new Product
                {
                    Id = new ObjectId(),
                    PLU = 3283,
                    Description = "Apples",
                    Ratings = DefaultProductRating
                };

                //MongoDB makes it easy to go from object to database with no ORM layer
                await collection.InsertOneAsync(Bananas);
                await collection.InsertOneAsync(Apples);

            }
            catch (Exception e)
            {
                return new BadRequestObjectResult("Error refreshing demo - " + e.Message);

            }

            return (ActionResult)new OkObjectResult("Refreshed Demo database");
        }
    }
}
