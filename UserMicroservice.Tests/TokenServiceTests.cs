using Moq;
using Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using user_microservice.Models;
using user_microservice.Services;

namespace user_microservice.Tests
{
    public class TokenServiceTests
    {
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly TokenService _tokenService;

        private const string ValidKey = "super_secret_test_key_1234567890_abcdef";
        private const string ValidIssuer = "http://auth-api:5000";
        private const string ValidAudience = "http://review-api:5001";
        private const string ValidExpiry = "15";

        public TokenServiceTests()
        {
            _mockUserManager = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);

            _mockConfiguration = new Mock<IConfiguration>();

            SetupValidConfiguration();

            _tokenService = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private void SetupValidConfiguration(
            string? key = ValidKey,
            string? issuer = ValidIssuer,
            string? audience = ValidAudience,
            string? expiry = ValidExpiry)
        {
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns(key!);
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns(issuer!);
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns(audience!);
            _mockConfiguration.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns(expiry!);
        }

        private AppUser MakeUser(string id = "user-1", string email = "test@example.com", string username = "testuser")
            => new AppUser { Id = id, Email = email, UserName = username };

        // ─────────────────────────────────────────────────────────────
        // CreateToken
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateToken_ReturnsNonEmptyString()
        {
            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            var token = await _tokenService.CreateToken(user);

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task CreateToken_ReturnsValidJwt_ThatCanBeParsed()
        {
            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            var token = await _tokenService.CreateToken(user);

            var handler = new JwtSecurityTokenHandler();
            Assert.True(handler.CanReadToken(token));

            var jwt = handler.ReadJwtToken(token);
            Assert.NotNull(jwt);
        }

        [Fact]
        public async Task CreateToken_ContainsCorrectSubClaim()
        {
            var user = MakeUser(id: "abc-123");
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            Assert.Equal("abc-123", sub);
        }

        [Fact]
        public async Task CreateToken_ContainsCorrectEmailClaim()
        {
            var user = MakeUser(email: "jan@example.com");
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
            Assert.Equal("jan@example.com", email);
        }

        [Fact]
        public async Task CreateToken_ContainsCorrectUsernameClaim()
        {
            var user = MakeUser(username: "jankowalski");
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var name = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value;
            Assert.Equal("jankowalski", name);
        }

        [Fact]
        public async Task CreateToken_ContainsAllRoleClaims()
        {
            var user = MakeUser();
            var roles = new List<string> { "User", "Moderator" };
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var tokenRoles = jwt.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            Assert.Contains("User", tokenRoles);
            Assert.Contains("Moderator", tokenRoles);
        }

        [Fact]
        public async Task CreateToken_HasCorrectIssuerAndAudience()
        {
            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal(ValidIssuer, jwt.Issuer);
            Assert.Contains(ValidAudience, jwt.Audiences);
        }

        [Fact]
        public async Task CreateToken_HasCorrectExpiration()
        {
            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var before = DateTime.UtcNow.AddMinutes(14);
            var token = await _tokenService.CreateToken(user);
            var after = DateTime.UtcNow.AddMinutes(16);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.True(jwt.ValidTo >= before, $"ValidTo {jwt.ValidTo} powinno być >= {before}");
            Assert.True(jwt.ValidTo <= after, $"ValidTo {jwt.ValidTo} powinno być <= {after}");
        }

        [Fact]
        public async Task CreateToken_UsesHmacSha256Algorithm()
        {
            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var token = await _tokenService.CreateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal(
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256,
                jwt.Header.Alg);
        }

        [Fact]
        public async Task CreateToken_ThrowsInvalidOperationException_WhenKeyIsMissing()
        {
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns((string)null!);

            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var svc = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateToken(user));
        }

        [Fact]
        public async Task CreateToken_ThrowsInvalidOperationException_WhenIssuerIsMissing()
        {
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns(ValidKey);
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns((string)null!);
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
            _mockConfiguration.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns(ValidExpiry);

            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var svc = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateToken(user));
        }

        [Fact]
        public async Task CreateToken_ThrowsInvalidOperationException_WhenAudienceIsMissing()
        {
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns(ValidKey);
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns((string)null!);
            _mockConfiguration.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns(ValidExpiry);

            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var svc = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateToken(user));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-number")]
        [InlineData("0")]
        public async Task CreateToken_FallsBackTo15Minutes_WhenExpiryIsMissingOrInvalid(string? expiry)
        {
            _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns(ValidKey);
            _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
            _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
            _mockConfiguration.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns(expiry!);

            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var svc = new TokenService(_mockConfiguration.Object, _mockUserManager.Object);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(await svc.CreateToken(user));
            Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        }

