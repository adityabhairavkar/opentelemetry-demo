// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System;

using cartservice.cartstore;
using cartservice.featureflags;
using cartservice.services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenTelemetry.Metrics;
using OpenTelemetry.ResourceDetectors.Container;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using LogLevel = NLog.LogLevel;
using NLog.Web;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
string redisAddress = builder.Configuration["REDIS_ADDR"];
RedisCartStore cartStore = null;
if (string.IsNullOrEmpty(redisAddress))
{
    Console.WriteLine("REDIS_ADDR environment variable is required.");
    Environment.Exit(1);
}
cartStore = new RedisCartStore(redisAddress);

// Initialize the redis store
await cartStore.InitializeAsync();
Console.WriteLine("Initialization completed");
try
{

    builder.Services.AddSingleton<ICartStore>(cartStore);
    builder.Services.AddSingleton<FeatureFlagHelper>();
    builder.Logging.ClearProviders();
    LogManager.Configuration = new XmlLoggingConfiguration("NLog.config");
    builder.Host.UseNLog();
  
    Console.WriteLine("NLOG Initialization completed");

    // see https://opentelemetry.io/docs/instrumentation/net/getting-started/

    Action<ResourceBuilder> appResourceBuilder =
        resource => resource
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector()
            .AddDetector(new ContainerResourceDetector());

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(appResourceBuilder)
        .WithTracing(builder => builder
            .AddRedisInstrumentation(
                cartStore.GetConnection(),
                options => options.SetVerboseDatabaseStatements = true)
            .AddAspNetCoreInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(builder => builder
            .AddRuntimeInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter());

    builder.Services.AddGrpc();
    builder.Services.AddGrpcHealthChecks()
        .AddCheck("Sample", () => HealthCheckResult.Healthy());
}
catch (Exception exception)
{
    // NLog: catch setup errors
    Console.WriteLine(exception +"Stopped program because of exception");
    throw;
}
var app = builder.Build();

app.MapGrpcService<CartService>();
app.MapGrpcHealthChecksService();

app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
});

app.Run();
