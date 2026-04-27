using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Rating.Model
{
    public class Rating
    {
        [BsonId]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [BsonElement("PersonID")]
        [JsonPropertyName("PersonId")]
        public int PersonId { get; set; }
        
        [BsonElement("UserID")]
        [JsonPropertyName("UserId")]
        public string UserId { get; set; }
        
        [BsonElement("Rate")]
        [JsonPropertyName("Rate")]
        public int Rate { get; set; }
    }
}
