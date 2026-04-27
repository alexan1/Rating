using System;
using System.Collections.Generic;
using System.IO;
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
using MongoDB.Driver;
using Rating;
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
            var mongo = CreateMongoContext(ratings);

            var function = new GetRating(mongo.Client.Object, Mock.Of<ILogger<GetRating>>());
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
            var mongo = CreateMongoContext();
            mongo.CollectionMock
                .Setup(x => x.FindAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.IsAny<FindOptions<RatingModel, RatingModel>>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("boom"));

            var function = new GetRating(mongo.Client.Object, Mock.Of<ILogger<GetRating>>());
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
            var mongo = CreateMongoContext(ratings);

            var function = new GetAllRatings(mongo.Client.Object, Mock.Of<ILogger<GetAllRatings>>());
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
            var mongo = CreateMongoContext();
            mongo.CollectionMock
                .Setup(x => x.FindAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.IsAny<FindOptions<RatingModel, RatingModel>>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("boom"));

            var function = new GetAllRatings(mongo.Client.Object, Mock.Of<ILogger<GetAllRatings>>());
            var request = CreateRequest(null);

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [TestMethod]
        public async Task UpsertRatingReturnsBadRequestWhenBodyIsNull()
        {
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(null);

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            mongo.CollectionMock.Verify(
                x => x.FindAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.IsAny<FindOptions<RatingModel, RatingModel>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestMethod]
        public async Task UpsertRatingInsertsNewRatingWhenNoExistingRecord()
        {
            var newRating = new RatingModel { PersonId = 9, UserId = "alex", Rate = 7 };
            var mongo = CreateMongoContext(Array.Empty<RatingModel>());
            mongo.CollectionMock
                .Setup(x => x.InsertOneAsync(
                    It.Is<RatingModel>(r => r.PersonId == 9 && r.UserId == "alex" && r.Rate == 7),
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(newRating);

            var response = await function.Run(request);
            var body = await ReadBodyAsync<RatingModel>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(9, body.PersonId);
            Assert.AreEqual("alex", body.UserId);
            Assert.AreEqual(7, body.Rate);
            mongo.CollectionMock.Verify(
                x => x.InsertOneAsync(
                    It.Is<RatingModel>(r => r.PersonId == 9 && r.UserId == "alex" && r.Rate == 7),
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task UpsertRatingDeletesExistingRatingWhenRateIsZero()
        {
            var existing = new RatingModel { Id = "507f1f77bcf86cd799439011", PersonId = 9, UserId = "alex", Rate = 4 };
            var update = new RatingModel { PersonId = 9, UserId = "alex", Rate = 0 };
            var mongo = CreateMongoContext(new[] { existing });
            mongo.CollectionMock
                .Setup(x => x.DeleteOneAsync(It.IsAny<FilterDefinition<RatingModel>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeleteResult)null);

            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(update);

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("null", bodyText);
            mongo.CollectionMock.Verify(
                x => x.DeleteOneAsync(It.IsAny<FilterDefinition<RatingModel>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task UpsertRatingReplacesExistingRatingWhenRecordExists()
        {
            var existing = new RatingModel { Id = "507f1f77bcf86cd799439011", PersonId = 9, UserId = "alex", Rate = 4 };
            var update = new RatingModel { PersonId = 9, UserId = "alex", Rate = 8 };
            var mongo = CreateMongoContext(new[] { existing });
            mongo.CollectionMock
                .Setup(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.IsAny<RatingModel>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReplaceOneResult)null);

            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(update);

            var response = await function.Run(request);
            var body = await ReadBodyAsync<RatingModel>(response);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(body);
            Assert.AreEqual(existing.Id, body.Id);
            Assert.AreEqual(8, body.Rate);
            mongo.CollectionMock.Verify(
                x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.Is<RatingModel>(r => r.Id == existing.Id && r.PersonId == 9 && r.UserId == "alex" && r.Rate == 8),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task UpsertRatingReturnsInternalServerErrorWhenDatabaseWriteFails()
        {
            var newRating = new RatingModel { PersonId = 9, UserId = "alex", Rate = 7 };
            var mongo = CreateMongoContext(Array.Empty<RatingModel>());
            mongo.CollectionMock
                .Setup(x => x.InsertOneAsync(It.IsAny<RatingModel>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(newRating);

            var response = await function.Run(request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [TestMethod]
        public async Task GetRatingReturnsBadRequestWhenPersonIdIsZero()
        {
            var mongo = CreateMongoContext();
            var function = new GetRating(mongo.Client.Object, Mock.Of<ILogger<GetRating>>());
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
            var mongo = CreateMongoContext();
            var function = new GetRating(mongo.Client.Object, Mock.Of<ILogger<GetRating>>());
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
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(null);

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
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating);

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
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating);

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
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating);

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
            var mongo = CreateMongoContext();
            var function = new UpsertRating(mongo.Client.Object, Mock.Of<ILogger<UpsertRating>>());
            var request = CreateRequest(invalidRating);

            var response = await function.Run(request);
            var bodyText = await ReadBodyTextAsync(response);
            var json = JsonDocument.Parse(bodyText).RootElement;

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("UserId cannot be empty or whitespace", json.GetProperty("error").GetString());
        }

        private static MongoContext CreateMongoContext(IEnumerable<RatingModel> queryResults = null)
        {
            var collectionMock = new Mock<IMongoCollection<RatingModel>>();
            collectionMock
                .Setup(x => x.FindAsync(
                    It.IsAny<FilterDefinition<RatingModel>>(),
                    It.IsAny<FindOptions<RatingModel, RatingModel>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestAsyncCursor<RatingModel>(queryResults ?? Array.Empty<RatingModel>()));

            var databaseMock = new Mock<IMongoDatabase>();
            databaseMock
                .Setup(x => x.GetCollection<RatingModel>(Settings.COLLECTION_NAME, null))
                .Returns(collectionMock.Object);

            var clientMock = new Mock<IMongoClient>();
            clientMock
                .Setup(x => x.GetDatabase(Settings.DATABASE_NAME, null))
                .Returns(databaseMock.Object);

            return new MongoContext(clientMock, collectionMock);
        }

        private static HttpRequestData CreateRequest(object body)
        {
            var context = CreateFunctionContext();
            var response = CreateResponse(context);
            var requestMock = new Mock<HttpRequestData>(context);
            requestMock.SetupGet(x => x.Body).Returns(CreateBodyStream(body));
            requestMock.SetupGet(x => x.Headers).Returns(new HttpHeadersCollection());
            requestMock.SetupGet(x => x.Cookies).Returns(Array.Empty<IHttpCookie>());
            requestMock.SetupGet(x => x.Url).Returns(new Uri("https://localhost/api/rating"));
            requestMock.SetupGet(x => x.Identities).Returns(Array.Empty<ClaimsIdentity>());
            requestMock.SetupGet(x => x.Method).Returns("GET");
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

        private static MemoryStream CreateBodyStream(object body)
        {
            var json = JsonSerializer.Serialize(body);
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

        private sealed record MongoContext(
            Mock<IMongoClient> Client,
            Mock<IMongoCollection<RatingModel>> CollectionMock);

        private sealed class TestAsyncCursor<T> : IAsyncCursor<T>
        {
            private readonly IReadOnlyList<T> _items;
            private bool _moved;

            public TestAsyncCursor(IEnumerable<T> items)
            {
                _items = new List<T>(items);
            }

            public IEnumerable<T> Current => _moved ? _items : Array.Empty<T>();

            public void Dispose()
            {
            }

            public bool MoveNext(CancellationToken cancellationToken = default)
            {
                if (_moved)
                {
                    return false;
                }

                _moved = true;
                return _items.Count > 0;
            }

            public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(MoveNext(cancellationToken));
            }
        }
    }
}