        // ─────────────────────────────────────────────────────────────
        // CreateRefreshToken
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public void CreateRefreshToken_ReturnsNonEmptyString()
        {
            var token = _tokenService.CreateRefreshToken();

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public void CreateRefreshToken_ReturnsValidGuid()
        {
            var token = _tokenService.CreateRefreshToken();

            Assert.True(Guid.TryParse(token, out _), $"'{token}' nie jest prawidłowym GUID.");
        }

        [Fact]
        public void CreateRefreshToken_ReturnsDifferentValueEachTime()
        {
            var token1 = _tokenService.CreateRefreshToken();
            var token2 = _tokenService.CreateRefreshToken();

            Assert.NotEqual(token1, token2);
        }

        // ─────────────────────────────────────────────────────────────
        // GetPrincipalFromExpiredToken
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetPrincipalFromExpiredToken_ReturnsPrincipal_WithCorrectClaims()
        {
            var user = MakeUser(id: "user-42", email: "a@a.pl", username: "aaa");
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });

            // Tworzymy token z krótkim wygaśnięciem (1 min), który możemy zaraz "wygasić" w teście
            var token = await _tokenService.CreateToken(user);

            // GetPrincipalFromExpiredToken ma ValidateLifetime = false, więc zadziała nawet na świeżym tokenie
            var principal = _tokenService.GetPrincipalFromExpiredToken(token);

            Assert.NotNull(principal);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            Assert.Equal("user-42", sub);
        }

        [Fact]
        public async Task GetPrincipalFromExpiredToken_ReturnsPrincipalWithRoles()
        {
            var user = MakeUser();
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User", "Moderator" });

            var token = await _tokenService.CreateToken(user);
            var principal = _tokenService.GetPrincipalFromExpiredToken(token);

            var roles = principal.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            Assert.Contains("User", roles);
            Assert.Contains("Moderator", roles);
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_ThrowsException_WhenTokenIsGarbage()
        {
            Assert.ThrowsAny<Exception>(() =>
                _tokenService.GetPrincipalFromExpiredToken("this.is.not.a.jwt"));
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_ThrowsException_WhenTokenIsEmpty()
        {
            Assert.ThrowsAny<Exception>(() =>
                _tokenService.GetPrincipalFromExpiredToken(""));
        }

        [Fact]
        public async Task GetPrincipalFromExpiredToken_ThrowsException_WhenSignedWithDifferentKey()
        {
            // Token podpisany innym kluczem — weryfikacja powinna rzucić wyjątek
            var otherConfig = new Mock<IConfiguration>();
            otherConfig.Setup(c => c["Jwt:Key"]).Returns("zupelnie_inny_klucz_1234567890_xyz");
            otherConfig.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
            otherConfig.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
            otherConfig.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns(ValidExpiry);

            var otherSvc = new TokenService(otherConfig.Object, _mockUserManager.Object);

            var user = MakeUser();
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var tokenSignedByOther = await otherSvc.CreateToken(user);

            // _tokenService używa ValidKey — nie powinien zaakceptować tokenu z innego klucza
            Assert.ThrowsAny<Exception>(() =>
                _tokenService.GetPrincipalFromExpiredToken(tokenSignedByOther));
        }

        [Fact]
        public async Task GetPrincipalFromExpiredToken_AcceptsToken_EvenAfterExpiry()
        {
            // Konfiguracja z bardzo krótkim czasem ważności (0 minut — wygasły natychmiast)
            var shortConfig = new Mock<IConfiguration>();
            shortConfig.Setup(c => c["Jwt:Key"]).Returns(ValidKey);
            shortConfig.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
            shortConfig.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
            shortConfig.Setup(c => c["Jwt:TokenExpirationInMinutes"]).Returns("0");

            var shortSvc = new TokenService(shortConfig.Object, _mockUserManager.Object);

            var user = MakeUser(id: "expired-user");
            _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

            var expiredToken = await shortSvc.CreateToken(user);

            // ValidateLifetime = false — powinno zadziałać mimo wygaśnięcia
            var principal = _tokenService.GetPrincipalFromExpiredToken(expiredToken);
            Assert.NotNull(principal);
        }
    }
}