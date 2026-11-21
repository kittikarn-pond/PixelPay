using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests.Users;

public sealed class UsersEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test1234!";
    private const string BareUnauthorized = """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""";
    private const string InternalError = """{"success":false,"error":{"message":"Internal server error"}}""";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"u{Guid.NewGuid():N}@pixelpay.test";

    private async Task<(string Id, string Email, string Token)> RegisterUser()
    {
        var email = NewEmail();
        var res = await Send(HttpMethod.Post, "/api/v1/auth/register",
            JsonSerializer.Serialize(new { email, password = Password, displayName = "Test User" }));
        var data = await Data(res, HttpStatusCode.Created);
        return (data.GetProperty("user").GetProperty("id").GetString()!, email, data.GetProperty("accessToken").GetString()!);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string? json = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (json is not null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req, Ct);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage res, HttpStatusCode status)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.StatusCode == status, $"{res.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static async Task AssertBody(HttpResponseMessage res, HttpStatusCode status, string body)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(body, await res.Content.ReadAsStringAsync(Ct));
    }

    private static string Validation(params string[] messages) =>
        $$$"""{"success":false,"error":{"statusCode":400,"message":{{{JsonSerializer.Serialize(messages)}}},"error":"Bad Request"}}""";

    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public async Task Profile_returns_selected_fields()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/users/profile", token: u.Token), HttpStatusCode.OK);

        Assert.Equal(["id", "email", "phone", "displayName", "avatarUrl", "role", "isVerified", "referralCode", "createdAt"], Keys(data));
        Assert.Equal(u.Id, data.GetProperty("id").GetString());
        Assert.Equal(u.Email, data.GetProperty("email").GetString());
        Assert.Equal("Test User", data.GetProperty("displayName").GetString());
        Assert.Equal("USER", data.GetProperty("role").GetString());
        Assert.False(data.GetProperty("isVerified").GetBoolean());
        Assert.Matches("^[A-Z0-9]{8}$", data.GetProperty("referralCode").GetString());
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", data.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Profile_includes_null_phone()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Get, "/api/v1/users/profile", token: u.Token), HttpStatusCode.OK);

        Assert.Equal(JsonValueKind.Null, data.GetProperty("phone").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Profile_of_deleted_user_returns_500()
    {
        var u = await RegisterUser();
        await using (var db = factory.Db())
        {
            await db.Wallets.Where(w => w.UserId == u.Id).ExecuteDeleteAsync(Ct);
            await db.Users.Where(x => x.Id == u.Id).ExecuteDeleteAsync(Ct);
        }

        await AssertBody(await Send(HttpMethod.Get, "/api/v1/users/profile", token: u.Token), HttpStatusCode.InternalServerError, InternalError);
    }

    [Theory]
    [InlineData("GET", "/api/v1/users/profile")]
    [InlineData("PATCH", "/api/v1/users/profile")]
    [InlineData("PUT", "/api/v1/users/change-password")]
    public async Task Routes_require_auth(string method, string url) =>
        await AssertBody(await Send(new HttpMethod(method), url, "{}"), HttpStatusCode.Unauthorized, BareUnauthorized);

    [Fact]
    public async Task Patch_updates_display_name_and_stamps_updated_at()
    {
        var u = await RegisterUser();
        DateTime before;
        await using (var db = factory.Db())
            before = (await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id, Ct)).UpdatedAt;
        await Task.Delay(1100, Ct);

        var data = await Data(await Send(HttpMethod.Patch, "/api/v1/users/profile", """{"displayName":"Updated Name"}""", u.Token),
            HttpStatusCode.OK);

        Assert.Equal(["id", "email", "displayName", "avatarUrl"], Keys(data));
        Assert.Equal("Updated Name", data.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("avatarUrl").ValueKind);
        await using var db2 = factory.Db();
        var saved = await db2.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id, Ct);
        Assert.Equal("Updated Name", saved.DisplayName);
        Assert.True(saved.UpdatedAt > before);
    }

    [Fact]
    public async Task Patch_sets_avatar_and_leaves_absent_fields_alone()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Patch, "/api/v1/users/profile", """{"avatarUrl":"https://x.test/a.png"}""", u.Token),
            HttpStatusCode.OK);

        Assert.Equal("https://x.test/a.png", data.GetProperty("avatarUrl").GetString());
        Assert.Equal("Test User", data.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Patch_null_avatar_clears_it()
    {
        var u = await RegisterUser();
        await Data(await Send(HttpMethod.Patch, "/api/v1/users/profile", """{"avatarUrl":"https://x.test/a.png"}""", u.Token), HttpStatusCode.OK);

        var data = await Data(await Send(HttpMethod.Patch, "/api/v1/users/profile", """{"avatarUrl":null}""", u.Token), HttpStatusCode.OK);

        Assert.Equal(JsonValueKind.Null, data.GetProperty("avatarUrl").ValueKind);
        await using var db = factory.Db();
        Assert.Null((await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id, Ct)).AvatarUrl);
    }

    [Fact]
    public async Task Patch_null_display_name_returns_500_and_changes_nothing()
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Patch, "/api/v1/users/profile",
            """{"displayName":null,"avatarUrl":"https://x.test/a.png"}""", u.Token), HttpStatusCode.InternalServerError, InternalError);

        await using var db = factory.Db();
        var saved = await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id, Ct);
        Assert.Equal("Test User", saved.DisplayName);
        Assert.Null(saved.AvatarUrl);
    }

    [Fact]
    public async Task Patch_empty_body_returns_current_profile()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Patch, "/api/v1/users/profile", "{}", u.Token), HttpStatusCode.OK);

        Assert.Equal("Test User", data.GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("""{"displayName":"a"}""", "displayName must be longer than or equal to 2 characters")]
    [InlineData("""{"displayName":5}""",
        "displayName must be shorter than or equal to 50 characters|displayName must be longer than or equal to 2 characters|displayName must be a string")]
    [InlineData("""{"avatarUrl":5}""", "avatarUrl must be a string")]
    [InlineData("""{"role":"ADMIN"}""", "property role should not exist")]
    public async Task Patch_validates(string json, string messages)
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Patch, "/api/v1/users/profile", json, u.Token), HttpStatusCode.BadRequest,
            Validation(messages.Split('|')));
    }

    [Fact]
    public async Task Patch_too_long_display_name_returns_400()
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Patch, "/api/v1/users/profile",
                JsonSerializer.Serialize(new { displayName = new string('a', 51) }), u.Token),
            HttpStatusCode.BadRequest, Validation("displayName must be shorter than or equal to 50 characters"));
    }

    [Fact]
    public async Task ChangePassword_updates_hash_and_allows_login_with_new_password()
    {
        var u = await RegisterUser();

        var data = await Data(await Send(HttpMethod.Put, "/api/v1/users/change-password",
            $$"""{"currentPassword":"{{Password}}","newPassword":"NewPass123"}""", u.Token), HttpStatusCode.OK);

        Assert.Equal(["message"], Keys(data));
        Assert.Equal("Password changed successfully", data.GetProperty("message").GetString());
        var login = await Send(HttpMethod.Post, "/api/v1/auth/login", JsonSerializer.Serialize(new { email = u.Email, password = "NewPass123" }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var old = await Send(HttpMethod.Post, "/api/v1/auth/login", JsonSerializer.Serialize(new { email = u.Email, password = Password }));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        await using var db = factory.Db();
        Assert.StartsWith("$2b$12$", (await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id, Ct)).PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_wrong_current_returns_400()
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Put, "/api/v1/users/change-password",
                """{"currentPassword":"Wrong1234","newPassword":"NewPass123"}""", u.Token),
            HttpStatusCode.BadRequest,
            """{"success":false,"error":{"statusCode":400,"message":"Current password is incorrect","error":"Bad Request"}}""");
    }

    [Theory]
    [InlineData("""{"currentPassword":"x","newPassword":"alllowercase1"}""", "newPassword must match /^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)/ regular expression")]
    [InlineData("""{"currentPassword":"x","newPassword":"Ab1"}""", "newPassword must be longer than or equal to 8 characters")]
    [InlineData("""{"currentPassword":"x"}""",
        "newPassword must match /^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)/ regular expression|newPassword must be shorter than or equal to 72 characters|newPassword must be longer than or equal to 8 characters|newPassword must be a string")]
    [InlineData("""{"newPassword":"NewPass123"}""", "currentPassword must be a string")]
    [InlineData("""{"currentPassword":5,"newPassword":"NewPass123"}""", "currentPassword must be a string")]
    [InlineData("""{"currentPassword":"x","newPassword":"NewPass123","extra":1}""", "property extra should not exist")]
    public async Task ChangePassword_validates(string json, string messages)
    {
        var u = await RegisterUser();

        await AssertBody(await Send(HttpMethod.Put, "/api/v1/users/change-password", json, u.Token), HttpStatusCode.BadRequest,
            Validation(messages.Split('|')));
    }
}
