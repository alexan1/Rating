using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Rating.Functions
{
    public class UpsertRating
    {
        private readonly IMongoCollection<Model.Rating> _ratings;
        private readonly ILogger<UpsertRating> _logger;

        public UpsertRating(
            IMongoClient mongoClient,
            ILogger<UpsertRating> logger)
        {
            _logger = logger;

            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _ratings = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        [Function(nameof(UpsertRating))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rating")] HttpRequestData req)
        {
            var rating = await req.ReadFromJsonAsync<Model.Rating>();

            try
            {
                if (rating == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                // Validate input
                var (isValid, errorMessage) = RatingValidator.Validate(rating);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid rating submission: {ErrorMessage}", errorMessage);
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteAsJsonAsync(new { error = errorMessage });
                    return response;
                }

                var filter = Builders<Model.Rating>.Filter.Where(ex => ex.PersonId == rating.PersonId && ex.UserId == rating.UserId);
                using var cursor = await _ratings.FindAsync(filter);
                var exrating = (await cursor.ToListAsync()).FirstOrDefault();

                if (exrating == null)
                {
                    await _ratings.InsertOneAsync(rating);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(rating);
                    return response;
                }

                if (rating.Rate == 0)
                {
                    await _ratings.DeleteOneAsync(r => r.Id == exrating.Id);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync<object>(null);
                    return response;
                }

                rating.Id = exrating.Id;
                await _ratings.ReplaceOneAsync(r => r.Id == exrating.Id, rating);

                var updateResponse = req.CreateResponse(HttpStatusCode.OK);
                await updateResponse.WriteAsJsonAsync(rating);
                return updateResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not update Rating Person: {PersonId} by User: {UserId}.", rating?.PersonId, rating?.UserId);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
