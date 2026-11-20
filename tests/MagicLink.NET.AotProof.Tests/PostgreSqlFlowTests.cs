using System.Net;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.AotProof.Tests;

/// <summary>The flows on the MagicLink.NET PostgreSQL store, plus the device flow, which needs its token storage.</summary>
public sealed class PostgreSqlFlowTests(PostgreSqlProofHost host) : FlowTests(host), IClassFixture<PostgreSqlProofHost>
{
    [Fact]
    public async Task Device_flow_waits_for_approval_then_returns_a_token_that_opens_api_me_once()
    {
        using var device = await RequestDeviceCode();
        var deviceCode = device.RootElement.GetProperty("device_code").GetString()!;
        var userCode = device.RootElement.GetProperty("user_code").GetString()!;
        Assert.EndsWith("/connect/verify", device.RootElement.GetProperty("verification_uri").GetString(), StringComparison.Ordinal);

        using (var pending = await RedeemDeviceCode(deviceCode))
        {
            Assert.Equal(Errors.AuthorizationPending, await Error(pending));
        }

        using (var approval = await Client.GetAsync("/connect/verify?user_code=" + Uri.EscapeDataString(userCode), Cancel))
        {
            Assert.Equal(HttpStatusCode.Redirect, approval.StatusCode);
        }

        using var tokens = await RedeemDeviceCode(deviceCode);
        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        using var body = await ReadJson(tokens);
        Assert.Equal(ProofClients.TestSubject, await CallApiMe(body.RootElement.GetProperty("access_token").GetString()!));

        using var again = await RedeemDeviceCode(deviceCode);
        Assert.Equal(Errors.InvalidGrant, await Error(again));
    }

    [Fact]
    public async Task Unknown_user_code_approves_nothing()
    {
        using var device = await RequestDeviceCode();
        var deviceCode = device.RootElement.GetProperty("device_code").GetString()!;

        using (var approval = await Client.GetAsync("/connect/verify?user_code=WRONGCODE", Cancel))
        {
            Assert.NotEqual(HttpStatusCode.Redirect, approval.StatusCode);
        }

        using var pending = await RedeemDeviceCode(deviceCode);
        Assert.Equal(Errors.AuthorizationPending, await Error(pending));
    }

    private async Task<JsonDocument> RequestDeviceCode()
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["client_id"] = ProofClients.DeviceClientId });
        using var response = await Client.PostAsync("/connect/device", content, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    private Task<HttpResponseMessage> RedeemDeviceCode(string deviceCode) => PostToken(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["grant_type"] = GrantTypes.DeviceCode,
        ["client_id"] = ProofClients.DeviceClientId,
        ["device_code"] = deviceCode,
    });
}
