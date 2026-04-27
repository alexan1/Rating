using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Rating.Data
{
    /// <summary>
    /// Azure Cosmos DB implementation of the data store.
    /// </summary>
    public class CosmosDataStore : IDataStore
    {
        private readonly Container _container;

        public CosmosDataStore(CosmosClient cosmosClient)
        {
            var database = cosmosClient.GetDatabase(Settings.DATABASE_NAME);
            _container = database.GetContainer(Settings.COLLECTION_NAME);
        }

        public async Task<Model.Rating> FindRatingAsync(int personId, string userId)
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.PersonId = @personId AND c.UserId = @userId")
                .WithParameter("@personId", personId)
                .WithParameter("@userId", userId);

            using FeedIterator<Model.Rating> iterator = _container.GetItemQueryIterator<Model.Rating>(
                queryDefinition,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(personId),
                    MaxItemCount = 1
                });

            while (iterator.HasMoreResults)
            {
                FeedResponse<Model.Rating> response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    return item;
                }
            }

            return null;
        }

        public async Task<List<Model.Rating>> FindRatingsByPersonIdAsync(int personId)
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.PersonId = @personId")
                .WithParameter("@personId", personId);

            using FeedIterator<Model.Rating> iterator = _container.GetItemQueryIterator<Model.Rating>(
                queryDefinition,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(personId)
                });
            var results = new List<Model.Rating>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<Model.Rating> response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task<List<Model.Rating>> GetAllRatingsAsync()
        {
            var query = "SELECT * FROM c";
            using FeedIterator<Model.Rating> iterator = _container.GetItemQueryIterator<Model.Rating>(query);
            var results = new List<Model.Rating>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<Model.Rating> response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task CreateRatingAsync(Model.Rating rating)
        {
            rating.Id = Guid.NewGuid().ToString();
            await _container.CreateItemAsync(rating, new PartitionKey(rating.PersonId));
        }

        public async Task UpdateRatingAsync(Model.Rating rating)
        {
            await _container.ReplaceItemAsync(rating, rating.Id, new PartitionKey(rating.PersonId));
        }

        public async Task DeleteRatingAsync(string id, int personId)
        {
            await _container.DeleteItemAsync<Model.Rating>(id, new PartitionKey(personId));
        }
    }
}
