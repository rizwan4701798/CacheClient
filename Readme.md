# Cache Client (.NET)

## Overview
The Cache Client is a thin .NET client library that provides a simple abstraction for interacting with the Cache Server. It hides all networking and serialization details.

## Design Goals
- Thin client (no cache logic)
- Simple ICache API
- Easy integration
- Distributed as a NuGet package

## Public Interface
ICache provides methods for Add, Get, Update, Remove, Clear, Initialize, and Dispose.

## Configuration
CacheClientOptions:
- Host
- Port
- TimeoutMilliseconds

## Example Usage
using CacheClientLib = CacheClient;

ICache cache = new CacheClientLib.CacheClient(
    new CacheClientLib.CacheClientOptions
    {
        Host = "localhost",
        Port = 5050
    });

cache.Initialize();
cache.Add("key", "value");
var value = cache.Get("key");
cache.Dispose();

## Error Handling
The client throws CacheClientException for server-side errors and InvalidOperationException if used before initialization.

## Testing
- Unit tests using xUnit, Moq, FluentAssertions
- Integration tests via test console app

## NuGet Packaging
dotnet pack CacheClient.csproj -c Release

## Summary
The Cache Client provides a lightweight and clean interface for consuming the remote cache server from .NET applications.
