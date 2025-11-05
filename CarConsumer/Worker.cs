using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using CarConsumer.Models;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CarConsumer
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly EventProcessorClient _processorClient;
		private readonly IDbContextFactory<PostgresContext> _dbContextFactory;

		public Worker(ILogger<Worker> logger, IDbContextFactory<PostgresContext> dbContextFactory, EventProcessorClient processorClient)
		{
			_logger = logger;
			_dbContextFactory = dbContextFactory;
			_processorClient = processorClient;
			_processorClient.ProcessEventAsync += ProcessEventHandler;
			_processorClient.ProcessErrorAsync += ProcessErrorHandler;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				await _processorClient.StartProcessingAsync(stoppingToken);
				await Task.Delay(Timeout.Infinite, stoppingToken);
			}catch(Exception ex)
			{
				_logger.LogError(ex, "Problem");
			}
			finally
			{
				try
				{
					await _processorClient.StopProcessingAsync();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error stopping EventProcessorClient");
				}
			}
		}

	async Task ProcessEventHandler(ProcessEventArgs eventArgs)
		{
			CarGpsData? data = null;
			try
			{
				data = JsonSerializer.Deserialize<CarGpsData>(Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));
				if (data == null)
				{
					throw new Exception("No data?");
				}

				data.Time = DateTime.SpecifyKind(data.Time, DateTimeKind.Utc);
				//_logger.LogInformation("\tReceived event: {0}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));

				using (var dbContext = _dbContextFactory.CreateDbContext())
				{
					dbContext.CarGpsData.Add(data);
					dbContext.SaveChanges();
				}

				await eventArgs.UpdateCheckpointAsync(); // Overkill, ampak dobro za testiranje
			}
			catch
			{
				_logger.LogInformation("\tFailed to save event in partition ({0}): {1}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()), eventArgs.Partition);
			}
		}

	Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
		{
			// Write details about the error to the console window
			_logger.LogError($"\tPartition '{eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
			_logger.LogError(eventArgs.Exception.Message);
			return Task.CompletedTask;
		}
	}
}
