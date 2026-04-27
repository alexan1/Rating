using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Cosmos;

namespace Rating.Data
{
    /// <summary>
    /// Azure Cosmos DB implementation of the data store.
    /// </summary>
    public class CosmosDataStore : IDataStore
    {
        private readonly CosmosContainer _container;

        public CosmosDataStore(CosmosClient cosmosClient)
        {
            var database = cosmosClient.GetDatabase(Settings.DATABASE_NAME);
            _container = database.GetContainer(Settings.COLLECTION_NAME);
        }

        public async Task<Model.Rating> FindRatingAsync(int personId, string userId)
        {
            var query = $"SELECT * FROM c WHERE c.PersonId = {personId} AND c.UserId = '{EscapeString(userId)}'";
            var iterator = _container.GetItemQueryIterator<Model.Rating>(query);
            
            await foreach (var item in iterator)
            {
                return item;
            }
            
            return null;
        }

        public async Task<List<Model.Rating>> FindRatingsByPersonIdAsync(int personId)
        {
            var query = $"SELECT * FROM c WHERE c.PersonId = {personId}";
            var iterator = _container.GetItemQueryIterator<Model.Rating>(query);
            var results = new List<Model.Rating>();
            
            await foreach (var item in iterator)
            {
                results.Add(item);
            }
            
            return results;
        }

        public async Task<List<Model.Rating>> GetAllRatingsAsync()
        {
            var query = "SELECT * FROM c";
            var iterator = _container.GetItemQueryIterator<Model.Rating>(query);
            var results = new List<Model.Rating>();
            
            await foreach (var item in iterator)
            {
                results.Add(item);
            }
            
            return results;
        }

        public async Task CreateRatingAsync(Model.Rating rating)
        {
            rating.Id = Guid.NewGuid().ToString();
            await _container.CreateItemAsync(rating, new PartitionKey(rating.PersonId.ToString()));
        }

        public async Task UpdateRatingAsync(Model.Rating rating)
        {
            await _container.ReplaceItemAsync(rating, rating.Id, new PartitionKey(rating.PersonId.ToString()));
        }

        public async Task DeleteRatingAsync(string id, int personId)
        {
            await _container.DeleteItemAsync<Model.Rating>(id, new PartitionKey(personId.ToString()));
        }

        private static string EscapeString(string value)
        {
            return value?.Replace("'", "''") ?? "";
        }
    }
}
