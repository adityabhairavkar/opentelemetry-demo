// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using cartservice.services;
using Microsoft.Extensions.Logging;
using Oteldemo;

namespace cartservice.featureflags;

public class FeatureFlagHelper
{
    private readonly static Random Random = new Random();
    private readonly FeatureFlagService.FeatureFlagServiceClient _featureFlagServiceClient;
    private readonly ILogger<FeatureFlagHelper> _logger;

    public FeatureFlagHelper(ILogger<FeatureFlagHelper> logger)
    {
        var featureFlagServiceUri = new Uri($"http://{Environment.GetEnvironmentVariable("FEATURE_FLAG_GRPC_SERVICE_ADDR")}");
        var channel = Grpc.Net.Client.GrpcChannel.ForAddress(featureFlagServiceUri);
        _featureFlagServiceClient = new FeatureFlagService.FeatureFlagServiceClient(channel);
        _logger = logger;
    }

    public async Task<bool> GenerateCartError()
    {
        if (Random.Next(3) != 1)
        {
            return false;
        }
        var getFlagRequest = new GetFlagRequest { Name = "cartServiceFailure" };
        var getFlagResponse = await _featureFlagServiceClient.GetFlagAsync(getFlagRequest);
        _logger.LogInformation(" CartServiceFailure Flag" + getFlagResponse.Flag.Enabled);
        return getFlagResponse.Flag.Enabled;
    }
}
