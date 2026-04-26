using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rating.Model;
using RatingModel = Rating.Model.Rating;

namespace TestRating
{
    [TestClass]
    public class RatingSummariesTests
    {
        [TestMethod]
        public void CreateForPersonReturnsZeroWhenThereAreNoRatings()
        {
            var result = RatingSummaries.CreateForPerson(7, new List<RatingModel>());

            Assert.AreEqual(7, result.PersonId);
            Assert.AreEqual(0.0, result.AverageRate);
        }

        [TestMethod]
        public void CreateForPersonReturnsAverageForMatchingRatings()
        {
            var ratings = new[]
            {
                new RatingModel { PersonId = 7, Rate = 8 },
                new RatingModel { PersonId = 7, Rate = 4 }
            };

            var result = RatingSummaries.CreateForPerson(7, ratings);

            Assert.AreEqual(6.0, result.AverageRate);
        }

        [TestMethod]
        public void CreateAllGroupsRatingsByPerson()
        {
            var ratings = new[]
            {
                new RatingModel { PersonId = 1, Rate = 10 },
                new RatingModel { PersonId = 1, Rate = 6 },
                new RatingModel { PersonId = 2, Rate = 5 }
            };

            var result = RatingSummaries.CreateAll(ratings).OrderBy(x => x.PersonId).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].PersonId);
            Assert.AreEqual(8.0, result[0].AverageRate);
            Assert.AreEqual(2, result[1].PersonId);
            Assert.AreEqual(5.0, result[1].AverageRate);
        }
    }
}
