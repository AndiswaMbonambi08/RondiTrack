using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using RondiTrack.Dtos;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class LogLeakTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task No_personal_or_secret_data_reaches_a_log_and_a_409_is_not_an_Error()
    {
        // Tests in this collection run one at a time, so everything after this index is ours.
        var start = factory.Logs.Entries.Count;

        var email = $"{Guid.NewGuid():N}@leaktest.example.com";
        const string password = "Correct-Horse-Battery-9";
        const string fullName = "Nomsa Leakcheck";
        const string stokvelName = "Leakcheck Savings Circle";
        const decimal amount = 1234.56m;

        // No automatic cookies: the test replays the refresh cookie by hand, whatever it is called.
        var anon = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        anon.DefaultRequestHeaders.Authorization = null;

        // 1. register
        var register = await anon.PostAsJsonAsync("/api/auth/register", new { fullName, email, password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = (await register.Content.ReadFromJsonAsync<UserResponse>(JsonOptions.CaseInsensitive))!;

        // 2. log in
        var login = await anon.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var firstAccess = (await login.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions.CaseInsensitive))!.AccessToken;
        var firstCookie = CookiePair(login);

        // 3. refresh, sending the cookie from the login
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", firstCookie);
        var refresh = await anon.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var secondAccess = (await refresh.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions.CaseInsensitive))!.AccessToken;
        var secondCookie = CookiePair(refresh);

        // 4. record a contribution with a distinctive amount (setup runs as the platform-admin test user)
        var admin = factory.CreateClient();
        var stokvel = (await (await admin.PostAsJsonAsync("/api/stokvels",
                new { name = stokvelName, contributionAmount = amount }))
            .Content.ReadFromJsonAsync<StokvelResponse>(JsonOptions.CaseInsensitive))!;

        var add = await admin.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { userId = user.Id });
        Assert.True(add.IsSuccessStatusCode, "AddMember: " + (int)add.StatusCode);

        var cycle = (await (await admin.PostAsJsonAsync("/api/contribution-cycles",
                new { stokvelId = stokvel.Id, label = "2026-10", targetAmount = amount }))
            .Content.ReadFromJsonAsync<ContributionCycleResponse>(JsonOptions.CaseInsensitive))!;

        using var contribution = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvel.Id}/contributions")
        {
            Content = JsonContent.Create(new { userId = user.Id, contributionCycleId = cycle.Id, amount })
        };
        contribution.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var recorded = await admin.SendAsync(contribution);
        Assert.True(recorded.StatusCode == HttpStatusCode.Created,
            "Record: " + (int)recorded.StatusCode + " " + await recorded.Content.ReadAsStringAsync());

        // 5. duplicate registration: a 409 is the caller's mistake, never an Error
        var beforeDuplicate = factory.Logs.Entries.Count;
        var duplicate = await anon.PostAsJsonAsync("/api/auth/register", new { fullName, email, password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var duplicateLogs = factory.Logs.Entries.Skip(beforeDuplicate).ToList();
        Assert.NotEmpty(duplicateLogs);
        Assert.DoesNotContain(duplicateLogs, e => e.Level >= LogLevel.Error);

        // 6. nothing personal, financial or secret in any message or exception text
        var secrets = new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["full name"] = fullName,
            ["stokvel name"] = stokvelName,
            ["amount"] = "1234.56",
            ["first access token"] = firstAccess,
            ["second access token"] = secondAccess,
            ["first refresh token"] = CookieValue(firstCookie),
            ["first refresh token (decoded)"] = Uri.UnescapeDataString(CookieValue(firstCookie)),
            ["second refresh token"] = CookieValue(secondCookie),
            ["second refresh token (decoded)"] = Uri.UnescapeDataString(CookieValue(secondCookie)),
        };

        // A short or empty secret would make "not found in the logs" meaningless.
        foreach (var (label, secret) in secrets)
            Assert.True(secret.Length >= 6, $"{label} is too short to be a meaningful check");

        var ours = factory.Logs.Entries.Skip(start).ToList();
        Assert.NotEmpty(ours);

        foreach (var (label, secret) in secrets)
        {
            var hit = ours.FirstOrDefault(e =>
                (e.Message + " " + e.ExceptionText).Contains(secret, StringComparison.OrdinalIgnoreCase));
            Assert.True(hit is null, $"{label} leaked into a log: [{hit?.Level}] {hit?.Category}");
        }
    }

    // "name=value" from the first Set-Cookie header.
    private static string CookiePair(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").First().Split(';')[0];

    private static string CookieValue(string pair) => pair[(pair.IndexOf('=') + 1)..];
}