using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.RabbitMQ;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Lending.KZKZ.EventBus.Cap
{
	public class MQConsumerClient : IConsumerClient, IDisposable
	{
		private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

		private readonly IConnectionChannelPool _connectionChannelPool;

		private readonly string _exchangeName;

		private readonly string _queueName;

		private readonly RabbitMQOptions _rabbitMQOptions;

		private IModel _channel;

		private IConnection _connection;

		public BrokerAddress BrokerAddress => new BrokerAddress("RabbitMQ", _rabbitMQOptions.HostName);

		public event EventHandler<TransportMessage> OnMessageReceived;

		public event EventHandler<LogMessageEventArgs> OnLog;

		public MQConsumerClient(string queueName, IConnectionChannelPool connectionChannelPool, IOptions<RabbitMQOptions> options)
		{
			_queueName = queueName;
			_connectionChannelPool = connectionChannelPool;
			_rabbitMQOptions = options.Value;
			_exchangeName = connectionChannelPool.Exchange;
		}

		public void Subscribe(IEnumerable<string> topics)
		{
			if (topics == null)
			{
				throw new ArgumentNullException("topics");
			}
			Connect();
			foreach (string topic in topics)
			{
				_channel.QueueBind(_queueName, _exchangeName, topic);
			}
		}

		public void Listening(TimeSpan timeout, CancellationToken cancellationToken)
		{
			Connect();
			EventingBasicConsumer consumer = new EventingBasicConsumer(_channel);
			consumer.Received += OnConsumerReceived;
			consumer.Shutdown += OnConsumerShutdown;
			consumer.Registered += OnConsumerRegistered;
			consumer.Unregistered += OnConsumerUnregistered;
			consumer.ConsumerCancelled += OnConsumerConsumerCancelled;
			_channel.BasicConsume(_queueName, autoAck: false, consumer);
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				cancellationToken.WaitHandle.WaitOne(timeout);
			}
		}

		public void Commit(object sender)
		{
			_channel.BasicAck((ulong)sender, multiple: false);
		}

		public void Reject(object sender)
		{
			_channel.BasicReject((ulong)sender, requeue: true);
		}

		public void Dispose()
		{
			_channel?.Dispose();
			_connection?.Dispose();
		}

		public void Connect()
		{
			if (_connection != null)
			{
				return;
			}
			_connectionLock.Wait();
			try
			{
				if (_connection == null)
				{
					_connection = _connectionChannelPool.GetConnection();
					_channel = _connection.CreateModel();
					_channel.ExchangeDeclare(_exchangeName, "topic", durable: true);
					Dictionary<string, object> arguments = new Dictionary<string, object>
				{
					{
						"x-message-ttl",
						_rabbitMQOptions.QueueMessageExpires
					}
				};
					_channel.BasicQos(0, 1, false);
					_channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false, arguments);
				}
			}
			finally
			{
				_connectionLock.Release();
			}
		}

		private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e)
		{
			LogMessageEventArgs args = new LogMessageEventArgs
			{
				LogType = MqLogType.ConsumerCancelled,
				Reason = string.Join(",", e.ConsumerTags)
			};
			this.OnLog?.Invoke(sender, args);
		}

		private void OnConsumerUnregistered(object sender, ConsumerEventArgs e)
		{
			LogMessageEventArgs args = new LogMessageEventArgs
			{
				LogType = MqLogType.ConsumerUnregistered,
				Reason = string.Join(",", e.ConsumerTags)
			};
			this.OnLog?.Invoke(sender, args);
		}

		private void OnConsumerRegistered(object sender, ConsumerEventArgs e)
		{
			LogMessageEventArgs args = new LogMessageEventArgs
			{
				LogType = MqLogType.ConsumerRegistered,
				Reason = string.Join(",", e.ConsumerTags)
			};
			this.OnLog?.Invoke(sender, args);
		}

		private void OnConsumerReceived(object sender, BasicDeliverEventArgs e)
		{
			Dictionary<string, string> headers = new Dictionary<string, string>();
			foreach (KeyValuePair<string, object> header in e.BasicProperties.Headers)
			{
				headers.Add(header.Key, (header.Value == null) ? null : Encoding.UTF8.GetString((byte[])header.Value));
			}
			headers.Add("cap-msg-group", _queueName);
			TransportMessage message = new TransportMessage(headers, e.Body.ToArray());
			this.OnMessageReceived?.Invoke(e.DeliveryTag, message);
		}

		private void OnConsumerShutdown(object sender, ShutdownEventArgs e)
		{
			LogMessageEventArgs args = new LogMessageEventArgs
			{
				LogType = MqLogType.ConsumerShutdown,
				Reason = e.ReplyText
			};
			this.OnLog?.Invoke(sender, args);
		}
	}
}