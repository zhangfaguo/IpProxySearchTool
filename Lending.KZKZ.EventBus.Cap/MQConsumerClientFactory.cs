

namespace Lending.KZKZ.EventBus.Cap
{
	using System;
	using DotNetCore.CAP;
	using DotNetCore.CAP.RabbitMQ;
	using DotNetCore.CAP.Transport;
	using Microsoft.Extensions.Options;

	internal sealed class MQConsumerClientFactory : IConsumerClientFactory
	{
		private readonly IConnectionChannelPool _connectionChannelPool;

		private readonly IOptions<RabbitMQOptions> _rabbitMQOptions;

		public MQConsumerClientFactory(IOptions<RabbitMQOptions> rabbitMQOptions, IConnectionChannelPool channelPool)
		{
			_rabbitMQOptions = rabbitMQOptions;
			_connectionChannelPool = channelPool;
		}

		public IConsumerClient Create(string groupId)
		{
			try
			{
				MQConsumerClient rabbitMQConsumerClient = new MQConsumerClient(groupId, _connectionChannelPool, _rabbitMQOptions);
				rabbitMQConsumerClient.Connect();
				return rabbitMQConsumerClient;
			}
			catch (Exception innerException)
			{
				throw new BrokerConnectionException(innerException);
			}
		}
	}
}
