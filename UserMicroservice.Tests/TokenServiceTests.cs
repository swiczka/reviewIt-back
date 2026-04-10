using Moq;
using Xunit;
using user_microservice.Services;
using user_microservice.Models;
using user_microservice.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace user_microservice.Tests
{
    public class TokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly TokenService _tokenService;

        public TokenServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockUserManager = new Mock<UserManager<AppUser>>(Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            _tokenService = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);
        }

        [Fact]
        public async Task CreateToken_ReturnsToken()
        {
            // Arrange
            var user = new AppUser { Id = "1", UserName = "user1", Email = "user1@example.com" };
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("your_secret_key");
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns("issuer");
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns("audience");

            // Act
            var token = await _tokenService.CreateToken(user);

            // Assert
            Assert.NotNull(token);
            Assert.IsType<string>(token);
        }
    }
}