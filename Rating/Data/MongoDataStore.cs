using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Rating.Data
{
    /// <summary>
    /// MongoDB implementation of the data store.
    /// </summary>
    public class MongoDataStore : IDataStore
    {
        private readonly IMongoCollection<Model.Rating> _collection;

        public MongoDataStore(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase(Settings.DATABASE_NAME);
            _collection = database.GetCollection<Model.Rating>(Settings.COLLECTION_NAME);
        }

        public async Task<Model.Rating> FindRatingAsync(int personId, string userId)
        {
            var filter = Builders<Model.Rating>.Filter.Where(r => r.PersonId == personId && r.UserId == userId);
            using var cursor = await _collection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<List<Model.Rating>> FindRatingsByPersonIdAsync(int personId)
        {
            var filter = Builders<Model.Rating>.Filter.Eq(r => r.PersonId, personId);
            using var cursor = await _collection.FindAsync(filter);
            return await cursor.ToListAsync();
        }

        public async Task<List<Model.Rating>> GetAllRatingsAsync()
        {
            using var cursor = await _collection.FindAsync(Builders<Model.Rating>.Filter.Empty);
            return await cursor.ToListAsync();
        }

        public async Task CreateRatingAsync(Model.Rating rating)
        {
            rating.Id = Guid.NewGuid().ToString();
            await _collection.InsertOneAsync(rating);
        }

        public async Task UpdateRatingAsync(Model.Rating rating)
        {
            await _collection.ReplaceOneAsync(r => r.Id == rating.Id, rating);
        }

        public async Task DeleteRatingAsync(string id, int personId)
        {
            await _collection.DeleteOneAsync(r => r.Id == id);
        }
    }
}
