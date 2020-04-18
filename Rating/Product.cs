using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Rating
{
    class Products
    {
        public class ProductRating
        {
            [BsonElement("Rating")]
            public int Rating { get; set; }
        }

        public class Product
        {
            [BsonId]
            public ObjectId Id { get; set; }


            [BsonElement("PLU")]
            public int PLU { get; set; }

            [BsonElement("Description")]
            public string Description { get; set; }

            [BsonElement("AverageRating")]
            public Double AvgRating { get; set; }


            [BsonElement("Ratings")]
            public List<ProductRating> Ratings { get; set; }
        }
    }
}
