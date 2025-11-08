using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using CarConsumer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using CarConsumer;

namespace CarConsumer.tests
{
	public class UnitTest1
	{
		
		[Fact]
		public async Task Test1()
		{
			PartitionContext partitionContext = EventHubsModelFactory.PartitionContext(
			fullyQualifiedNamespace: "sample-hub.servicebus.windows.net",
			eventHubName: "sample-hub",
			consumerGroup: "$Default",
			partitionId: "0");

			EventData eventData = EventHubsModelFactory.EventData(
				eventBody: new BinaryData("{\"Time\":\"2025-11-08T10:57:55\\u002B00:00\",\"CarId\":\"ABCD\",\"Longitude\":1,\"Latitude\":2}"),
				systemProperties: new Dictionary<string, object>(), //arbitrary value
				partitionKey: "sample-key",
				sequenceNumber: 1000,
				offsetString: "1500:1:3344.1",
				enqueuedTime: DateTimeOffset.Parse("11:36 PM"));

			// This creates a new instance of ProcessEventArgs to pass into the handler directly.

			ProcessEventArgs processEventArgs = new(
				partition: partitionContext,
				data: eventData,
				updateCheckpointImplementation: _ => Task.CompletedTask); // arbitrary value

			var loggerMock = new Mock<ILogger<Worker>>();
			var evProcClient = new Mock<EventProcessorClient>();

			var options = new DbContextOptionsBuilder<PostgresContext>()
				.UseInMemoryDatabase(databaseName: $"TestDatabase_{Guid.NewGuid()}").Options;
			var dbContextFactoryMock = new Mock<IDbContextFactory<PostgresContext>>();
			dbContextFactoryMock
				.Setup(f => f.CreateDbContext())
				.Returns(() => new PostgresContext(options));

			Worker worker = new Worker(loggerMock.Object, dbContextFactoryMock.Object, evProcClient.Object);
			await worker.ProcessEventHandler(processEventArgs);

			using var dbContext = new PostgresContext(options);
			var savedData = await dbContext.CarGpsData.ToListAsync();
			Assert.Single(savedData);
			Assert.Equal("ABCD", savedData[0].CarId);
			Assert.Equal(2, savedData[0].Latitude);
			Assert.Equal(1, savedData[0].Longitude);
		}
	}
}