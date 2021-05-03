using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rating.Model
{
    public class ViewRating
    {
        public int PersonId { get; set; }
        public double AverageRate { get; set; }
    }
}
