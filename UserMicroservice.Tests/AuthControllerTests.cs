using Moq;
using Xunit;
using user_microservice.Controllers;
using user_microservice.Models;
using user_microservice.Dtos;
using user_microservice.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace user_microservice.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockUserManager = new Mock<UserManager<AppUser>>(Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            _mockTokenService = new Mock<ITokenService>();

            // Mockowanie IConfiguration
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("your_secret_key");
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns("issuer");
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns("audience");

            _controller = new AuthController(_mockConfiguration.Object, _mockUserManager.Object, _mockTokenService.Object);
        }

        [Fact]
        public async Task GetUserById_ReturnsNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            _mockUserManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((AppUser)null);

            // Act
            var result = await _controller.GetUserById("1");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("User not found.", notFoundResult.Value);
        }
    }
}