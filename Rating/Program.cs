using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Rating;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(_ => new MongoClient(Environment.GetEnvironmentVariable(Settings.MONGO_CONNECTION_STRING)));
    })
    .Build();

host.Run();
