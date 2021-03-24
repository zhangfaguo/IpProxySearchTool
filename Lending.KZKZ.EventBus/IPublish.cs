using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lending.KZKZ.EventBus
{
    public interface IPublish
    {

        void Publish<T>(string name, T contentObj, string callbackName = null);


        void Publish<T>(string name, T contentObj, IDictionary<string, string> headers);

        Task PublishAsync<T>(string name, T contentObj, string callbackName = null, CancellationToken cancellationToken = default);

        Task PublishAsync<T>(string name, T contentObj, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
    }
}
