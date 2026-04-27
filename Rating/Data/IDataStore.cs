using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rating.Data
{
    /// <summary>
    /// Abstraction for data persistence layer.
    /// Allows swapping between MongoDB, Cosmos DB, or other backends.
    /// </summary>
    public interface IDataStore
    {
        /// <summary>
        /// Find a single rating by PersonId and UserId.
        /// </summary>
        Task<Model.Rating> FindRatingAsync(int personId, string userId);

        /// <summary>
        /// Find all ratings for a given PersonId.
        /// </summary>
        Task<List<Model.Rating>> FindRatingsByPersonIdAsync(int personId);

        /// <summary>
        /// Get all ratings.
        /// </summary>
        Task<List<Model.Rating>> GetAllRatingsAsync();

        /// <summary>
        /// Create a new rating.
        /// </summary>
        Task CreateRatingAsync(Model.Rating rating);

        /// <summary>
        /// Update an existing rating.
        /// </summary>
        Task UpdateRatingAsync(Model.Rating rating);

        /// <summary>
        /// Delete a rating by ID.
        /// </summary>
        Task DeleteRatingAsync(string id, int personId);
    }
}
