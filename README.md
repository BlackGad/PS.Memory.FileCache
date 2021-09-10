# Status

## Master

[![Build status](https://ci.appveyor.com/api/projects/status/3n68og1q7gcebk8x?svg=true)](https://ci.appveyor.com/project/BlackGad/ps-memory-filecache)
[![NuGet version (PS.Memory.Cache)](https://img.shields.io/nuget/v/PS.Memory.FileCache.svg?style=flat-square)](https://www.nuget.org/packages/PS.Memory.FileCache/)

## CI

[![Build status](https://ci.appveyor.com/api/projects/status/5teu4584irkgnysp?svg=true)](https://ci.appveyor.com/project/BlackGad/ps-memory-filecache-s9805)
[![MyGet version (PS.Memory.Cache)](https://img.shields.io/myget/ps-projects/v/PS.Memory.FileCache.svg?style=flat-square)](https://www.myget.org/feed/ps-projects/package/nuget/PS.Memory.FileCache)

# Description

It is simple in-process file cache implementation inherited from standard [ObjectCache](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.caching.objectcache?view=dotnet-plat-ext-5.0) class. FileCache is thread safe (supports multithread/multiprocessing parallel cache processing).

# Capabilities

## Support

- **AbsoluteExpirations**: The ability to automatically remove cache entries at a specific date and time.
- **SlidingExpirations**: The ability to automatically remove cache entries that have not been accessed in a specified time span.
- **CacheRegions**: The ability to partition its storage into cache regions, and supports the ability to insert cache entries into those regions and to retrieve cache entries from those regions.
- **InMemoryProvider**: Current implementation uses [MemoryCache] internally as fast proxy.

## Not support

- **CacheEntryChangeMonitors**: The ability to create change monitors that monitor entries.
- **CacheEntryRemovedCallback**: Can raise a notification that an entry has been removed from the cache.
- **CacheEntryUpdateCallback**: Can raise a notification that an entry is about to be removed from the cache. This setting also indicates that a cache implementation supports the ability to automatically replace the entry that is being removed with a new cache entry.

# How to use

Generic usage is the same as [MemoryCache](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.caching.memorycache?view=dotnet-plat-ext-5.0) usage. 

To setup cache location use [DefaultRepository](https://github.com/BlackGad/PS.Memory.FileCache/blob/master/PS.Memory.FileCache/Default/DefaultRepository.cs) as [FileCache](https://github.com/BlackGad/PS.Memory.FileCache/blob/master/PS.Memory.FileCache/FileCache.cs) constructor parameter. Default location is `<app folder>\Cache\`

```csharp
using (var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))))
{
    var cache = new FileCache(repository))
   //...
}
```

# Settings

Current implementation has several logically separated parts that can be changed or overridden.

## IRepository

Encapsulates all IO operations. Library contains default implementation [DefaultRepository](https://github.com/BlackGad/PS.Memory.FileCache/blob/master/PS.Memory.FileCache/Default/DefaultRepository.cs). All methods in default implementation are virtual so can be overridden. 

### Cache location

Cache location can be changed via constructor parameter.

### Key and Region name sanitizing

To enable/disable cache keys and regions sanitizing use **sanitizeNames** constructor parameter. Default value is **true**.

### Cleanup settings

Default implementation supports automatic repository cleaning within a fixed period of time (2 seconds by default) on instance dispose and via manual `DefaultRepository.Cleanup()` call. 
Clean operation means delete all files that were marked as deleted or expired.

To prevent file access issues there is delay options (5 seconds by default) which means file is allowed for deletion only after specified period.

```csharp
var cleanupSettings = new CleanupSettings //Default settings defined in static CleanupSettings.Default property
{
    GuarantyFileLifetimePeriod = TimeSpan.FromSeconds(5),
    CleanupPeriod = TimeSpan.FromSeconds(2)
};

using (var repository = new DefaultRepository(cleanupSettings: cleanupSettings))
{
   var cache = new FileCache(repository)
   //...
}
```

Also `CleanupSettings.Infinite` static property can be used to **disable** cleanup functionality.

## IMemoryCacheFacade

Fast proxy operations. [DefaultMemoryCacheFacade](https://github.com/BlackGad/PS.Memory.FileCache/blob/master/PS.Memory.FileCache/Default/DefaultMemoryCacheFacade.cs) you can configure internal items lifetime period via constructor parameter. Default value is 10 minutes.

## IDataSerializer

Controls how cache item will be serialized and deserialized. [DefaultDataSerializer](https://github.com/BlackGad/PS.Memory.FileCache/blob/master/PS.Memory.FileCache/Default/DefaultDataSerializer.cs) have 2 stage serialization:
1. Serialize CacheItem value (using [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter?view=net-5.0))
2. Serialize CacheItem itself (using binary [reader](https://docs.microsoft.com/en-us/dotnet/api/system.io.binaryreader?view=net-5.0) and [writer](https://docs.microsoft.com/en-us/dotnet/api/system.io.binarywriter?view=net-5.0)).

Cache values serialization can be easily modified.

Json data serialization/deserialization using [Json.NET](https://www.newtonsoft.com/json)

```csharp
class JsonDataSerializer : DefaultDataSerializer
{
    protected override object DeserializeData(Type type, byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject(json, type);
    }

    protected override byte[] SerializeData(Type type, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        return Encoding.UTF8.GetBytes(json);
    }
}
```
