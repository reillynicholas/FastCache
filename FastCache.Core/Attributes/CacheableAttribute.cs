using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.Core.Attributes
{
    public class CacheableAttribute : AbstractInterceptorAttribute
    {
        private readonly string _key;
        private readonly string _expression;
        private readonly long _expire;
        public sealed override int Order { get; set; }

        public CacheableAttribute(string key, string expression, long expire = 0)
        {
            _key = key;
            _expression = expression;
            _expire = expire;
            Order = 2;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var cacheClient = context.ServiceProvider.GetService<ICacheClient>();

            var dictionary = new Dictionary<string, object>();
            var parameterInfos = context.ImplementationMethod.GetParameters();
            for (var i = 0; i < context.Parameters.Length; i++)
            {
                dictionary.Add(parameterInfos[i].Name, context.Parameters[i]);
            }

            var key = KeyGenerateHelper.GetKey(_key, _expression, dictionary);

            var canGetCache = true;
            foreach (var customAttribute in context.ProxyMethod.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName == typeof(EvictableAttribute).FullName)
                {
                    canGetCache = false;
                }
            }

            if (canGetCache)
            {
                var cacheValue = await cacheClient.Get(key);

                if (null != cacheValue.Value)
                {
                    context.ReturnValue = cacheValue.Value;
                    return;
                }
            }

            await next(context);

            var returnType = context.ReturnValue.GetType();

            await cacheClient.Set(key, new CacheItem
            {
                Value = context.ReturnValue,
                CreatedAt = DateTime.Now.Ticks,
                Expire = _expire > 0 ? DateTime.Now.AddSeconds(_expire).Ticks : DateTime.Now.AddYears(1).Ticks,
                AssemblyName = returnType.Assembly.GetName().Name,
                Type = returnType.FullName ?? string.Empty,
            }, _expire);
        }
    }
}