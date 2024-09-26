using System.Net;
using System.Security.Claims;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Verify;
using FSH.WebApi.Host.Controllers.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Infrastructure.Test.Controllers.Identity;
public class UsersControllerTest
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ISender> _mediatorMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly UsersController _controller;
    private readonly IStringLocalizer _t;

    public UsersControllerTest()
    {
        _userServiceMock = new Mock<IUserService>();
        _mediatorMock = new Mock<ISender>();
        _currentUserMock = new Mock<ICurrentUser>();
        _controller = new UsersController(_userServiceMock.Object);
        _t = new Mock<IStringLocalizer>().Object;
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

    public void SetRequestContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-from-host"] = "testhost";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost:5001");
        httpContext.Request.PathBase = "/basepath";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    // Test for the GetUserDetail

    [Fact]
    public async Task GetByIdAsync_ValidId_ReturnsUserDetails()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        var userDetails = new UserDetailsDto { Id = Guid.Parse(userId) };
        _userServiceMock.Setup(x => x.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDetails);

        // Act
        var result = await _controller.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        Assert.Equal(userDetails, result);
    }

    [Fact]
    public async Task GetByIdAsync_InvalidId_ThrowsNotFoundException()
    {
        // Arrange
        var invalidId = "123";
        _userServiceMock.Setup(x => x.GetAsync(invalidId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(_t["User Not Found"]));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _controller.GetByIdAsync(invalidId, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_NullId_ReturnsFalse()
    {
        // Arrange
        string nullId = null;
        _userServiceMock.Setup(x => x.GetAsync(nullId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDetailsDto)null);

        // Act
        var result = await _controller.GetByIdAsync(nullId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = "d311-fcd5-4878-be4b-a4490574b08d";
        _userServiceMock.Setup(x => x.GetAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(_t["User Not Found"]));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _controller.GetByIdAsync(nonExistentId, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ValidIdUTC5_ReturnsUserDetails()
    {
        // Arrange
        var userId = "21683b36-3526-4260-bea8-6c6a8e6b63d5";
        var userDetails = new UserDetailsDto { Id = Guid.Parse(userId) };
        _userServiceMock.Setup(x => x.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDetails);

        // Act
        var result = await _controller.GetByIdAsync(userId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<UserDetailsDto>(result);
        Assert.Equal(userId, result.Id.ToString());
    }

    // Test for the ConfrimEmailAddressForUser

    [Fact]
    public async Task ConfirmEmailAsync_ValidUserIdAndCode_ReturnsTrue()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Account Confirmed for E-Mail test@example.com. You can now use the /api/tokens endpoint to generate JWT.");

        // Act
        var result = await _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None);

        // Assert
        Assert.Contains("Account Confirmed", result);
    }

    [Fact]
    public async Task ConfirmEmailAsync_InvalidUserId_ReturnsFalse()
    {
        // Arrange
        var userId = "123";
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidUserIdInvalidCode_ReturnsFalse()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        var code = "123";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_InvalidUserIdAndCode_ReturnsFalse()
    {
        // Arrange
        var userId = "123";
        var code = "123";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_NullUserId_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_NullUserIdAndValidCode_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_NullUserIdAndInvalidCode_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        var code = "123";
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidUserIdAndNullCode_ReturnsFalse()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        string code = null;
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmailAsync_InvalidUserIdAndNullCode_ReturnsFalse()
    {
        // Arrange
        var userId = "123";
        string code = null;
        var tenant = "root";
        _userServiceMock.Setup(x => x.ConfirmEmailAsync(userId, code, tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InternalServerException("An error occurred while confirming E-Mail."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmEmailAsync(tenant, userId, code, CancellationToken.None));
    }

    // Test for the ConfrimPhoneNumberForUser

    [Fact]
    public async Task UTC01_ConfirmPhoneNumberAsync_ValidUserIdAndCode_ReturnsTrue()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ReturnsAsync("success");

        // Act
        var result = await _controller.ConfirmPhoneNumberAsync(code);

        // Assert
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task UTC02_ConfirmPhoneNumberAsync_ValidUserIdInvalidCode_ReturnsFalse()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        var code = "123";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC03_ConfirmPhoneNumberAsync_ValidUserIdNullCode_ReturnsFalse()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        string code = null;
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC04_ConfirmPhoneNumberAsync_InvalidUserIdValidCode_ReturnsFalse()
    {
        // Arrange
        var userId = "6f0fdd25-6888-4ed2-b5c8-baf9019797cc";
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC05_ConfirmPhoneNumberAsync_InvalidUserIdInvalidCode_ReturnsFalse()
    {
        // Arrange
        var userId = "6f0fdd25-6888-4ed2-b5c8-baf9019797cc";
        var code = "123";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC06_ConfirmPhoneNumberAsync_InvalidUserIdNullCode_ReturnsFalse()
    {
        // Arrange
        var userId = "6f0fdd25-6888-4ed2-b5c8-baf9019797cc";
        string code = null;
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC07_ConfirmPhoneNumberAsync_NullUserIdValidCode_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        var code = "32bc02d0-3641-4c82-9e55-977ac12caab8";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC08_ConfirmPhoneNumberAsync_NullUserIdInvalidCode_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        var code = "123";
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    [Fact]
    public async Task UTC09_ConfirmPhoneNumberAsync_NullUserIdNullCode_ReturnsFalse()
    {
        // Arrange
        string userId = null;
        string code = null;
        SetupUserContext(userId, true);
        _userServiceMock.Setup(x => x.ConfirmPhoneNumberAsync(userId, code))
            .ThrowsAsync(new InternalServerException("Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ConfirmPhoneNumberAsync(code));
    }

    // ResendPhoneNumberConfirmationCode

    [Fact]
    public async Task UTC01_ResendPhoneNumberCodeConfirmAsync_Success()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        SetupUserContext(userId, true);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendPhoneCodeRequest>(), default))
        .ReturnsAsync("Send code successfully!");

        // Act
        var result = await _controller.ResendPhoneNumberCodeConfirmAsync();

        // Assert
        Assert.Equal("Send code successfully!", result);
    }

    [Fact]
    public async Task UTC02_ResendPhoneNumberCodeConfirmAsync_UserNotAuthenticated()
    {
        // Arrange
        string userId = "6f0fdd25-6888-4ed2-b5c8-baf9019797cc";
        SetupUserContext(userId, false);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendPhoneCodeRequest>(), default))
        .ThrowsAsync(new UnauthorizedAccessException(_t["User is not authenticated."]));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ResendPhoneNumberCodeConfirmAsync());
    }

    [Fact]
    public async Task UTC03_ResendPhoneNumberCodeConfirmAsync_UserAlreadyVerified()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        SetupUserContext(userId, true);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendPhoneCodeRequest>(), default))
            .ThrowsAsync(new InternalServerException(_t["An error occurred while resending Mobile Phone confirmation code."]));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ResendPhoneNumberCodeConfirmAsync());
    }

    [Fact]
    public async Task UTC04_ResendPhoneNumberCodeConfirmAsync_ServerError()
    {
        // Arrange
        SetupUserContext("5b15f43e-41a9-4ce7-a86f-dd646c22079e", false);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendPhoneCodeRequest>(), default))
            .ThrowsAsync(new InternalServerException("An error occurred while resending Mobile Phone confirmation code."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ResendPhoneNumberCodeConfirmAsync());
    }

    // ResendEmailAddressConfirmationCode
    [Fact]
    public async Task UTC01_ResendEmailConfirmAsync_Success()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        SetupUserContext(userId, true);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendEmailConfirmRequest>(), default))
        .ReturnsAsync("Send code successfully!");

        // Act
        var result = await _controller.ResendEmailConfirmAsync();

        // Assert
        Assert.Equal("Send code successfully!", result);
    }

    [Fact]
    public async Task UTC02_ResendEmailConfirmAsync_UserNotAuthenticated()
    {
        // Arrange
        string userId = "6f0fdd25-6888-4ed2-b5c8-baf9019797cc";
        SetupUserContext(userId, false);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendEmailConfirmRequest>(), default))
        .ThrowsAsync(new UnauthorizedAccessException(_t["User is not authenticated."]));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _controller.ResendEmailConfirmAsync());
    }

    [Fact]
    public async Task UTC03_ResendEmailConfirmAsync_UserAlreadyVerified()
    {
        // Arrange
        var userId = "5b15f43e-41a9-4ce7-a86f-dd646c22079e";
        SetupUserContext(userId, true);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendEmailConfirmRequest>(), default))
            .ThrowsAsync(new InternalServerException(_t["An error occurred while resending Mobile Phone confirmation code."]));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ResendEmailConfirmAsync());
    }

    [Fact]
    public async Task UTC04_ResendEmailConfirmAsync_ServerError()
    {
        // Arrange
        SetupUserContext("5b15f43e-41a9-4ce7-a86f-dd646c22079e", false);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ResendEmailConfirmRequest>(), default))
            .ThrowsAsync(new InternalServerException("An error occurred while resending Mobile Phone confirmation code."));

        // Act & Assert
        await Assert.ThrowsAsync<InternalServerException>(() =>
            _controller.ResendEmailConfirmAsync());
    }

    // selfRegisterAccountForUser
    [Fact]
    public async Task SelfRegisterAsync_ValidRequest_ReturnsExpectedResult()
    {
        // Arrange
        SetRequestContext();

        var request = new CreateUserRequest
        {
            Email = "nguyenvancaoky@hotmail.com",
            UserName = "nguyenvancaoky",
            Password = "123456!!!@",
            FirstName = "Nguyễn Văn Cao Kỳ",
            PhoneNumber = "965476312"
        };

        var expectedOrigin = "https://testhost";
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), expectedOrigin))
                        .ReturnsAsync("User testuser Registered.");

        // Act
        var result = await _controller.SelfRegisterAsync(request);

        // Assert
        Assert.Equal("User testuser Registered.", result);
        _userServiceMock.Verify(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), expectedOrigin), Times.Once);
    }

    [Fact]
    public async Task SelfRegisterAsync_EmptyPhoneNumber_ReturnsBadRequest()
    {
        // Arrange
        SetRequestContext();

        var request = new CreateUserRequest
        {
            Email = "nguyenvancaoky@hotmail.com",
            UserName = "nguyenvancaoky",
            Password = "123456!!!@",
            FirstName = "Nguyễn Văn Cao Kỳ",
            PhoneNumber = ""
        };
        var expectedOrigin = "https://testhost";
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), expectedOrigin))
            .ThrowsAsync(new BadRequestException("Phone number is required."));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Phone number is required.", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_ExistingUsername_ReturnsBadRequest()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "12userIsExits",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Username already exists."));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Username already exists.", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "54emailIsExits",
            UserName = "testuser",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Invalid email format."));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Invalid email format.", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_ExistingEmail_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "12@48.3",
            UserName = "testuser",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };

        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Email already exists."));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Email already exists.", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_NameWithSpecialCharacters_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "password",
            FirstName = "Nguyễn Văn Cao Kỳ",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Name contains invalid characters"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Name contains invalid characters", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_UsernameWithSpecialCharacters_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "nguyenvancaoky!",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };

        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Username contains invalid characters"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Username contains invalid characters", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_InvalidPassword_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "123456!!!",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Password does not meet complexity requirements"));

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Password does not meet complexity requirements", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_InvalidPhoneNumber_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "abc123"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Invalid phone number format"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Invalid phone number format", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_EmailWithSpecialCharacters_ReturnsFalse()
    {
        // Arrange
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "nguyenvancaoky@hotmail.com",
            UserName = "testuser",
            Password = "password",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Email contains invalid characters"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        // Assert
        Assert.Equal("Email contains invalid characters", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_ValidDataAlternate_ReturnsTrue()
    {
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "alternate@example.com",
            UserName = "alternateuser",
            Password = "ValidPass123!",
            FirstName = "Alternate",
            PhoneNumber = "9876543210"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ReturnsAsync("User registered successfully");

        var result = await _controller.SelfRegisterAsync(request);

        // Assert
        Assert.Equal("User registered successfully", result);
        _userServiceMock.Verify(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SelfRegisterAsync_ExistingUsernameAlternate_ReturnsFalse()
    {
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "new@example.com",
            UserName = "12userIsExits",
            Password = "password",
            FirstName = "New",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Username already exists"));

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        Assert.Equal("Username already exists", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_ExistingEmailAlternate_ReturnsFalse()
    {
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "12@48.3",
            UserName = "newuser",
            Password = "password",
            FirstName = "New",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Email already exists"));

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        Assert.Equal("Email already exists", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_InvalidPasswordAlternate_ReturnsFalse()
    {
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "123@#$$$",
            FirstName = "Test",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Password does not meet complexity requirements"));

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        Assert.Equal("Password does not meet complexity requirements", result.Message);
    }

    [Fact]
    public async Task SelfRegisterAsync_NameWithWhitespace_ReturnsFalse()
    {
        SetRequestContext();
        var request = new CreateUserRequest
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "password",
            FirstName = "Abc ",
            PhoneNumber = "1234567890"
        };
        _userServiceMock.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<string>()))
            .ThrowsAsync(new BadRequestException("Name contains leading or trailing whitespace"));

        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.SelfRegisterAsync(request));

        Assert.Equal("Name contains leading or trailing whitespace", result.Message);
    }

    // resetPassword
    [Fact]
    public async Task UC1_ResetPasswordAsync_EmptyValues_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = null,
            Password = null,
            Token = null,
            captchaToken = null
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid request"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid request", result.Message);
    }

    [Fact]
    public async Task UC2_ResetPasswordAsync_NullEmail_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = null,
            Password = "123Pa$$word!",
            Token = "invalid_token",
            captchaToken = "invalid_token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid format"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid format", result.Message);
    }

    [Fact]
    public async Task UC3_ResetPasswordAsync_NullEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = null,
            Password = "456Pa$$word!",
            Token = "token",
            captchaToken = "token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Incorrect infor"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Contains("Incorrect", result.Message);
    }

    [Fact]
    public async Task UC4_ResetPasswordAsync_NullPassword_NullCaptchaToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com",
            Password = null,
            Token = "token",
            captchaToken = null
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Incorrect infor"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Contains("Incorrect", result.Message);
    }

    [Fact]
    public async Task UC5_ResetPasswordAsync_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com",
            Password = "123Pa$$word",
            Token = "invalidtoken",
            captchaToken = "invalidtoken"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid token"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid token", result.Message);
    }

    [Fact]
    public async Task UC6_ResetPasswordAsync_InvalidPassword_NullToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com",
            Password = "123Pa$$word",
            Token = null,
            captchaToken = "valid"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Somethings went wrong"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Somethings went wrong", result.Message);
    }

    [Fact]
    public async Task UC7_ResetPasswordAsync_WhitespaceEmail_NullPassword_NullToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com ",
            Password = null,
            Token = null,
            captchaToken = "null"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Bad Request"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Bad Request", result.Message);
    }

    [Fact]
    public async Task UC8_ResetPasswordAsync_WhitespaceEmail_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com ",
            Password = "123Pa$$word",
            Token = "invalidtoken",
            captchaToken = "invalidtoken"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Bad Request"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Bad Request", result.Message);
    }

    [Fact]
    public async Task UC9_ResetPasswordAsync_WhitespaceEmail_InvalidPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com ",
            Password = "456Pa$$word",
            Token = "token",
            captchaToken = "token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid password"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid password", result.Message);
    }

    [Fact]
    public async Task UC10_ResetPasswordAsync_WrongFormatEmail_IncorrectPassword_NullToken_InvalidCaptchaToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root",
            Password = "456Pa$$word",
            Token = null,
            captchaToken = "invalid"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid email format"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid email format", result.Message);
    }

    [Fact]
    public async Task UC11_ResetPasswordAsync_WrongFormatEmail_InValidToken_NullCaptchaToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root",
            Password = "123Pa$$word",
            Token = "invalidtoken",
            captchaToken = null
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid email format"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid email format", result.Message);
    }

    [Fact]
    public async Task UC12_ResetPasswordAsync_WrongFormatEmail_NullPassword_InValidCaptchaToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root",
            Password = null,
            Token = "token",
            captchaToken = "invalidtoken"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid email format"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid email format", result.Message);
    }

    [Fact]
    public async Task UC13_ResetPasswordAsync_WrongPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com",
            Password = "456Pa$$word",
            Token = "token",
            captchaToken = "token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid password"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid password", result.Message);
    }

    [Fact]
    public async Task UC14_ResetPasswordAsync_ValidRequest_ReturnsExpectedResult()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com",
            Password = "123Pa$$word",
            Token = "token",
            captchaToken = "token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ReturnsAsync("Password reset successfully");

        // Act
        var result = await _controller.ResetPasswordAsync(request);

        // Assert
        Assert.Equal("Password reset successfully", result);
        _userServiceMock.Verify(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()), Times.Once);
    }

    [Fact]
    public async Task UC15_ResetPasswordAsync_WhitespaceEmail_WrongPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "admin@root.com ",
            Password = "456Pa$$ord!",
            Token = "token",
            captchaToken = "token"
        };

        _userServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ThrowsAsync(new BadRequestException("Invalid password"));

        // Act
        var result = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ResetPasswordAsync(request));

        // Assert
        Assert.Equal("Invalid password", result.Message);
    }
}
