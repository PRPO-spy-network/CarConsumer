using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using CarConsumer;
using CarConsumer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var config = new ConfigurationBuilder()
	.AddEnvironmentVariables()
	.Build();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging();


#region Timescale
string connectionString = config["TIMESCALE_CONN_STRING"] ?? throw new InvalidDataException("REGION env. var. ne obstaja"); ;
builder.Services.AddDbContextFactory<PostgresContext>(options => options.UseNpgsql(connectionString));
#endregion

#region Event hubs
//TODO region bo kubernetes doloèal -> trenutno EU
string REGION = config["REGION"] ?? throw new InvalidDataException("REGION env. var. ne obstaja");
var storageClient = new BlobContainerClient(
			config["BLOB_CONNECTION_STRING"] ?? throw new InvalidDataException("BLOB_CONNECTION_STRING ne obstaja"),
			config["BLOB_NAME"] ?? throw new InvalidDataException("BLOB_NAME ne obstaja"));
storageClient.CreateIfNotExists();

string EVENT_HUBS_CONNECTION_STRING = config["EVENT_HUBS_CONNECTION_STRING"] ?? throw new InvalidDataException("EVENT_HUBS_CONNECTION_STRING ne obstaja"); ;

var processor = new EventProcessorClient(
	storageClient,
	EventHubConsumerClient.DefaultConsumerGroupName,
	EVENT_HUBS_CONNECTION_STRING,
	REGION);
#endregion

builder.Services.AddSingleton(processor);
builder.Services.AddHostedService<Worker>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();


var logger = host.Services.GetRequiredService<ILogger<Program>>();
if (builder.Environment.IsDevelopment())
{
	logger.LogInformation("Running in dev mode");
}

using (var scope = host.Services.CreateScope())
{
	var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PostgresContext>>();
	using var dbContext = dbContextFactory.CreateDbContext();
	try
	{
		if (await dbContext.Database.CanConnectAsync())
		{
			logger.LogInformation("Connected to timescale.");
		}
		else
		{
			logger.LogWarning("Can't connect to timescale. ");
		}
	}
	catch (Exception ex)
	{
		logger.LogError($"Error connecting to timescale: {ex.Message}");
	}
}


host.Run();
