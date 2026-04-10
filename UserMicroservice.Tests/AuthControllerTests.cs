using Moq;
using Xunit;
using user_microservice.Controllers;
using user_microservice.Models;
using user_microservice.Dtos;
using user_microservice.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;

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
            _mockUserManager = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);

            _mockTokenService = new Mock<ITokenService>();
            _mockConfiguration = new Mock<IConfiguration>();

            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(x => x["Key"]).Returns("sample_jwt_key_for_testing");
            jwtSection.Setup(x => x["Issuer"]).Returns("http://auth-api:5000");
            jwtSection.Setup(x => x["Audience"]).Returns("http://review-api:5001");
            jwtSection.Setup(x => x["TokenExpirationInMinutes"]).Returns("15");
            _mockConfiguration.Setup(x => x.GetSection("Jwt")).Returns(jwtSection.Object);

            // GetValue<int> wewnętrznie używa GetSection(key).Value
            var refreshTokenSection = new Mock<IConfigurationSection>();
            refreshTokenSection.Setup(x => x.Value).Returns("7");
            _mockConfiguration
                .Setup(x => x.GetSection("RefreshToken:ExpirationInDays"))
                .Returns(refreshTokenSection.Object);

            _controller = new AuthController(
                _mockConfiguration.Object,
                _mockUserManager.Object,
                _mockTokenService.Object);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ustawia HttpContext kontrolera z podanym userId w claimsach
        /// oraz opcjonalnie flagą IsAuthenticated.
        /// </summary>
        private void SetupHttpContext(string? userId = null, bool isAuthenticated = false)
        {
            var claims = new List<Claim>();
            if (userId != null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

            var identity = new ClaimsIdentity(
                isAuthenticated ? claims : Enumerable.Empty<Claim>(),
                isAuthenticated ? "TestAuth" : null);

            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        /// <summary>
        /// Ustawia HttpContext z zamockowanym ciasteczkiem refreshToken.
        /// </summary>
        private void SetupHttpContextWithCookie(string? userId, string? refreshCookieValue)
        {
            var claims = new List<Claim>();
            if (userId != null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;

            if (refreshCookieValue != null)
            {
                httpContext.Request.Headers["Cookie"] =
                    $"refreshToken={refreshCookieValue}";
            }

            // Potrzebne, żeby Response.Cookies.Append nie rzucał wyjątku
            httpContext.Response.Body = new System.IO.MemoryStream();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/auth/{id}  –  GetUserById
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetUserById_ReturnsNotFound_WhenUserDoesNotExist()
        {
            _mockUserManager
                .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);

            var result = await _controller.GetUserById("nonexistent-id");

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal("User not found.", notFound.Value);
        }

        [Fact]
        public async Task GetUserById_ReturnsOk_WithCorrectDto_WhenUserExists()
        {
            var user = new AppUser { Id = "1", UserName = "jan", Email = "jan@example.com" };
            var roles = new List<string> { "User", "Moderator" };

            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);

            var result = await _controller.GetUserById("1");

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AppUserReadDto>(ok.Value);
            Assert.Equal("1", dto.Id);
            Assert.Equal("jan", dto.Username);
            Assert.Equal("jan@example.com", dto.Email);
            Assert.Equal(new[] { "Moderator", "User" }, dto.Roles); // posortowane
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/auth/me  –  GetMe
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMe_ReturnsUnauthorized_WhenNoUserIdInToken()
        {
            SetupHttpContext(userId: null, isAuthenticated: true);

            var result = await _controller.GetMe();

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid token.", unauthorized.Value);
        }

        [Fact]
        public async Task GetMe_ReturnsNotFound_WhenUserDoesNotExistInDb()
        {
            SetupHttpContext(userId: "ghost-id", isAuthenticated: true);

            _mockUserManager
                .Setup(m => m.FindByIdAsync("ghost-id"))
                .ReturnsAsync((AppUser)null!);

            var result = await _controller.GetMe();

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetMe_ReturnsOk_WithCorrectDto_WhenUserExists()
        {
            var user = new AppUser { Id = "42", UserName = "ania", Email = "ania@example.com" };
            SetupHttpContext(userId: "42", isAuthenticated: true);

            _mockUserManager.Setup(m => m.FindByIdAsync("42")).ReturnsAsync(user);
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Admin", "User" });

            var result = await _controller.GetMe();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AppUserReadDto>(ok.Value);
            Assert.Equal("42", dto.Id);
            Assert.Equal("ania", dto.Username);
            Assert.Equal(new[] { "Admin", "User" }, dto.Roles); // posortowane
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/register  –  Register
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenAlreadyLoggedIn()
        {
            SetupHttpContext(userId: "1", isAuthenticated: true);

            var result = await _controller.Register(new RegisterInputDto
            {
                Email = "a@a.pl",
                Username = "a",
                Password = "Passw0rd!",
                ConfirmPassword = "Passw0rd!"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("You are already logged in.", bad.Value);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenEmailAlreadyExists()
        {
            SetupHttpContext(isAuthenticated: false);

            _mockUserManager
                .Setup(m => m.FindByEmailAsync("dup@example.com"))
                .ReturnsAsync(new AppUser { Email = "dup@example.com" });

            var result = await _controller.Register(new RegisterInputDto
            {
                Email = "dup@example.com",
                Username = "newuser",
                Password = "Passw0rd!",
                ConfirmPassword = "Passw0rd!"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Email already exists.", bad.Value);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenUsernameAlreadyExists()
        {
            SetupHttpContext(isAuthenticated: false);

            _mockUserManager
                .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);
            _mockUserManager
                .Setup(m => m.FindByNameAsync("taken"))
                .ReturnsAsync(new AppUser { UserName = "taken" });

            var result = await _controller.Register(new RegisterInputDto
            {
                Email = "new@example.com",
                Username = "taken",
                Password = "Passw0rd!",
                ConfirmPassword = "Passw0rd!"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Username already exists.", bad.Value);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenCreateAsyncFails()
        {
            SetupHttpContext(isAuthenticated: false);

            _mockUserManager
                .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);
            _mockUserManager
                .Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);
            _mockUserManager
                .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Password too weak." }));

            var result = await _controller.Register(new RegisterInputDto
            {
                Email = "ok@example.com",
                Username = "okuser",
                Password = "weak",
                ConfirmPassword = "weak"
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_ReturnsOk_WithUserRole_WhenSuccessful()
        {
            SetupHttpContext(isAuthenticated: false);

            _mockUserManager
                .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);
            _mockUserManager
                .Setup(m => m.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null!);
            _mockUserManager
                .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.Register(new RegisterInputDto
            {
                Email = "fresh@example.com",
                Username = "freshuser",
                Password = "Passw0rd!",
                ConfirmPassword = "Passw0rd!"
            });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AuthResponseDto>(ok.Value);
            Assert.Contains("User", dto.Roles);
            Assert.Equal("fresh@example.com", dto.Email);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/login  –  Login
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task Login_ReturnsBadRequest_WhenAlreadyLoggedIn()
        {
            SetupHttpContext(userId: "1", isAuthenticated: true);

            var result = await _controller.Login(new LoginInputDto
            {
                Email = "a@a.pl",
                Password = "Passw0rd!"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("You are already logged in.", bad.Value);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
        {
            SetupHttpContext(isAuthenticated: false);

            _mockUserManager
                .Setup(m => m.FindByEmailAsync("no@example.com"))
                .ReturnsAsync((AppUser)null!);

            var result = await _controller.Login(new LoginInputDto
            {
                Email = "no@example.com",
                Password = "Passw0rd!"
            });

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid email or password.", unauth.Value);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
        {
            SetupHttpContext(isAuthenticated: false);

            var user = new AppUser { Id = "1", Email = "u@example.com" };
            _mockUserManager.Setup(m => m.FindByEmailAsync("u@example.com")).ReturnsAsync(user);
            _mockUserManager
                .Setup(m => m.CheckPasswordAsync(user, "wrongpass"))
                .ReturnsAsync(false);

            var result = await _controller.Login(new LoginInputDto
            {
                Email = "u@example.com",
                Password = "wrongpass"
            });

            var unauth = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            Assert.Equal("Invalid email or password.", unauth.Value);
        }

        [Fact]
        public async Task Login_ReturnsOk_WithTokenAndRoles_WhenCredentialsAreValid()
        {
            SetupHttpContextWithCookie(userId: null, refreshCookieValue: null);

            var user = new AppUser { Id = "5", Email = "valid@example.com", UserName = "valid" };

            _mockUserManager.Setup(m => m.FindByEmailAsync("valid@example.com")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.CheckPasswordAsync(user, "Passw0rd!")).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });
            _mockUserManager
                .Setup(m => m.UpdateAsync(It.IsAny<AppUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _mockTokenService.Setup(t => t.CreateToken(user)).ReturnsAsync("jwt-token");
            _mockTokenService.Setup(t => t.CreateRefreshToken()).Returns("refresh-token");

            var result = await _controller.Login(new LoginInputDto
            {
                Email = "valid@example.com",
                Password = "Passw0rd!"
            });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AuthResponseDto>(ok.Value);
            Assert.Equal("jwt-token", dto.Token);
            Assert.Equal("5", dto.Id);
            Assert.Contains("User", dto.Roles);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/refresh  –  RefreshToken
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task RefreshToken_ReturnsBadRequest_WhenTokenIsEmpty()
        {
            SetupHttpContextWithCookie(null, null);

            var result = await _controller.RefreshToken(new RefreshTokenDto { Token = "" });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Token is required.", bad.Value);
        }

        [Fact]
        public async Task RefreshToken_ReturnsBadRequest_WhenTokenServiceThrows()
        {
            SetupHttpContextWithCookie(null, null);

            _mockTokenService
                .Setup(t => t.GetPrincipalFromExpiredToken(It.IsAny<string>()))
                .Throws(new Exception("Invalid token"));

            var result = await _controller.RefreshToken(new RefreshTokenDto { Token = "bad-token" });

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Nieprawidłowy lub wygasły token.", bad.Value);
        }

        [Fact]
        public async Task RefreshToken_ReturnsUnauthorized_WhenRefreshCookieMissing()
        {
            var userId = "99";
            // Brak ciasteczka refreshToken
            SetupHttpContextWithCookie(userId, refreshCookieValue: null);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth"));

            _mockTokenService
                .Setup(t => t.GetPrincipalFromExpiredToken("valid-expired-token"))
                .Returns(principal);

            var user = new AppUser
            {
                Id = userId,
                RefreshToken = "stored-refresh",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
            };
            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var result = await _controller.RefreshToken(
                new RefreshTokenDto { Token = "valid-expired-token" });

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task RefreshToken_ReturnsUnauthorized_WhenRefreshTokenExpired()
        {
            var userId = "99";
            SetupHttpContextWithCookie(userId, "stored-refresh");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth"));

            _mockTokenService
                .Setup(t => t.GetPrincipalFromExpiredToken("valid-expired-token"))
                .Returns(principal);

            var user = new AppUser
            {
                Id = userId,
                RefreshToken = "stored-refresh",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1) // wygasł
            };
            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

            var result = await _controller.RefreshToken(
                new RefreshTokenDto { Token = "valid-expired-token" });

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task RefreshToken_ReturnsOk_WithNewTokens_WhenEverythingIsValid()
        {
            var userId = "99";
            SetupHttpContextWithCookie(userId, "stored-refresh");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth"));

            _mockTokenService
                .Setup(t => t.GetPrincipalFromExpiredToken("valid-expired-token"))
                .Returns(principal);

            var user = new AppUser
            {
                Id = userId,
                Email = "u@example.com",
                RefreshToken = "stored-refresh",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(3)
            };
            _mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
            _mockUserManager
                .Setup(m => m.UpdateAsync(It.IsAny<AppUser>()))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            _mockTokenService.Setup(t => t.CreateToken(user)).ReturnsAsync("new-jwt");
            _mockTokenService.Setup(t => t.CreateRefreshToken()).Returns("new-refresh");

            var result = await _controller.RefreshToken(
                new RefreshTokenDto { Token = "valid-expired-token" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AuthResponseDto>(ok.Value);
            Assert.Equal("new-jwt", dto.Token);
            Assert.Equal(userId, dto.Id);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/users/resolve-names  –  ResolveUserNames
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task ResolveUserNames_ReturnsEmptyDict_WhenListIsNull()
        {
            var result = await _controller.ResolveUserNames(
                new ResolveUserNamesRequestDto { UserIds = null });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);
            Assert.Empty(dict);
        }

        [Fact]
        public async Task ResolveUserNames_ReturnsEmptyDict_WhenListIsEmpty()
        {
            var result = await _controller.ResolveUserNames(
                new ResolveUserNamesRequestDto { UserIds = new List<string>() });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);
            Assert.Empty(dict);
        }

        [Fact]
        public async Task ResolveUserNames_SkipsUnknownIds_AndReturnsKnown()
        {
            var userA = new AppUser { Id = "id-A", UserName = "Alice" };

            _mockUserManager.Setup(m => m.FindByIdAsync("id-A")).ReturnsAsync(userA);
            _mockUserManager.Setup(m => m.FindByIdAsync("id-X")).ReturnsAsync((AppUser)null!);

            var result = await _controller.ResolveUserNames(
                new ResolveUserNamesRequestDto
                {
                    UserIds = new List<string> { "id-A", "id-X" }
                });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);

            Assert.True(dict.ContainsKey("id-A"));
            Assert.Equal("Alice", dict["id-A"]);
            Assert.False(dict.ContainsKey("id-X"));
        }

        [Fact]
        public async Task ResolveUserNames_DeduplicatesIds()
        {
            var userA = new AppUser { Id = "id-A", UserName = "Alice" };

            _mockUserManager.Setup(m => m.FindByIdAsync("id-A")).ReturnsAsync(userA);

            var result = await _controller.ResolveUserNames(
                new ResolveUserNamesRequestDto
                {
                    UserIds = new List<string> { "id-A", "id-A", "id-A" }
                });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);

            // Tylko jeden wpis mimo trzech zduplikowanych ID
            Assert.Single(dict);
            Assert.Equal("Alice", dict["id-A"]);

            // FindByIdAsync powinno być wywołane tylko raz
            _mockUserManager.Verify(m => m.FindByIdAsync("id-A"), Times.Once);
        }

        [Fact]
        public async Task ResolveUserNames_IgnoresBlankIds()
        {
            var result = await _controller.ResolveUserNames(
                new ResolveUserNamesRequestDto
                {
                    UserIds = new List<string> { "", "   ", null! }
                });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dict = Assert.IsType<Dictionary<string, string>>(ok.Value);
            Assert.Empty(dict);

            _mockUserManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }
    }
}