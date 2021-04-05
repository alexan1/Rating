using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rating
{
    public class Rating
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("PersonID")]
        public int PersonID { get; set; }
        [BsonElement("UserID")]
        public string UserID { get; set; }
        [BsonElement("Rate")]
        public int Rate { get; set; }
    }
}
