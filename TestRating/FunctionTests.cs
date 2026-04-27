using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rating;
using Rating.Data;
using Rating.Functions;
using Rating.Model;
using RatingModel = Rating.Model.Rating;

namespace TestRating
{
    [TestClass]
    public class FunctionTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [TestMethod]
        public async Task GetRatingReturnsAverageForRequestedPerson()
        {
            var ratings = new List<RatingModel>
            {
                new() { PersonId = 7, UserId = "a", Rate = 8 },
                new() { PersonId = 7, UserId = "b", Rate = 4 }
            };
            var store = CreateDataStoreContext(ratings);

            var function = new GetRating(store.StoreMock.Object, Mock.Of<ILogger<GetRating>>());
            var request = CreateRequest(null);

            var response = await function.Run(request, 7);
            var body = await ReadBodyAsync<ViewRating>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(7, body.PersonId);
            Assert.AreEqual(6.0, body.AverageRate);
        }

        [TestMethod]
        public async Task GetRatingReturnsInternalServerErrorWhenQueryFails()
        {
            var store = CreateDataStoreContext();
            store.StoreMock
                .Setup(x => x.FindRatingsByPersonIdAsync(It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var function = new GetRating(store.StoreMock.Object, Mock.Of<ILogger<GetRating>>());
            var request = CreateRequest(null);

            var response = await function.Run(request, 7);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [TestMethod]
        public async Task GetAllRatingsReturnsGroupedSummaries()
        {
            var ratings = new List<RatingModel>
            {
                new() { PersonId = 1, UserId = "a", Rate = 10 },
                new() { PersonId = 1, UserId = "b", Rate = 6 },
                new() { PersonId = 2, UserId = "c", Rate = 5 }
            };
            var store = CreateDataStoreContext(ratings);

            var function = new GetAllRatings(store.StoreMock.Object, Mock.Of<ILogger<GetAllRatings>>());
            var request = CreateRequest(null);

            var response = await function.Run(request);
            var body = await ReadBodyAsync<List<ViewRating>>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(2, body.Count);
            Assert.AreEqual(8.0, body.Find(x => x.PersonId == 1)?.AverageRate);
            Assert.AreEqual(5.0, body.Find(x => x.PersonId == 2)?.AverageRate);
        }

        [TestMethod]
        public async Task GetAllRatingsReturnsInternalServerErrorWhenQueryFails()
        {
            var store = CreateDataStoreContext();
            store.StoreMock
                .Setup(x => x.GetAllRatingsAsync())
                .ThrowsAsync(new InvalidOperationException("boom"));

            var function = new GetAllRatings(store.StoreMock.Object, Mock.Of<ILogger<GetAllRatings>>());
            var request = CreateRequest(null);

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenBodyIsNull()
        {
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(null, "PUT");

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task UpsertRatingInsertsNewRatingWhenNoExistingRecord()
        {
            var newRating = new RatingModel { PersonId = 9, UserId = "alex", Rate = 7 };
            var store = CreateDataStoreContext(Array.Empty<RatingModel>());

            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(newRating, "PUT");

            var response = await function.Run(request);
            var body = await ReadBodyAsync<RatingModel>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(9, body.PersonId);
            Assert.AreEqual("alex", body.UserId);
            Assert.AreEqual(7, body.Rate);
        }

        [TestMethod]
        public async Task UpsertRatingDeletesExistingRatingWhenRateIsZero()
        {
            var existing = new RatingModel { Id = "507f1f77bcf86cd799439011", PersonId = 9, UserId = "alex", Rate = 4 };
            var update = new RatingModel { PersonId = 9, UserId = "alex", Rate = 0 };
            var store = CreateDataStoreContext(new[] { existing });

            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(update, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("null", bodyText);
        }

        [TestMethod]
        public async Task UpsertRatingReplacesExistingRatingWhenRecordExists()
        {
            var existing = new RatingModel { Id = "507f1f77bcf86cd799439011", PersonId = 9, UserId = "alex", Rate = 4 };
            var update = new RatingModel { PersonId = 9, UserId = "alex", Rate = 8 };
            var store = CreateDataStoreContext(new[] { existing });

            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(update, "PUT");

            var response = await function.Run(request);
            var body = await ReadBodyAsync<RatingModel>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(existing.Id, body.Id);
            Assert.AreEqual(8, body.Rate);
        }

        [TestMethod]
        public async Task UpsertRatingReturnsInternalServerErrorWhenDatabaseWriteFails()
        {
            var newRating = new RatingModel { PersonId = 9, UserId = "alex", Rate = 7 };
            var store = CreateDataStoreContext(Array.Empty<RatingModel>());
            store.StoreMock
                .Setup(x => x.CreateRatingAsync(It.IsAny<RatingModel>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(newRating, "PUT");

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [TestMethod]
        public async Task GetRatingReturnsBadRequestWhenPersonIdIsZero()
        {
            var store = CreateDataStoreContext();
            var function = new GetRating(store.StoreMock.Object, Mock.Of<ILogger<GetRating>>());
            var request = CreateRequest(null);

            var response = await function.Run(request, 0);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("PersonId must be greater than 0", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task GetRatingReturnsBadRequestWhenPersonIdIsNegative()
        {
            var store = CreateDataStoreContext();
            var function = new GetRating(store.StoreMock.Object, Mock.Of<ILogger<GetRating>>());
            var request = CreateRequest(null);

            var response = await function.Run(request, -5);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("PersonId must be greater than 0", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenBodyIsNullWithErrorPayload()
        {
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(null, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("Request body is required.", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenPersonIdIsZero()
        {
            var invalidRating = new RatingModel { PersonId = 0, UserId = "alex", Rate = 7 };
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("PersonId must be greater than 0", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenRateIsInvalid()
        {
            var invalidRating = new RatingModel { PersonId = 9, UserId = "alex", Rate = 15 };
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("Rate must be 0 (to delete) or between 1 and 10", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenUserIdIsEmpty()
        {
            var invalidRating = new RatingModel { PersonId = 9, UserId = "", Rate = 7 };
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("UserId cannot be empty or whitespace", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenUserIdIsWhitespace()
        {
            var invalidRating = new RatingModel { PersonId = 9, UserId = "   ", Rate = 7 };
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating, "PUT");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("UserId cannot be empty or whitespace", json.GetProperty("error").GetString());
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenJsonIsMalformed()
        {
            var store = CreateDataStoreContext();
            var function = new UpsertRating(store.StoreMock.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(null, "PUT", "{");

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("Request body contains invalid JSON.", json.GetProperty("error").GetString());
        }

        private static HttpRequestData CreateRequest(object body, string method = "GET", string rawBody = null)
        {
            var context = CreateFunctionContext();
            var response = CreateResponse(context);
            var requestMock = new Mock<HttpRequestData>(context);
            requestMock.SetupGet(x => x.Body).Returns(CreateBodyStream(body, rawBody));
            requestMock.SetupGet(x => x.Headers).Returns(new HttpHeadersCollection());
            requestMock.SetupGet(x => x.Cookies).Returns(Array.Empty<IHttpCookie>());
            requestMock.SetupGet(x => x.Url).Returns(new Uri("https://localhost/api/rating"));
            requestMock.SetupGet(x => x.Identities).Returns(Array.Empty<ClaimsIdentity>());
            requestMock.SetupGet(x => x.Method).Returns(method);
            requestMock.Setup(x => x.CreateResponse()).Returns(response);

            return requestMock.Object;
        }

        private static HttpResponseData CreateResponse(FunctionContext context)
        {
            var responseMock = new Mock<HttpResponseData>(context);
            responseMock.SetupProperty(x => x.StatusCode);
            responseMock.SetupProperty(x => x.Body, new MemoryStream());
            responseMock.SetupGet(x => x.Headers).Returns(new HttpHeadersCollection());
            responseMock.SetupGet(x => x.Cookies).Returns(Mock.Of<HttpCookies>());

            return responseMock.Object;
        }

        private static FunctionContext CreateFunctionContext()
        {
            var services = new ServiceCollection();
            services
                .AddOptions<WorkerOptions>()
                .Configure(options => options.Serializer = new JsonObjectSerializer());
            var serviceProvider = services.BuildServiceProvider();

            var contextMock = new Mock<FunctionContext>();
            contextMock.SetupProperty(x => x.InstanceServices, serviceProvider);

            return contextMock.Object;
        }

        private static MemoryStream CreateBodyStream(object body, string rawBody = null)
        {
            var json = rawBody ?? JsonSerializer.Serialize(body);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private static async Task<T> ReadBodyAsync<T>(HttpResponseData response)
        {
            response.Body.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(response.Body, JsonOptions);
        }

        private static async Task<string> ReadBodyTextAsync(HttpResponseData response)
        {
            response.Body.Position = 0;
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private sealed record DataStoreContext(
            Mock<IDataStore> StoreMock);

        private static DataStoreContext CreateDataStoreContext(IEnumerable<RatingModel> queryResults = null)
        {
            var storeMock = new Mock<IDataStore>();
            var results = new List<RatingModel>(queryResults ?? Array.Empty<RatingModel>());

            storeMock
                .Setup(x => x.FindRatingsByPersonIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int personId) => results.FindAll(r => r.PersonId == personId));

            storeMock
                .Setup(x => x.GetAllRatingsAsync())
                .ReturnsAsync(results);

            storeMock
                .Setup(x => x.FindRatingAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((int personId, string userId) => results.FirstOrDefault(r => r.PersonId == personId && r.UserId == userId));

            storeMock
                .Setup(x => x.CreateRatingAsync(It.IsAny<RatingModel>()))
                .Callback<RatingModel>(r => 
                {
                    r.Id = Guid.NewGuid().ToString();
                    results.Add(r);
                })
                .Returns(Task.CompletedTask);

            storeMock
                .Setup(x => x.UpdateRatingAsync(It.IsAny<RatingModel>()))
                .Callback<RatingModel>(r =>
                {
                    var existing = results.FirstOrDefault(x => x.Id == r.Id);
                    if (existing != null)
                    {
                        results.Remove(existing);
                        results.Add(r);
                    }
                })
                .Returns(Task.CompletedTask);

            storeMock
                .Setup(x => x.DeleteRatingAsync(It.IsAny<string>(), It.IsAny<int>()))
                .Callback<string, int>((id, personId) =>
                {
                    var toRemove = results.FirstOrDefault(r => r.Id == id);
                    if (toRemove != null)
                        results.Remove(toRemove);
                })
                .Returns(Task.CompletedTask);

            return new DataStoreContext(storeMock);
        }
    }
}
