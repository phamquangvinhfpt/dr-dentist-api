﻿namespace FSH.WebApi.Application.Common.Caching;

public interface ICacheKeyService : IScopedService
{
    public string GetCacheKey(string name, object id, bool includeTenantId = true);
    public string GetCacheKey(string name, bool includeTenantId = true);
}