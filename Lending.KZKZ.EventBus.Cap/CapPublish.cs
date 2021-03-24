using DotNetCore.CAP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lending.KZKZ.EventBus.Cap
{
    internal class CapPublish : IPublish
    {

        ICapPublisher Publisher { get; }
        public CapPublish(ICapPublisher publisher)
        {
            Publisher = publisher;
        }

        public void Publish<T>(string name, T contentObj, string callbackName = null)
        {
            Publisher.Publish(name, contentObj, callbackName);
        }

        public void Publish<T>(string name, T contentObj, IDictionary<string, string> headers)
        {
            Publisher.Publish(name, contentObj, headers);
        }

        public Task PublishAsync<T>(string name, T contentObj, string callbackName = null, CancellationToken cancellationToken = default)
        {
            return Publisher.PublishAsync(name, contentObj, callbackName, cancellationToken);
        }

        public Task PublishAsync<T>(string name, T contentObj, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Publisher.PublishAsync(name, contentObj, headers, cancellationToken);
        }
    }
}
