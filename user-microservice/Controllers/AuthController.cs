using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using user_microservice.Data;
using user_microservice.Dtos;
using user_microservice.Interfaces;
using user_microservice.Models;
using user_microservice.Validators;
using System.Linq;

namespace user_microservice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly RegisterValidator _registerValidator;
        private readonly int _refreshTokenExpirationInDays;

        public AuthController(
            IConfiguration configuration,
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            RegisterValidator registerValidator)
        {
            _configuration = configuration;
            _userManager = userManager;
            _tokenService = tokenService;
            _registerValidator = registerValidator;
            _refreshTokenExpirationInDays = _configuration.GetValue<int>("RefreshToken:ExpirationInDays", 7);
        }

        private CookieOptions GetRefreshTokenCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(_refreshTokenExpirationInDays),
                SameSite = SameSiteMode.Lax,
                Secure = false
            };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AppUserReadDto>> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("User not found.");

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new AppUserReadDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email,
                Roles = roles.OrderBy(r => r).ToList()
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<AppUserReadDto>> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (userId == null)
                return Unauthorized("Invalid token.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            var meRoles = await _userManager.GetRolesAsync(user);
            return Ok(new AppUserReadDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email,
                Roles = meRoles.OrderBy(r => r).ToList()
            });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var rawToken = dto.Token;
            if (string.IsNullOrEmpty(rawToken))
                return BadRequest("Token is required.");

            ClaimsPrincipal principal;
            try
            {
                principal = _tokenService.GetPrincipalFromExpiredToken(rawToken);
            }
            catch
            {
                return BadRequest("Nieprawidłowy lub wygasły token.");
            }

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var rToken = Request.Cookies["refreshToken"];
            if (rToken == null || user.RefreshToken != rToken || user.RefreshTokenExpiryTime < DateTime.UtcNow)
                return Unauthorized();

            var newJwtToken = await _tokenService.CreateToken(user);
            string newRefreshToken = _tokenService.CreateRefreshToken();
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_refreshTokenExpirationInDays);
            await _userManager.UpdateAsync(user);

            Response.Cookies.Append("refreshToken", newRefreshToken, GetRefreshTokenCookieOptions());

            var refreshRoles = await _userManager.GetRolesAsync(user);
            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Email = user.Email!,
                Token = newJwtToken,
                Roles = refreshRoles.OrderBy(r => r).ToList()
            });
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterInputDto model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return BadRequest("You are already logged in.");

            // ── Walidacja biznesowa ────────────────────────────────
            var validation = _registerValidator.Validate(model);
            if (!validation.IsValid)
                return BadRequest(new { errors = validation.Errors });
            // ──────────────────────────────────────────────────────

            var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingUserByEmail != null)
                return BadRequest("Email already exists.");

            var existingUserByUsername = await _userManager.FindByNameAsync(model.Username);
            if (existingUserByUsername != null)
                return BadRequest("Username already exists.");

            var user = new AppUser { UserName = model.Username, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, UserRoles.User);
            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Email = user.Email!,
                Roles = new List<string> { UserRoles.User }
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginInputDto model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return BadRequest("You are already logged in.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized("Invalid email or password.");

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!isPasswordValid)
                return Unauthorized("Invalid email or password.");

            try
            {
                var token = await _tokenService.CreateToken(user);
                string refreshToken = _tokenService.CreateRefreshToken();
                int refreshTokenExpirationInDays = _configuration.GetValue<int>("RefreshToken:ExpirationInDays", 7);

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationInDays);
                await _userManager.UpdateAsync(user);

                Response.Cookies.Append("refreshToken", refreshToken, GetRefreshTokenCookieOptions());

                var loginRoles = await _userManager.GetRolesAsync(user);
                return Ok(new AuthResponseDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    Token = token,
                    Roles = loginRoles.OrderBy(r => r).ToList()
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Wystąpił błąd serwera. Spróbuj ponownie później." });
            }
        }

        [AllowAnonymous]
        [HttpPost("users/resolve-names")]
        public async Task<ActionResult<Dictionary<string, string>>> ResolveUserNames(
            [FromBody] ResolveUserNamesRequestDto dto)
        {
            var raw = dto.UserIds;
            if (raw == null || raw.Count == 0)
                return Ok(new Dictionary<string, string>());

            var distinct = raw
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .Take(100)
                .ToList();

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in distinct)
            {
                var u = await _userManager.FindByIdAsync(id);
                if (u != null && !string.IsNullOrEmpty(u.UserName))
                    result[id] = u.UserName;
            }

            return Ok(result);
        }
    }
}