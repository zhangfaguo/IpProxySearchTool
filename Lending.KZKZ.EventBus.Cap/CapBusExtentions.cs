using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Threading;

namespace Lending.KZKZ.EventBus.Cap
{
    public static class CapBusExtentions
    {

        internal static IServiceCollection Service;

        public static IServiceCollection ConfigCap(this IServiceCollection ServiceCollections, string host, string virtualHost, int port, string userName, string password, string capSchema, string connectString,string qouteName, Action<IServiceCollection> config = null)
        {
            Service = ServiceCollections;
            config?.Invoke(ServiceCollections);
            ServiceCollections.AddCap(options =>
            {
                options.UseRabbitMQ(opt =>
                {
                    opt.HostName = host;
                    opt.UserName = userName;
                    opt.Password = password;
                    opt.Port = port;
                    opt.VirtualHost = virtualHost ?? "/";
                });
                options.UseSqlServer(b =>
                {
                    b.ConnectionString = connectString;
                    b.Schema = capSchema ?? "cap";
                });
                options.DefaultGroup = qouteName;
                options.RegisterExtension(new CapOptionsExtension());
            });
            ServiceCollections.AddLogging(cfg =>
            {
                cfg.AddConsole();

                cfg.AddNLog();
            });

            ServiceCollections.AddScoped<IPublish, CapPublish>();
            return ServiceCollections;
        }

    
        public static IServiceProvider UseCap(this IServiceProvider engine, CancellationToken token)
        {
            engine.GetRequiredService<IBootstrapper>().BootstrapAsync(token);
            return engine;
        }
    }

    internal class CapOptionsExtension : ICapOptionsExtension
    {
        public void AddServices(IServiceCollection services)
        {
            services.Replace(new ServiceDescriptor(typeof(IConsumerServiceSelector), typeof(CapCustomerSelector), ServiceLifetime.Singleton));
        }
    }
}
