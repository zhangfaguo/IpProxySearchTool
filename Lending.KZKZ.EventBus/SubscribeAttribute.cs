using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lending.KZKZ.EventBus
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class SubscribeAttribute : Attribute
    {
        /// <summary>
        /// Topic or exchange route key name.
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// Defines wether this attribute defines a topic subscription partial.
        /// The defined topic will be combined with a topic subscription defined on class level,
        /// which results for example in subscription on "class.method".
        /// </summary>
        public bool IsPartial
        {
            get;
        }

        /// <summary>
        /// Default group name is CapOptions setting.(Assembly name)
        /// kafka --&gt; groups.id
        /// rabbit MQ --&gt; queue.name
        /// </summary>
        public string Group
        {
            get;
            set;
        }

        public SubscribeAttribute(string name, bool isPartial = false)
        {
            Name = name;
            IsPartial = isPartial;
        }
    }
}
