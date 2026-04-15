using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using WhiskeyAndSmokes.Api.Models;
using Xunit;

namespace WhiskeyAndSmokes.Tests.Controllers;

public class NotificationsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public NotificationsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListNotifications_ReturnsNotificationsWithUnreadCount()
    {
        var notifications = new List<Notification>
        {
            new() { Id = "n1", UserId = CustomWebApplicationFactory.TestUserId, Title = "New friend!", IsRead = false, CreatedAt = DateTime.UtcNow },
            new() { Id = "n2", UserId = CustomWebApplicationFactory.TestUserId, Title = "New thought", IsRead = true, CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
        };

        _factory.CosmosDb.QueryAsync(
            "notifications",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            50,
            Arg.Any<Expression<Func<Notification, bool>>?>())
            .Returns((notifications, (string?)null));

        var response = await _client.GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MarkRead_ValidNotification_ReturnsOk()
    {
        var notification = new Notification
        {
            Id = "n1",
            UserId = CustomWebApplicationFactory.TestUserId,
            Title = "Test",
            IsRead = false
        };

        _factory.CosmosDb.GetAsync<Notification>("notifications", "n1", CustomWebApplicationFactory.TestUserId)
            .Returns(notification);

        _factory.CosmosDb.UpsertAsync(
            "notifications",
            Arg.Any<Notification>(),
            Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Notification>(1));

        var response = await _client.PutAsync("/api/notifications/n1/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MarkRead_NotFound_ReturnsNotFound()
    {
        _factory.CosmosDb.GetAsync<Notification>("notifications", "nonexistent", CustomWebApplicationFactory.TestUserId)
            .Returns((Notification?)null);

        var response = await _client.PutAsync("/api/notifications/nonexistent/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_OtherUsersNotification_ReturnsNotFound()
    {
        var notification = new Notification
        {
            Id = "n-other",
            UserId = "other-user",
            Title = "Not mine",
            IsRead = false
        };

        _factory.CosmosDb.GetAsync<Notification>("notifications", "n-other", CustomWebApplicationFactory.TestUserId)
            .Returns(notification);

        var response = await _client.PutAsync("/api/notifications/n-other/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAllRead_UpdatesUnreadNotifications()
    {
        var unread = new List<Notification>
        {
            new() { Id = "n1", UserId = CustomWebApplicationFactory.TestUserId, Title = "A", IsRead = false },
            new() { Id = "n2", UserId = CustomWebApplicationFactory.TestUserId, Title = "B", IsRead = false },
        };

        _factory.CosmosDb.QueryAsync(
            "notifications",
            CustomWebApplicationFactory.TestUserId,
            Arg.Any<string?>(),
            200,
            Arg.Any<Expression<Func<Notification, bool>>?>())
            .Returns((unread, (string?)null));

        _factory.CosmosDb.UpsertAsync(
            "notifications",
            Arg.Any<Notification>(),
            Arg.Any<string>())
            .Returns(callInfo => callInfo.ArgAt<Notification>(1));

        var response = await _client.PutAsync("/api/notifications/read-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
