using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Lending.KZKZ.EventBus.Cap
{
    public class CapDispatcher : IDispatcher, IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IMessageSender _sender;
        private readonly ISubscribeDispatcher _executor;
        private readonly ILogger<CapDispatcher> _logger;

        private readonly Channel<MediumMessage> _publishedChannel;
        private readonly Channel<(MediumMessage, ConsumerExecutorDescriptor)> _receivedChannel;

        public CapDispatcher(ILogger<CapDispatcher> logger,
            IMessageSender sender,
            IOptions<CapOptions> options,
            ISubscribeDispatcher executor)
        {
            _logger = logger;
            _sender = sender;
            _executor = executor;

            _publishedChannel = Channel.CreateUnbounded<MediumMessage>(new UnboundedChannelOptions() { SingleReader = false, SingleWriter = true });
            _receivedChannel = Channel.CreateUnbounded<(MediumMessage, ConsumerExecutorDescriptor)>();

            Task.WhenAll(Enumerable.Range(0,1)
                .Select(_ => Task.Factory.StartNew(Sending, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray());

            Task.WhenAll(Enumerable.Range(0, options.Value.ConsumerThreadCount)
                .Select(_ => Task.Factory.StartNew(Processing, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray());
        }

        public void EnqueueToPublish(MediumMessage message)
        {
            _publishedChannel.Writer.TryWrite(message);
        }

        public void EnqueueToExecute(MediumMessage message, ConsumerExecutorDescriptor descriptor)
        {
            // _receivedChannel.Writer.TryWrite((message, descriptor));
            var task = _executor.DispatchAsync(message, descriptor, _cts.Token).Result;
        }

        public void Dispose()
        {
            _cts.Cancel();
        }

        private async Task Sending()
        {
            try
            {
                while (await _publishedChannel.Reader.WaitToReadAsync(_cts.Token))
                {
                    while (_publishedChannel.Reader.TryRead(out var message))
                    {
                        try
                        {
                            var result = await _sender.SendAsync(message);
                           
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                $"An exception occurred when sending a message to the MQ. Id:{message.DbId}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        private async Task Processing()
        {
            try
            {
                while (await _receivedChannel.Reader.WaitToReadAsync(_cts.Token))
                {
                    while (_receivedChannel.Reader.TryRead(out var message))
                    {
                        await _executor.DispatchAsync(message.Item1, message.Item2, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }
    }
}
