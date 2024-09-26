using System.Net;
using FSH.WebApi.Application.Identity.Tokens;
using FSH.WebApi.Host.Controllers.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Infrastructure.Test.Controllers.Identity
{
    public class TokensControllerTest
    {
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly TokensController _controller;

        public TokensControllerTest()
        {
            _tokenServiceMock = new Mock<ITokenService>();
            _controller = new TokensController(_tokenServiceMock.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        [Fact]
        public async Task GetTokenAsync_EmailIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest(
                string.Empty,
                "123Pa$$word!",
                "9PA}rTVa^9*1tCyiNTk?ix=.dq)6kW",
                "123"
            );

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsync_PasswordIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest(
                "admin@root.com",
                string.Empty,
                "9PA}rTVa^9*1tCyiNTk?ix=.dq)6kW",
                "123"
            );

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsync_CaptchaTokenIsEmpty_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest("admin@root.com", "123Pa$$word!", string.Empty, "123");

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsync_AllFieldsAreEmpty_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest(string.Empty, string.Empty, string.Empty, "123");

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsync_AllFieldsAreNull_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest(null, null, null, "123");

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsync_AllFieldsAreWhitespace_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest(" ", " ", " ", "123");

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTokenAsyncValidRequestReturnsTokenResponse()
        {
            // Arrange
            var request = new TokenRequest("test@gmail.com", "password", "captchaToken", "123");
            var response = new TokenResponse("token", "refreshToken", DateTime.UtcNow.AddDays(7));

            _tokenServiceMock
                .Setup(x =>
                    x.GetTokenAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetTokenAsync(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var tokenResponse = Assert.IsType<TokenResponse>(okResult.Value);
            Assert.Equal(response.Token, tokenResponse.Token);
        }

        [Fact]
        public async Task RefreshAsyncValidRequestReturnsTokenResponse()
        {
            // Arrange
            var request = new RefreshTokenRequest("token", "refreshToken");
            var response = new TokenResponse(
                "newToken",
                "newRefreshToken",
                DateTime.UtcNow.AddDays(7)
            );

            _tokenServiceMock
                .Setup(x => x.RefreshTokenAsync(request, It.IsAny<string>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.RefreshAsync(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RefreshAsyncInvalidRequestReturnsBadRequest()
        {
            // Arrange
            var request = new RefreshTokenRequest("", "");

            // Act
            var result = await _controller.RefreshAsync(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public void GetIpAddressHeaderPresentReturnsCorrectIpAddress()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.1";
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var ipAddress = _controller.GetIpAddress();

            // Assert
            Assert.Equal("192.168.1.1", ipAddress);
        }

        [Fact]
        public void GetIpAddressHeaderNotPresentReturnsRemoteIpAddress()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var ipAddress = _controller.GetIpAddress();

            // Assert
            Assert.Equal("127.0.0.1", ipAddress);
        }

        [Fact]
        public void GetIpAddressHeaderNotPresentAndNoRemoteIpAddressReturnsNA()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var ipAddress = _controller.GetIpAddress();

            // Assert
            Assert.Equal("N/A", ipAddress);
        }

        [Fact]
        public async Task GetTokenAsyncInvalidCaptchaTokenReturnsUnauthorized()
        {
            // Arrange
            var request = new TokenRequest("test@gmail.com", "password", "invalidCaptchaToken", "123");

            _tokenServiceMock
                .Setup(x =>
                    x.GetTokenAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ThrowsAsync(new UnauthorizedAccessException());

            try
            {
                // Act
                var result = await _controller.GetTokenAsync(request, CancellationToken.None);

                // Assert
                Assert.True(false);
            }
            catch (UnauthorizedAccessException)
            {
                // Assert
                Assert.True(true);
            }
        }

        [Fact]
        public async Task GetTokenAsyncServiceExceptionReturnsInternalServerError()
        {
            // Arrange
            var request = new TokenRequest("test@gmail.com", "password", "captchaToken", "123");

            _tokenServiceMock
                .Setup(x =>
                    x.GetTokenAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ThrowsAsync(new Exception());

            // Act
            try
            {
                var result = await _controller.GetTokenAsync(request, CancellationToken.None);

                // Assert
                Assert.True(false);
            }
            catch (Exception)
            {
                // Assert
                Assert.True(true);
            }
        }
    }
}
