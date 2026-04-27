using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Rating;
using Rating.Data;

namespace Rating.Functions
{
    public class UpsertRating
    {
        private readonly IDataStore _dataStore;
        private readonly ILogger<UpsertRating> _logger;

        public UpsertRating(
            IDataStore dataStore,
            ILogger<UpsertRating> logger)
        {
            _dataStore = dataStore;
            _logger = logger;
        }

        [Function(nameof(UpsertRating))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "rating")] HttpRequestData req)
        {
            Model.Rating rating;

            try
            {
                rating = await req.ReadFromJsonAsync<Model.Rating>();
            }
            catch (Exception ex) when (IsInvalidRequestPayload(ex))
            {
                _logger.LogWarning(ex, "Invalid rating submission: malformed JSON payload.");
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { error = "Request body contains invalid JSON." });
                return response;
            }

            try
            {
                if (rating == null)
                {
                    _logger.LogWarning("Invalid rating submission: request body is null.");
                    var response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteAsJsonAsync(new { error = "Request body is required." });
                    return response;
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

                var existing = await _dataStore.FindRatingAsync(rating.PersonId, rating.UserId);

                if (existing == null)
                {
                    await _dataStore.CreateRatingAsync(rating);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(rating);
                    return response;
                }

                if (rating.Rate == 0)
                {
                    await _dataStore.DeleteRatingAsync(existing.Id, existing.PersonId);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync<object>(null);
                    return response;
                }

                rating.Id = existing.Id;
                await _dataStore.UpdateRatingAsync(rating);

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

        private static bool IsInvalidRequestPayload(Exception exception)
        {
            while (exception != null)
            {
                if (exception is JsonException || exception is FormatException || exception is InvalidOperationException || exception is NotSupportedException)
                {
                    return true;
                }

                exception = exception.InnerException;
            }

            return false;
        }
    }
}
