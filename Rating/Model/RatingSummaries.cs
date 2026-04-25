using System.Collections.Generic;
using System.Linq;

namespace Rating.Model
{
    public static class RatingSummaries
    {
        public static ViewRating CreateForPerson(int personId, IEnumerable<Rating> ratings)
        {
            var rateValues = ratings.Select(x => x.Rate).ToList();

            return new ViewRating
            {
                PersonId = personId,
                AverageRate = rateValues.Count == 0 ? 0.0 : rateValues.Average()
            };
        }

        public static IReadOnlyList<ViewRating> CreateAll(IEnumerable<Rating> ratings)
        {
            return ratings
                .GroupBy(x => x.PersonId)
                .Select(group => CreateForPerson(group.Key, group))
                .ToList();
        }
    }
}
