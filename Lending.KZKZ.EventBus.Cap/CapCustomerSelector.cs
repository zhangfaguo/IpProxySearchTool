using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lending.KZKZ.EventBus.Cap
{
    internal class CapCustomerSelector : ConsumerServiceSelector
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly CapOptions _capOptions;
        public CapCustomerSelector(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _capOptions = serviceProvider.GetService<IOptions<CapOptions>>().Value;
        }

        protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(IServiceProvider provider)
        {
            var list = base.FindConsumersFromInterfaceTypes(provider);
            var myList = FindConsumersFromIocTypes(provider);
            if (myList != null)
            {
                list = list.Concat(myList);
            }
            return list;
        }
        private IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromIocTypes(IServiceProvider provider)
        {
            List<ConsumerExecutorDescriptor> executorDescriptorList = new List<ConsumerExecutorDescriptor>();
            TypeInfo capSubscribeTypeInfo = typeof(ISubscribe).GetTypeInfo();
            foreach (ServiceDescriptor service in CapBusExtentions.Service.Where((ServiceDescriptor o) => o.ImplementationType != null || o.ImplementationFactory != null))
            {
                Type detectType = service.ImplementationType ?? service.ServiceType;
                if (capSubscribeTypeInfo.IsAssignableFrom(detectType))
                {
                    Type actualType = service.ImplementationType;
                    if (actualType == null && service.ImplementationFactory != null)
                    {
                        actualType = provider.GetRequiredService(service.ServiceType).GetType();
                    }
                    if (actualType == null)
                    {
                        throw new NullReferenceException("ServiceType");
                    }
                    executorDescriptorList.AddRange(GetSubscribeAttributesDescription(actualType.GetTypeInfo(), service.ServiceType.GetTypeInfo()));
                }
            }
            return executorDescriptorList;
        }
        protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypes()
        {
            var list = new List<ConsumerExecutorDescriptor>();
            try
            {
                var lists = base.FindConsumersFromControllerTypes();
                list.AddRange(lists);
            }
            catch (Exception)
            {
              
            }
            var myList = FindConsumersFromControllerTypesCustom();
            if (myList != null)
            {
                list.AddRange(myList);
            }
            return list;
        }

        private IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypesCustom()
        {
            List<ConsumerExecutorDescriptor> executorDescriptorList = new List<ConsumerExecutorDescriptor>();
            var assems = AppDomain.CurrentDomain.GetAssemblies();
            foreach(var assem in assems)
            {
                try
                {
                    var types = assem.ExportedTypes;
                    if (types != null)
                    {
                        foreach (Type exportedType in types)
                        {
                            TypeInfo typeInfo = exportedType.GetTypeInfo();
                            if (Helper.IsController(typeInfo))
                            {
                                executorDescriptorList.AddRange(GetSubscribeAttributesDescription(typeInfo));
                            }
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
           
            return executorDescriptorList;
        }



        private IEnumerable<ConsumerExecutorDescriptor> GetSubscribeAttributesDescription(TypeInfo typeInfo, TypeInfo serviceTypeInfo = null)
        {
            SubscribeAttribute topicClassAttribute = typeInfo.GetCustomAttribute<SubscribeAttribute>(inherit: true);
            foreach (MethodInfo method in typeInfo.GetRuntimeMethods())
            {
                IEnumerable<SubscribeAttribute> topicMethodAttributes = method.GetCustomAttributes<SubscribeAttribute>(inherit: true);
                if (topicClassAttribute == null)
                {
                    topicMethodAttributes = topicMethodAttributes.Where((SubscribeAttribute x) => !x.IsPartial);
                }
                if (!topicMethodAttributes.Any())
                {
                    continue;
                }
                foreach (SubscribeAttribute attr in topicMethodAttributes)
                {
                    SetSubscribeAttribute(attr);
                    List<ParameterDescriptor> parameters = (from parameter in method.GetParameters()
                                                            select new ParameterDescriptor
                                                            {
                                                                Name = parameter.Name,
                                                                ParameterType = parameter.ParameterType,
                                                                IsFromCap = parameter.GetCustomAttributes(typeof(FromCapAttribute)).Any()
                                                            }).ToList();
                    var subAttr = new CapSubscribeAttribute(attr.Name)
                    {
                        Group = attr.Group
                    };
                    yield return InitDescriptor(subAttr, method, typeInfo, serviceTypeInfo, parameters, subAttr);
                }
            }
        }


        private void SetSubscribeAttribute(SubscribeAttribute attribute)
        {
            attribute.Group = (attribute.Group ?? _capOptions.DefaultGroup) + "." + _capOptions.Version;
        }


        private static ConsumerExecutorDescriptor InitDescriptor(TopicAttribute attr, MethodInfo methodInfo, TypeInfo implType, TypeInfo serviceTypeInfo, IList<ParameterDescriptor> parameters, TopicAttribute classAttr = null)
        {
            return new ConsumerExecutorDescriptor
            {
                Attribute = attr,
                ClassAttribute = classAttr,
                MethodInfo = methodInfo,
                ImplTypeInfo = implType,
                ServiceTypeInfo = serviceTypeInfo,
                Parameters = parameters
            };
        }

    }
}
