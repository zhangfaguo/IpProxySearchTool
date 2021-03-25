using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetCore.CAP;
using DotNetCore.CAP.Diagnostics;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Serialization;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lending.KZKZ.EventBus.Cap
{


	// DotNetCore.CAP.Internal.ConsumerRegister


	internal class CapMyRegistor : IConsumerRegister, IProcessingServer, IDisposable
	{
		private readonly ILogger _logger;

		private readonly IServiceProvider _serviceProvider;

		private readonly IConsumerClientFactory _consumerClientFactory;

		private readonly IDispatcher _dispatcher;

		private readonly ISerializer _serializer;

		private readonly IDataStorage _storage;

		private readonly MethodMatcherCache _selector;

		private readonly TimeSpan _pollingDelay = TimeSpan.FromSeconds(1.0);

		private readonly CapOptions _options;

		private CancellationTokenSource _cts;

		private BrokerAddress _serverAddress;

		private Task _compositeTask;

		private bool _disposed;

		private static bool _isHealthy = true;

		private static readonly DiagnosticListener s_diagnosticListener = new DiagnosticListener("CapDiagnosticListener");

		public CapMyRegistor(ILogger<CapMyRegistor> logger, IServiceProvider serviceProvider)
		{
			_logger = logger;
			_serviceProvider = serviceProvider;
			_options = serviceProvider.GetService<IOptions<CapOptions>>()?.Value;
			_selector = serviceProvider.GetService<MethodMatcherCache>();
			_consumerClientFactory = serviceProvider.GetService<IConsumerClientFactory>();
			_dispatcher = serviceProvider.GetService<IDispatcher>();
			_serializer = serviceProvider.GetService<ISerializer>();
			_storage = serviceProvider.GetService<IDataStorage>();
			_cts = new CancellationTokenSource();
		}

		public bool IsHealthy()
		{
			return _isHealthy;
		}

		public void Start()
		{
			foreach (KeyValuePair<string, IReadOnlyList<ConsumerExecutorDescriptor>> matchGroup in _selector.GetCandidatesMethodsOfGroupNameGrouped())
			{
				for (int i = 0; i < _options.ConsumerThreadCount; i++)
				{
					Task.Factory.StartNew(delegate
					{
						try
						{
							IConsumerClient consumerClient = _consumerClientFactory.Create(matchGroup.Key);
							_serverAddress = consumerClient.BrokerAddress;
							RegisterMessageProcessor(consumerClient);
							consumerClient.Subscribe(Enumerable.Select<ConsumerExecutorDescriptor, string>((IEnumerable<ConsumerExecutorDescriptor>)matchGroup.Value, (Func<ConsumerExecutorDescriptor, string>)((ConsumerExecutorDescriptor x) => x.TopicName)));
							consumerClient.Listening(_pollingDelay, _cts.Token);
						}
						catch (OperationCanceledException)
						{
						}
						catch (BrokerConnectionException ex2)
						{
							_isHealthy = false;
							_logger.LogError(ex2, ex2.Message);
						}
						catch (Exception ex3)
						{
							_logger.LogError(ex3, ex3.Message);
						}
					}, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
				}
			}
			_compositeTask = Task.CompletedTask;
		}

		public void ReStart(bool force = false)
		{
			if (!IsHealthy() || force)
			{
				Pulse();
				_cts = new CancellationTokenSource();
				_isHealthy = true;
				Start();
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			try
			{
				Pulse();
				_compositeTask?.Wait(TimeSpan.FromSeconds(2.0));
			}
			catch (AggregateException ex)
			{
				Exception innerEx = ex.InnerExceptions[0];
				
			}
		}

		public void Pulse()
		{
			_cts?.Cancel();
		}

		private void RegisterMessageProcessor(IConsumerClient client)
		{
			client.OnMessageReceived += async delegate (object sender, TransportMessage transportMessage)
			{
				long? tracingTimestamp = null;
				try
				{
					tracingTimestamp = TracingBefore(transportMessage, _serverAddress);
					string name = transportMessage.GetName();
					string group = transportMessage.GetGroup();
					ConsumerExecutorDescriptor executor;
					bool canFindSubscriber = _selector.TryGetTopicExecutor(name, group, out executor);
					Message message;
					try
					{
						if (!canFindSubscriber)
						{
							SubscriberNotFoundException ex = new SubscriberNotFoundException("Message can not be found subscriber. Name:" + name + ", Group:" + group + ". " +   " see: https://github.com/dotnetcore/CAP/issues/63");
							TracingError(tracingTimestamp, transportMessage, client.BrokerAddress, ex);
							throw ex;
						}
						Type type = Enumerable.FirstOrDefault<ParameterDescriptor>((IEnumerable<ParameterDescriptor>)executor.Parameters, (Func<ParameterDescriptor, bool>)((ParameterDescriptor x) => !x.IsFromCap))?.ParameterType;
						message = await _serializer.DeserializeAsync(transportMessage, type);
						message.RemoveException();
					}
					catch (Exception e3)
					{
						transportMessage.Headers.Add("cap-exception", "SerializationException-->" + e3.Message);
						if (transportMessage.Headers.TryGetValue("cap-msg-type", out var val))
						{
							string dataUri2 = "data:" + val + ";base64," + Convert.ToBase64String(transportMessage.Body);
							message = new Message(transportMessage.Headers, dataUri2);
						}
						else
						{
							string dataUri = "data:UnknownType;base64," + Convert.ToBase64String(transportMessage.Body);
							message = new Message(transportMessage.Headers, dataUri);
						}
					}
					if (message.HasException())
					{
						string content = _serializer.Serialize(message);
						_storage.StoreReceivedExceptionMessage(name, group, content);
						client.Commit(sender);
						try
						{
							_options.FailedThresholdCallback?.Invoke(new FailedInfo
							{
								ServiceProvider = _serviceProvider,
								MessageType = MessageType.Subscribe,
								Message = message
							}); 
						}
						catch (Exception e2)
						{
							 
						}
						TracingAfter(tracingTimestamp, transportMessage, _serverAddress);
					}
					else
					{
						MediumMessage mediumMessage = _storage.StoreReceivedMessage(name, group, message);
						mediumMessage.Origin = message;
						
						TracingAfter(tracingTimestamp, transportMessage, _serverAddress);
						_dispatcher.EnqueueToExecute(mediumMessage, executor);
						client.Commit(sender);

					}
				}
				catch (Exception e)
				{
					_logger.LogError(e, "An exception occurred when process received message. Message:'{0}'.", transportMessage);
					client.Reject(sender);
					TracingError(tracingTimestamp, transportMessage, client.BrokerAddress, e);
				}
			};
			client.OnLog += WriteLog;
		}

		private void WriteLog(object sender, LogMessageEventArgs logmsg)
		{
			switch (logmsg.LogType)
			{
				case MqLogType.ConsumerCancelled:
					_logger.LogWarning("RabbitMQ consumer cancelled. --> " + logmsg.Reason);
					break;
				case MqLogType.ConsumerRegistered:
					_logger.LogInformation("RabbitMQ consumer registered. --> " + logmsg.Reason);
					break;
				case MqLogType.ConsumerUnregistered:
					_logger.LogWarning("RabbitMQ consumer unregistered. --> " + logmsg.Reason);
					break;
				case MqLogType.ConsumerShutdown:
					_isHealthy = false;
					_logger.LogWarning("RabbitMQ consumer shutdown. --> " + logmsg.Reason);
					break;
				case MqLogType.ConsumeError:
					_logger.LogError("Kafka client consume error. --> " + logmsg.Reason);
					break;
				case MqLogType.ServerConnError:
					_isHealthy = false;
					_logger.LogCritical("Kafka server connection error. --> " + logmsg.Reason);
					break;
				case MqLogType.ExceptionReceived:
					_logger.LogError("AzureServiceBus subscriber received an error. --> " + logmsg.Reason);
					break;
				case MqLogType.InvalidIdFormat:
					_logger.LogError("AmazonSQS subscriber delete inflight message failed, invalid id. --> " + logmsg.Reason);
					break;
				case MqLogType.MessageNotInflight:
					_logger.LogError("AmazonSQS subscriber change message's visibility failed, message isn't in flight. --> " + logmsg.Reason);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private long? TracingBefore(TransportMessage message, BrokerAddress broker)
		{
			if (s_diagnosticListener.IsEnabled("DotNetCore.CAP.WriteConsumeBefore"))
			{
				CapEventDataSubStore eventData = new CapEventDataSubStore
				{
					OperationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
					Operation = message.GetName(),
					BrokerAddress = broker,
					TransportMessage = message
				};
				s_diagnosticListener.Write("DotNetCore.CAP.WriteConsumeBefore", eventData);
				return eventData.OperationTimestamp;
			}
			return null;
		}

		private void TracingAfter(long? tracingTimestamp, TransportMessage message, BrokerAddress broker)
		{
			if (tracingTimestamp.HasValue && s_diagnosticListener.IsEnabled("DotNetCore.CAP.WriteConsumeAfter"))
			{
				long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				CapEventDataSubStore eventData = new CapEventDataSubStore
				{
					OperationTimestamp = now,
					Operation = message.GetName(),
					BrokerAddress = broker,
					TransportMessage = message,
					ElapsedTimeMs = now - tracingTimestamp.Value
				};
				s_diagnosticListener.Write("DotNetCore.CAP.WriteConsumeAfter", eventData);
			}
		}

		private void TracingError(long? tracingTimestamp, TransportMessage message, BrokerAddress broker, Exception ex)
		{
			if (tracingTimestamp.HasValue && s_diagnosticListener.IsEnabled("DotNetCore.CAP.WriteConsumeError"))
			{
				long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				CapEventDataSubStore eventData = new CapEventDataSubStore
				{
					OperationTimestamp = now,
					Operation = message.GetName(),
					BrokerAddress = broker,
					TransportMessage = message,
					ElapsedTimeMs = now - tracingTimestamp.Value,
					Exception = ex
				};
				s_diagnosticListener.Write("DotNetCore.CAP.WriteConsumeError", eventData);
			}
		}
	}

}
