using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rating.Model
{
    public class Rating
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("PersonID")]
        public int PersonId { get; set; }
        [BsonElement("UserID")]
        public string UserId { get; set; }
        [BsonElement("Rate")]
        public int Rate { get; set; }
    }
}
