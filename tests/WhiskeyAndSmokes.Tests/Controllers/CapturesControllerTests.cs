using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class CapturesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static string TestUserId => CustomWebApplicationFactory.TestUserId;

    public CapturesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUploadUrl_ReturnsOk()
    {
        _factory.BlobStorage.GenerateUploadUrlAsync(TestUserId, "photo.jpg")
            .Returns(("https://upload.example.com/sas", "https://blob.example.com/photo.jpg"));

        var response = await _client.GetAsync("/api/captures/upload-url?fileName=photo.jpg");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UploadUrlResponse>();
        body.Should().NotBeNull();
        body!.UploadUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetUploadUrl_MissingFileName_Returns400()
    {
        var response = await _client.GetAsync("/api/captures/upload-url");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUploadUrl_InvalidExtension_Returns400()
    {
        var response = await _client.GetAsync("/api/captures/upload-url?fileName=test.exe");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCapture_ReturnsCreated()
    {
        _factory.CosmosDb.CreateAsync("captures", Arg.Any<Capture>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Capture>(1));

        var request = new CreateCaptureRequest
        {
            Photos = ["https://blob.example.com/photo1.jpg"],
            UserNote = "Nice whiskey"
        };
        var response = await _client.PostAsJsonAsync("/api/captures", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CaptureResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(CaptureStatus.Pending);
    }

    [Fact]
    public async Task GetCapture_ReturnsOk()
    {
        var capture = new Capture
        {
            Id = "cap-1",
            UserId = TestUserId,
            Photos = ["https://blob.example.com/photo.jpg"],
            Status = CaptureStatus.Completed
        };
        _factory.CosmosDb.GetAsync<Capture>("captures", "cap-1", TestUserId).Returns(capture);

        var response = await _client.GetAsync("/api/captures/cap-1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CaptureResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be("cap-1");
    }

    [Fact]
    public async Task GetCapture_NotFound_Returns404()
    {
        _factory.CosmosDb.GetAsync<Capture>("captures", "nonexistent", TestUserId)
            .Returns((Capture?)null);

        var response = await _client.GetAsync("/api/captures/nonexistent");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListCaptures_ReturnsOk()
    {
        var captures = new List<Capture>
        {
            new() { Id = "cap-1", UserId = TestUserId, Status = CaptureStatus.Completed },
            new() { Id = "cap-2", UserId = TestUserId, Status = CaptureStatus.Pending }
        };

        _factory.CosmosDb.QueryAsync<Capture>(
            "captures",
            TestUserId,
            Arg.Any<string?>(),
            Arg.Any<int>(),
            Arg.Any<Expression<Func<Capture, bool>>?>())
            .Returns((captures, (string?)null));

        var response = await _client.GetAsync("/api/captures");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CaptureResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReprocessCapture_ReturnsOk()
    {
        var capture = new Capture
        {
            Id = "cap-1",
            UserId = TestUserId,
            Status = CaptureStatus.Completed,
            ItemIds = ["item-1"]
        };
        _factory.CosmosDb.GetAsync<Capture>("captures", "cap-1", TestUserId).Returns(capture);
        _factory.CosmosDb.UpsertAsync("captures", Arg.Any<Capture>(), Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Capture>(1));

        var response = await _client.PostAsync("/api/captures/cap-1/reprocess", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReprocessCapture_NotFound_Returns404()
    {
        _factory.CosmosDb.GetAsync<Capture>("captures", "not-found", TestUserId)
            .Returns((Capture?)null);

        var response = await _client.PostAsync("/api/captures/not-found/reprocess", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReprocessCapture_AlreadyProcessing_Returns409()
    {
        var capture = new Capture
        {
            Id = "cap-processing",
            UserId = TestUserId,
            Status = CaptureStatus.Processing
        };
        _factory.CosmosDb.GetAsync<Capture>("captures", "cap-processing", TestUserId)
            .Returns(capture);

        var response = await _client.PostAsync("/api/captures/cap-processing/reprocess", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
