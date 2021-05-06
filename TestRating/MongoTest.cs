using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;
using System;

namespace TestRating
{
    [TestClass]
    public class MongoTest
    {
        //private readonly MongoClient _mongoClient;

        //public MongoTest(MongoClient mongoClient)
        //{
        //    _mongoClient = mongoClient;
        //}

        [TestMethod]
        public void TestMethod1()
        {

            const bool result = true;
            var settings = Environment.GetEnvironmentVariable("MongoDBAtlasConnectionString");
            var settings1 = "mongodb+srv://alexan1:<passwod>.c0dsb.azure.mongodb.net?retryWrites=true&w=majority";
            var client = new MongoClient(settings1);
            var database = client.GetDatabase("People");
            database.RunCommand((Command<BsonDocument>)"{ping:1}");

            Assert.IsTrue(result);
            //Assert.AreEqual(settings, settings1);

        }
    }
}
