using System.Net;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PixelPay.IntegrationTests.Support;

namespace PixelPay.IntegrationTests;

public class EnvelopeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
        s.AddControllers().ConfigureApplicationPartManager(m =>
            m.ApplicationParts.Add(new AssemblyPart(typeof(EnvelopeTestController).Assembly)));
        s.AddScoped<IValidator<NamedDto>, NamedDtoValidator>();
    })).CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task AssertResponse(HttpResponseMessage res, HttpStatusCode status, string body)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(body, await res.Content.ReadAsStringAsync(Ct));
    }

    private Task<HttpResponseMessage> PostJson(string url, string json) =>
        _client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), Ct);

    [Fact]
    public async Task Object_is_wrapped() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/object", Ct), HttpStatusCode.OK,
            """{"success":true,"data":{"id":1,"at":"2026-10-26T04:00:45.100Z","amount":"12.5"}}""");

    [Fact]
    public async Task Null_is_wrapped_as_null_data() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/null", Ct), HttpStatusCode.OK,
            """{"success":true,"data":null}""");

    [Fact]
    public async Task Api_exception_renders_nest_body() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/not-found", Ct), HttpStatusCode.NotFound,
            """{"success":false,"error":{"statusCode":404,"message":"Wallet not found","error":"Not Found"}}""");

    [Fact]
    public async Task Bare_unauthorized_has_no_error_key() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/unauthorized", Ct), HttpStatusCode.Unauthorized,
            """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""");

    [Fact]
    public async Task Unhandled_exception_is_500() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/crash", Ct), HttpStatusCode.InternalServerError,
            """{"success":false,"error":{"message":"Internal server error"}}""");

    [Fact]
    public async Task Unknown_route_is_nest_404() =>
        await AssertResponse(await _client.GetAsync("/api/v1/nope?x=1", Ct), HttpStatusCode.NotFound,
            """{"success":false,"error":{"statusCode":404,"message":"Cannot GET /api/v1/nope?x=1","error":"Not Found"}}""");

    [Fact]
    public async Task Bare_ok_is_wrapped_with_null_data() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/bare-ok", Ct), HttpStatusCode.OK,
            """{"success":true,"data":null}""");

    [Fact]
    public async Task Bare_not_found_renders_nest_body() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/bare-not-found", Ct), HttpStatusCode.NotFound,
            """{"success":false,"error":{"statusCode":404,"message":"Not Found"}}""");

    [Fact]
    public async Task Bare_unauthorized_result_renders_nest_body() =>
        await AssertResponse(await _client.GetAsync("/api/v1/envelope-test/bare-unauthorized", Ct), HttpStatusCode.Unauthorized,
            """{"success":false,"error":{"statusCode":401,"message":"Unauthorized"}}""");

    [Fact]
    public async Task Non_json_body_is_validated_as_empty_object()
    {
        var res = await _client.PostAsync("/api/v1/envelope-test/named", new StringContent("hello", Encoding.UTF8, "text/plain"), Ct);

        await AssertResponse(res, HttpStatusCode.BadRequest,
            """{"success":false,"error":{"statusCode":400,"message":["name should not be empty"],"error":"Bad Request"}}""");
    }

    [Fact]
    public async Task Wrong_method_is_nest_404_not_405() =>
        await AssertResponse(await PostJson("/api/v1/health", "{}"), HttpStatusCode.NotFound,
            """{"success":false,"error":{"statusCode":404,"message":"Cannot POST /api/v1/health","error":"Not Found"}}""");

    [Fact]
    public async Task Unknown_property_and_validator_messages_share_one_list() =>
        await AssertResponse(await PostJson("/api/v1/envelope-test/named", """{"foo":1}"""), HttpStatusCode.BadRequest,
            """{"success":false,"error":{"statusCode":400,"message":["property foo should not exist","name should not be empty"],"error":"Bad Request"}}""");

    [Fact]
    public async Task Unknown_property_is_rejected()
    {
        var res = await PostJson("/api/v1/envelope-test/named", """{ "name":"a", "foo":1 }""");

        await AssertResponse(res, HttpStatusCode.BadRequest,
            """{"success":false,"error":{"statusCode":400,"message":["property foo should not exist"],"error":"Bad Request"}}""");
    }

    [Fact]
    public async Task Valid_post_defaults_to_201() =>
        await AssertResponse(await PostJson("/api/v1/envelope-test/named", """{"name":"a"}"""), HttpStatusCode.Created,
            """{"success":true,"data":{"name":"a"}}""");

    [Fact]
    public async Task Dto_without_validator_accepts_unknown_properties() =>
        await AssertResponse(await PostJson("/api/v1/envelope-test/loose", """{"name":"a","foo":1}"""), HttpStatusCode.OK,
            """{"success":true,"data":{"name":"a"}}""");

    [Theory]
    [InlineData("""{"name":""")]
    [InlineData("")] // Deliberate deviation (R8): Nest returns 500 for an empty JSON body.
    public async Task Malformed_or_empty_json_is_400_with_string_message(string body) =>
        await AssertResponse(await PostJson("/api/v1/envelope-test/loose", body), HttpStatusCode.BadRequest,
            """{"success":false,"error":{"statusCode":400,"message":"Body is not valid JSON but content-type is set to 'application/json'","error":"Bad Request"}}""");

    [Fact]
    public async Task Error_responses_keep_cors_headers()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/envelope-test/not-found");
        req.Headers.Add("Origin", "http://localhost:3001");

        var res = await _client.SendAsync(req, Ct);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("http://localhost:3001", Assert.Single(res.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Head_runs_get_without_body()
    {
        var res = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/v1/health"), Ct);

        await AssertResponse(res, HttpStatusCode.OK, "");
    }
}
