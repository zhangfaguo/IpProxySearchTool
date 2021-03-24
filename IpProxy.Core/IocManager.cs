using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpProxy.Core
{
    public class IocManager
    {
        public static IServiceCollection Services { get; }


        public static IServiceProvider ServiceProvider { get; set; }


        public static CancellationTokenSource  TokenSource { get; set; }
        static IocManager()
        {
            Services = new ServiceCollection();
        }



    }
}
