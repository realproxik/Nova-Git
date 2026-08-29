using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaCrypto;

/// <summary>GitHub's OAuth device flow. Tokens remain in memory and are never logged or persisted.</summary>
static class GitHubOAuth
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<string> LoginAsync(string clientId, IWin32Window owner)
    {
        if (clientId.Any(char.IsWhiteSpace)) throw new ArgumentException("The OAuth client ID cannot contain spaces.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = "read:user" }) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await Client.SendAsync(request); response.EnsureSuccessStatusCode();
        var device = await JsonSerializer.DeserializeAsync<DeviceCodeResponse>(await response.Content.ReadAsStreamAsync(), JsonOptions) ?? throw new InvalidOperationException("GitHub returned an invalid device code response.");
        if (string.IsNullOrWhiteSpace(device.DeviceCode) || string.IsNullOrWhiteSpace(device.UserCode) || string.IsNullOrWhiteSpace(device.VerificationUri)) throw new InvalidOperationException("GitHub returned an incomplete device code response.");
        Process.Start(new ProcessStartInfo(device.VerificationUri) { UseShellExecute = true });
        MessageBox.Show(owner, $"Your browser was opened to GitHub. Enter this one-time code:\n\n{device.UserCode}\n\nNovaGit will wait for approval.", "GitHub sign in", MessageBoxButtons.OK, MessageBoxIcon.Information);
        var delay = Math.Max(5, device.Interval ?? 5);
        var expires = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn);
        while (DateTimeOffset.UtcNow < expires)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId, ["device_code"] = device.DeviceCode, ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code" }) };
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var tokenResponse = await Client.SendAsync(tokenRequest); tokenResponse.EnsureSuccessStatusCode();
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(await tokenResponse.Content.ReadAsStreamAsync(), JsonOptions) ?? throw new InvalidOperationException("GitHub returned an invalid authorization response.");
            if (!string.IsNullOrWhiteSpace(token.AccessToken)) return await GetLogin(token.AccessToken);
            if (token.Error == "slow_down") delay += 5;
            else if (token.Error is not "authorization_pending") throw new InvalidOperationException(token.ErrorDescription ?? token.Error ?? "GitHub authorization failed.");
        }
        throw new TimeoutException("GitHub authorization timed out. Start sign-in again.");
    }

    static async Task<string> GetLogin(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user"); request.Headers.UserAgent.ParseAdd("NovaGit-Desktop"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Client.SendAsync(request); response.EnsureSuccessStatusCode();
        var user = await JsonSerializer.DeserializeAsync<GitHubUser>(await response.Content.ReadAsStreamAsync(), JsonOptions); return user?.Login ?? "GitHub user";
    }
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    sealed record DeviceCodeResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int? Interval);
    sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken, string? Error, [property: JsonPropertyName("error_description")] string? ErrorDescription);
    sealed record GitHubUser(string? Login);
}
