using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Azure.Cosmos;
using Rating;
using Rating.Data;

var mongoConnectionString = Environment.GetEnvironmentVariable(Settings.MONGO_CONNECTION_STRING);
var cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString");

// Default to MongoDB, but allow switching via environment variable
var useCosmosDb = !string.IsNullOrWhiteSpace(cosmosConnectionString) && 
                  Environment.GetEnvironmentVariable("DATA_STORE_TYPE")?.ToLower() == "cosmos";

if (!useCosmosDb && string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException($"Missing required environment variable: {Settings.MONGO_CONNECTION_STRING}");
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        if (useCosmosDb)
        {
            services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));
            services.AddSingleton<IDataStore, CosmosDataStore>();
        }
        else
        {
            services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
            services.AddSingleton<IDataStore>(sp => new MongoDataStore(sp.GetRequiredService<IMongoClient>()));
        }
    })
    .Build();

host.Run();
