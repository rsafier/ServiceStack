﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceStack.Caching;
using ServiceStack.Web;

namespace ServiceStack
{
    public static class AutoQueryDataServiceSource
    {
        public static QueryDataSource<T> ServiceSource<T>(this QueryDataContext ctx, object requestDto, ICacheClient cache, TimeSpan? expiresIn=null, string cacheKey=null)
        {
            if (cacheKey == null)
                cacheKey = "aqd:" + requestDto.ToGetUrl();

            var cachedResults = cache.Get<List<T>>(cacheKey);
            if (cachedResults != null)
                return new MemoryDataSource<T>(ctx, cachedResults);

            var response = ServiceSource<T>(ctx, requestDto);
            return response.CacheMemorySource(cache, cacheKey, expiresIn);
        }

        internal static QueryDataSource<T> CacheMemorySource<T>(this MemoryDataSource<T> response, ICacheClient cache, string cacheKey, TimeSpan? expiresIn)
        {
            if (expiresIn != null)
                cache.Set(cacheKey, response.Data, expiresIn.Value);
            else
                cache.Set(cacheKey, response.Data);

            return response;
        }

        public static MemoryDataSource<T> ServiceSource<T>(this QueryDataContext ctx, object requestDto)
        {
            var response = HostContext.ServiceController.Execute(requestDto, ctx.Request);
            var results = GetResults<T>(response);
            if (results == null)
                throw new NotSupportedException("IEnumerable<{0}> could not be derived from Response {1} from Request {2}"
                    .Fmt(typeof(T).Name, response.GetType().Name, requestDto.GetType().Name));

            return new MemoryDataSource<T>(ctx, results);
        }

        public static IEnumerable<T> GetResults<T>(object response)
        {
            var task = response as Task;
            if (task != null)
                response = task.GetResult();

            var httpResult = response as IHttpResult;
            if (httpResult != null)
                response = httpResult.Response;

            var result = response as IEnumerable<T>;
            if (result != null)
                return result;

            foreach (var pi in response.GetType().GetPublicProperties())
            {
                if (typeof(IEnumerable<T>).IsAssignableFrom(pi.PropertyType))
                {
                    return (IEnumerable<T>)pi.GetGetMethod().Invoke(response, new object[0]);
                }
            }

            return null;
        }
    }
}