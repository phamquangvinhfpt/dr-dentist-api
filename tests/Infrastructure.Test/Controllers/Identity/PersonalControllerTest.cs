using Amazon.Runtime.Internal;
using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Auditing;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Profile;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Host.Controllers.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace Infrastructure.Test.Controllers.Identity;
public class PersonalControllerTest
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ISender> _mediatorMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly PersonalController _controller;
    private readonly Mock<IWorkingCalendarService> _workingCalendarServiceMock;

    public PersonalControllerTest()
    {
        _userServiceMock = new Mock<IUserService>();
        _auditServiceMock = new Mock<IAuditService>();
        _mediatorMock = new Mock<ISender>();
        _currentUserMock = new Mock<ICurrentUser>();
        _controller = new PersonalController(_userServiceMock.Object, _auditServiceMock.Object, _workingCalendarServiceMock.Object);
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

    // getProfileDetailsOfCurrentlyUser
    [Fact]
    public async Task UC1_GetProfileDetailsOfCurrentlyUser_ReturnsUserDetailsDto()
    {
        // Arrange
        var userId = "8fcfa40a-19af-4e45-982d-e78fe340fc8e";
        var cancellationToken = CancellationToken.None;
        var userDetailsDto = new UserDetailsDto();

        _userServiceMock.Setup(x => x.GetAsync(userId, cancellationToken))
            .ReturnsAsync(userDetailsDto);

        SetupUserContext(userId, true);

        // Act
        var result = await _controller.GetProfileAsync(cancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedDto = Assert.IsType<UserDetailsDto>(okResult.Value);
        Assert.Equal(userDetailsDto, returnedDto);
    }

    //[Fact]
    //public async Task UC2_GetProfileDetailsOfCurrentlyUser_InvalidUserId_ReturnsUnauthorized()
    //{
    //    // Arrange
    //    var userId = "7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1";
    //    var cancellationToken = CancellationToken.None;
    //    var userDetailsDto = new UserDetailsDto();

    //    _userServiceMock.Setup(x => x.GetAsync(userId, cancellationToken))
    //        .ThrowsAsync(new UnauthorizedAccessException("UserId is Invalid"));

    //    SetupUserContext(userId, true);

    //    // Act
    //    var result = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.GetProfileAsync(cancellationToken));

    //    // Assert
    //    Assert.IsType<UnauthorizedAccessException>(result);
    //    Assert.Equal("UserId is Invalid", result.Message);
    //}

    //[Fact]
    //public async Task UC3_GetProfileDetailsOfCurrentlyUser_NullUserId_ReturnsUnauthorized()
    //{
    //    // Arrange
    //    string userId = "7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1";
    //    var cancellationToken = CancellationToken.None;
    //    var userDetailsDto = new UserDetailsDto();

    //    _userServiceMock.Setup(x => x.GetAsync(userId, cancellationToken))
    //        .ThrowsAsync(new UnauthorizedAccessException("UserId is Invalid"));

    //    SetupUserContext(userId, true);

    //    // Act
    //    var result = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.GetProfileAsync(cancellationToken));

    //    // Assert
    //    Assert.IsType<UnauthorizedAccessException>(result);
    //    Assert.Equal("UserId is Invalid", result.Message);
    //}

    [Fact]
    public async Task UC3_GetProfileDetailsOfCurrentlyUser_UserIdHasSpace_ReturnsUnauthorized()
    {
        // Arrange
        var userId = "8fcfa40a-19af-4e45-982d-e78fe340fc8e ";
        var cancellationToken = CancellationToken.None;
        var userDetailsDto = new UserDetailsDto();

        _userServiceMock.Setup(x => x.GetAsync(userId, cancellationToken))
            .ThrowsAsync(new UnauthorizedAccessException("UserId is Invalid"));

        SetupUserContext(userId, true);

        // Act
        var result = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.GetProfileAsync(cancellationToken));

        // Assert
        Assert.IsType<UnauthorizedAccessException>(result);
        Assert.Equal("UserId is Invalid", result.Message);
    }

    // updateProfileDetailsOfCurrentlyUser
    [Fact]
    public async Task UpdateProfileAsync_ValidUserId_CallsMediatorSend()
    {
        // Arrange
        var userId = "8fcfa40a-19af-4e45-982d-e78fe340fc8e";
        SetupUserContext(userId, true);
        var request = new UpdateUserRequest
        {
            UserId = userId,
            FirstName = "Cao Kỳ",
            LastName = "Nguyễn Văn",
            Gender = true,
            BirthDate = new DateOnly(1999, 12, 31)
        };
        var expectedResult = "success";

        _mediatorMock.Setup(m => m.Send(request, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.UpdateProfileAsync(request);

        // Assert
        _mediatorMock.Verify(m => m.Send(request, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", "Nguyễn Văn Cao Kỳ", true, "2024-07-01", true, "success")]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", " Nguyễn Văn Cao Kỳ ", false, "2024-07-01", false, "failure")]
    [InlineData(null, "Nguyễn Văn Cao Kỳ", true, "2024-07-01", false, "UserId invalid")]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", null, true, "2024-07-01", true, "Name invalid")]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", "Nguyễn Văn Cao Kỳ", null, "2024-07-01", true, "Gender invalid")]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", "Nguyễn Văn Cao Kỳ", true, null, true, "BirthDate invalid")]
    [InlineData("7fb6b2c2-3ec1-44dd-b80f-0e2632c9b2a1", "Nguyễn Văn Cao Kỳ", true, "2024-07-01", false, "failure")]
    [InlineData("8fcfa40a-19af-4e45-982d-e78fe340fc8e", "Nguyễn Văn Cao Kỳ", true, "1999-12-31", true, "success")]
    [InlineData("8fcfa40a-19af-4e45-982d-e78fe340fc8e", "", true, "1999-12-31", true, "Name invalid")]
    [InlineData("8fcfa40a-19af-4e45-982d-e78fe340fc8e", "Nguyễn Văn Cao Kỳ", false, "1999-12-31", true, "Gender invalid")]
    [InlineData("8fcfa40a-19af-4e45-982d-e78fe340fc8e", "Nguyễn Văn Cao Kỳ", true, "", true, "BirthDate invalid")]
    [InlineData("8fcfa40a-19af-4e45-982d-e78fe340fc8e", "Nguyễn Văn Cao Kỳ", true, "1999-12-31", false, "failure")]
    public async Task UpdateProfileAsync_TestsVariousConditions(
        string userId,
        string name,
        bool? gender,
        string birthDateStr,
        bool confirm,
        string expectedMessage)
    {
        // Arrange
        SetupUserContext(userId, userId != null);

        DateOnly? birthDate = string.IsNullOrEmpty(birthDateStr) ? (DateOnly?)null : DateOnly.Parse(birthDateStr);
        var request = new UpdateUserRequest
        {
            UserId = userId,
            FirstName = name,
            LastName = "Nguyễn Văn",
            Gender = gender,
            BirthDate = birthDate
        };

        var result = expectedMessage == "success" ? "success" : "failure";

        _mediatorMock.Setup(m => m.Send(request, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(result);

        if (expectedMessage == "UserId invalid")
        {
            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _controller.UpdateProfileAsync(request));
        }
        else
        {
            // Act
            var actionResult = await _controller.UpdateProfileAsync(request);

            // Assert
            Assert.Equal(result, actionResult);
        }
    }

    [Theory]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "User not found", true)]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "", "Thangdzprov!p123", "Password is required", true)] // Empty Old Password
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "", "New password is required", true)] // Empty New Password
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "Success", false)] // Valid Data
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thang!!!555", "Success", false)] // Valid Data, New Password Different
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123 ", "Success", false)] // Valid Data with Spaces
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "New password and confirm new password don't match", false)] // New Password Mismatch
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "New password must be different from old password", false)] // New Password Same as Old
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "User not found", true)] // Empty UserId
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", null, "Thangdzprov!p123", "Password is required", true)] // Null Old Password
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", null, "New password is required", true)] // Null New Password
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Thangdzprov!p123", "Thangdzprov!p123", "Confirm new password is required", true)] // Null Confirm New Password
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "", "", "Password can't be empty", true)] // Empty Passwords
    public async Task ChangePasswordAsync_TestsVariousConditions(
    string userId,
    string oldPassword,
    string newPassword,
    string expectedMessage,
    bool expectUnauthorized)
    {
        // Arrange
        SetupUserContext(userId, !string.IsNullOrEmpty(userId));
        var model = new ChangePasswordRequest
        {
            Password = oldPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = newPassword
        };

        if (expectedMessage == "Success")
        {
            _userServiceMock.Setup(s => s.ChangePasswordAsync(model))
                            .Returns(Task.CompletedTask);
        }
        else
        {
            _userServiceMock.Setup(s => s.ChangePasswordAsync(model))
                .Returns(Task.FromResult(new UnauthorizedResult()));
        }

        // Act
        var result = await _controller.ChangePasswordAsync(model);

        // Assert
        if (expectUnauthorized)
        {
            Assert.IsType<OkResult>(result);
        }
        else
        {
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);
        }
    }

    [Theory]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Create", null, true, "User not found.")]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", null, "abc", false, "Resource not found.")]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Create", "", false, "Resource not found.")]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Create", "Assignment", false, "User not found.")]
    [InlineData("05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", "Create", "abc", false, "Resource not found.")]
    public async Task GetLogsAsync_TestsVariousConditions(string userId, string action, string resource, bool isAuthenticated, string expectedMessage)
    {
        // Arrange
        SetupUserContext(userId, isAuthenticated);

        var request = new GetMyAuditLogsRequest
        {
            Action = action,
            Resource = resource,
            PageNumber = 1,
            PageSize = 10
        };

        if (isAuthenticated)
        {
            _auditServiceMock.Setup(s => s.GetUserTrailsAsync(It.IsAny<GetMyAuditLogsRequest>()))
                             .ReturnsAsync(new PaginationResponse<AuditDto>(new List<AuditDto>(), 0, 1, 10));
        }
        else
        {
            _auditServiceMock.Setup(s => s.GetUserTrailsAsync(It.IsAny<GetMyAuditLogsRequest>()))
                             .ThrowsAsync(new Exception("User not found."));
        }

        // Act
        IActionResult result;
        try
        {
            result = (IActionResult)await _controller.GetLogsAsync(request);
        }
        catch (Exception ex)
        {
            result = new BadRequestObjectResult(ex.Message);
        }

        // Assert
        if (isAuthenticated && !string.IsNullOrEmpty(resource))
        {
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult);
            var paginationResponse = Assert.IsType<PaginationResponse<AuditDto>>(okResult.Value);
            Assert.NotNull(paginationResponse);
            Assert.Equal("Success", expectedMessage);
        }
        else
        {
            if (result == null)
            {
                Assert.True(expectedMessage.Contains("not found"), "Expected BadRequestObjectResult but got null.");
            }
            else
            {
                var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
                Assert.NotNull(badRequestResult);
                Assert.Equal(expectedMessage, badRequestResult.Value.ToString());
            }
        }
    }

    [Theory]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", true, true, false, true, "Avatar updated successfully.")]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", true, false, false, true, "Avatar updated successfully.")]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", false, false, true, true, "Avatar updated successfully.")]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", false, true, false, true, "Avatar updated successfully.")]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", false, false, false, true, "Avatar updated successfully.")]
    [InlineData( "05f0097f-a9ef-4d10-a0a2-6efd2831cc5b", true, true, false, true, "Avatar updated successfully.")]
    public async Task UpdateAvatarAsync_ShouldHandleVariousInputs(
        string userId,
        bool hasImage,
        bool deleteCurrentImage,
        bool isAuthenticated,
        bool expectedSuccess,
        string expectedMessage)
    {
        // Arrange
        SetupUserContext(userId, isAuthenticated);

        var request = new UpdateAvatarRequest
        {
            UserId = userId,
            DeleteCurrentImage = deleteCurrentImage
        };

        if (hasImage)
        {
            request.Image = new FormFile(
                baseStream: new MemoryStream(),
                baseStreamOffset: 0,
                length: 1024,
                name: "testImage",
                fileName: "test.jpg"
            );
        }

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateAvatarRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessage);

        // Act
        var result = await _controller.UpdateAvatarAsync(request);

        // Assert
        if (expectedSuccess)
        {
            var okResult = Assert.IsType<string>(result);
            Assert.Equal(expectedMessage, okResult);
        }
        else
        {
            if (userId == null)
            {
                Assert.IsType<UnauthorizedAccessException>(result);
            }
            else
            {
                var notFoundResult = Assert.IsType<string>(result);
                Assert.Equal(expectedMessage, notFoundResult);
            }
        }

        _mediatorMock.Verify(m => m.Send(It.IsAny<UpdateAvatarRequest>(), It.IsAny<CancellationToken>()),
        expectedSuccess ? Times.Once() : Times.Never(),
        "Mediator.Send should be called based on expected success state.");
    }
}
