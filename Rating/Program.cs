using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Rating;

var mongoConnectionString = Environment.GetEnvironmentVariable(Settings.MONGO_CONNECTION_STRING);
if (string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException($"Missing required environment variable: {Settings.MONGO_CONNECTION_STRING}");
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
    })
    .Build();

host.Run();
