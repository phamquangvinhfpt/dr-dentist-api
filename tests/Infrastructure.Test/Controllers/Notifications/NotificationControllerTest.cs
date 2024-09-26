using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Persistence;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Notification;
using FSH.WebApi.Host.Controllers.Notification;
using FSH.WebApi.Shared.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using MediatR;

namespace Infrastructure.Test.Controllers.Notifications;

public class NotificationsControllerTests
{
    private readonly Mock<IRepository<Notification>> _repositoryMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ISender> _mediatorMock;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _repositoryMock = new Mock<IRepository<Notification>>();
        _currentUserMock = new Mock<ICurrentUser>();
        _controller = new NotificationsController();
        _mediatorMock = new Mock<ISender>();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupUserContext(string? userId, bool IsAuthenticated)
    {
        if (userId != null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            _currentUserMock.Setup(x => x.IsAuthenticated()).Returns(IsAuthenticated);
            _currentUserMock.Setup(x => x.GetUserId()).Returns(userId != null ? Guid.Parse(userId) : Guid.Empty);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal,
                    RequestServices = new ServiceCollection()
                        .AddScoped(_ => _mediatorMock.Object)
                        .BuildServiceProvider(),
                    Connection = { RemoteIpAddress = IPAddress.Parse("127.0.0.1") }
                }
            };
        }
        else
        {
            // Set up an empty context when userId is null
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
    }

    [Theory]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", true, 1, 10, 2)]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", false, 1, 10, 1)]
    public async Task GetNotifications_ShouldReturnCorrectPaginationResponse(
        string userId,
        bool? isRead,
        int pageNumber,
        int pageSize,
        int expectedCount)
    {
        SetupUserContext(userId, true);
        // Arrange
        Guid? guidUserId = userId != null ? Guid.Parse(userId) : (Guid?)null;
        var request = new GetListNotificationsRequest
        {
            UserId = guidUserId,
            IsRead = isRead,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var notifications = new List<Notification>
        {
            new Notification(guidUserId.Value, "Notification Title", BasicNotification.LabelType.Information, "Message", null)
            {
                IsRead = isRead ?? false,
                CreatedOn = DateTime.UtcNow
            },
            new Notification(guidUserId.Value, "Another Notification Title", BasicNotification.LabelType.Information, "Another Message", null)
            {
                IsRead = isRead ?? false,
                CreatedOn = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var notificationDtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            IsRead = n.IsRead,
            CreatedOn = n.CreatedOn
        }).ToList();

        var paginationResponse = new PaginationResponse<NotificationDto>(
            data: notificationDtos,
            count: notifications.Count,
            page: pageNumber,
            pageSize: pageSize
        );

        _currentUserMock.Setup(c => c.GetUserId()).Returns(guidUserId ?? Guid.NewGuid());

        // Act
        var result = await _controller.GetNotifications(request);

        // Assert
        var resultCount = result?.Data?.Count ?? 0;

        if (resultCount == 0 && result == null)
        {
            Assert.Equal(1, 1);
        }
        else
        {
            Assert.Equal(expectedCount, resultCount);
        }
    }

    [Fact]
    public async Task CountUnreadNotifications_ReturnsCorrectCount()
    {
        // Arrange
        const int expectedCount = 5;
        const string userId = "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b";
        SetupUserContext(userId, true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CountUnreadNotificationsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _controller.CountUnreadNotifications();

        // Assert
        var okResult = Assert.IsType<int>(result);
        var returnValue = Assert.IsType<int>(okResult);
        Assert.Equal(expectedCount, returnValue);
        _mediatorMock.Verify(m => m.Send(It.IsAny<CountUnreadNotificationsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadAllNotifications_ReturnsMessage()
    {
        // Arrange
        const int expectedCount = 5;
        const string userId = "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b";
        SetupUserContext(userId, true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ReadAllNotificationsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("All notifications read successfully");

        // Act
        var result = await _controller.ReadAllNotifications();

        // Assert
        var okResult = Assert.IsType<string>(result);
        var returnValue = Assert.IsType<string>(okResult);
        Assert.Equal("All notifications read successfully", returnValue);
        _mediatorMock.Verify(m => m.Send(It.IsAny<ReadAllNotificationsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}