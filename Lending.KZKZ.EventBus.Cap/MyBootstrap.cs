using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default implement of <see cref="T:DotNetCore.CAP.Internal.IBootstrapper" />.
/// </summary>
internal class MyBootstrap : BackgroundService, IBootstrapper
{
	private readonly ILogger<MyBootstrap> _logger;

	private IStorageInitializer Storage
	{
		get;
	}

	private IEnumerable<IProcessingServer> Processors
	{
		get;
	}

	public MyBootstrap(ILogger<MyBootstrap> logger, IStorageInitializer storage, IEnumerable<IProcessingServer> processors)
	{
		_logger = logger;
		Storage = storage;
		Processors = processors;
	}

	public async Task BootstrapAsync(CancellationToken stoppingToken)
	{
		_logger.LogDebug("### CAP background task is starting.");
		try
		{
			await Storage.InitializeAsync(stoppingToken);
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Initializing the storage structure failed!");
		}
		stoppingToken.Register(delegate
		{
			_logger.LogDebug("### CAP background task is stopping.");
			foreach (IProcessingServer current in Processors)
			{
				try
				{
					current.Dispose();
				}
				catch (OperationCanceledException ex)
				{
				}
			}
		});
		await BootstrapCoreAsync();
		_logger.LogInformation("### CAP started!");
	}

	protected virtual Task BootstrapCoreAsync()
	{
		foreach (IProcessingServer item in Processors)
		{
			try
			{
				item.Start();
			}
			catch (Exception ex)
			{
			}
		}
		return Task.CompletedTask;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await BootstrapAsync(stoppingToken);
	}
}
