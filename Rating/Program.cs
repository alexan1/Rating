using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Rating.Auth;
using Rating;
using Rating.Data;

var mongoConnectionString = Environment.GetEnvironmentVariable(Settings.MONGO_CONNECTION_STRING);
var cosmosConnectionString = Environment.GetEnvironmentVariable(Settings.COSMOS_CONNECTION_STRING);
var dataStoreType = Environment.GetEnvironmentVariable(Settings.DATA_STORE_TYPE);
var b2cAuthority = Environment.GetEnvironmentVariable(Settings.B2C_AUTHORITY);
var b2cAudience = Environment.GetEnvironmentVariable(Settings.B2C_AUDIENCE);

var useCosmosDb = string.Equals(dataStoreType, Settings.COSMOS_DATA_STORE, StringComparison.OrdinalIgnoreCase);

if (!string.IsNullOrWhiteSpace(dataStoreType) &&
    !useCosmosDb &&
    !string.Equals(dataStoreType, Settings.MONGO_DATA_STORE, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Unsupported {Settings.DATA_STORE_TYPE} value '{dataStoreType}'. Expected '{Settings.MONGO_DATA_STORE}' or '{Settings.COSMOS_DATA_STORE}'.");
}

if (useCosmosDb)
{
    if (string.IsNullOrWhiteSpace(cosmosConnectionString))
    {
        throw new InvalidOperationException($"Missing required environment variable: {Settings.COSMOS_CONNECTION_STRING}");
    }
}
else if (string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException($"Missing required environment variable: {Settings.MONGO_CONNECTION_STRING}");
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IAccessTokenValidator>(_ => new AccessTokenValidator(
            b2cAuthority,
            b2cAudience));

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
