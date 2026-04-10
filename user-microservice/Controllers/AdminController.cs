using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using user_microservice.Data;
using user_microservice.Dtos;
using user_microservice.Models;

namespace user_microservice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = UserRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private static readonly HashSet<string> AssignableRoles = new(StringComparer.Ordinal)
        {
            UserRoles.User,
            UserRoles.Moderator,
            UserRoles.Admin
        };

        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetUsers()
        {
            var users = _userManager.Users.ToList();  // Pobieramy użytkowników (wciąż może być null lub pusta lista)
            if (users == null || users.Count == 0)
            {
                return Ok(new List<UserListItemDto>());
            }

            var list = new List<UserListItemDto>(users.Count);
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var rolesList = roles?.OrderBy(r => r).ToList() ?? new List<string>();  // Sprawdzamy null dla ról
                list.Add(new UserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    Username = u.UserName ?? string.Empty,
                    Roles = rolesList  // Teraz używamy rolesList
                });
            }
            return Ok(list);
        }

        [HttpPost("users/{userId}/roles")]
        public async Task<ActionResult> AssignRole(string userId, [FromBody] AssignRoleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RoleName) || !AssignableRoles.Contains(dto.RoleName))
            {
                return BadRequest("Nieprawidłowa rola. Dozwolone: User, Moderator, Admin.");
            }

            if (!await _roleManager.RoleExistsAsync(dto.RoleName))
            {
                return BadRequest("Rola nie istnieje w systemie.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Użytkownik nie znaleziony.");
            }

            if (await _userManager.IsInRoleAsync(user, dto.RoleName))
            {
                return Ok(new { message = "Użytkownik ma już tę rolę." });
            }

            if (dto.RoleName == UserRoles.Admin)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Nie można nadać roli Administrator z tego panelu." });
            }

            var add = await _userManager.AddToRoleAsync(user, dto.RoleName);
            if (!add.Succeeded)
            {
                return BadRequest(add.Errors);
            }

            if (dto.RoleName == UserRoles.Moderator)
            {
                var current = await _userManager.GetRolesAsync(user);
                if (!current.Contains(UserRoles.Admin) && !current.Contains(UserRoles.User))
                {
                    await _userManager.AddToRoleAsync(user, UserRoles.User);
                }
            }

            return NoContent();
        }

        [HttpDelete("users/{userId}/roles/{roleName}")]
        public async Task<ActionResult> RemoveRole(string userId, string roleName)
        {
            if (!AssignableRoles.Contains(roleName))
            {
                return BadRequest("Nieprawidłowa rola.");
            }

            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (callerId == userId && roleName == UserRoles.Admin)
            {
                return BadRequest("Nie możesz odebrać sobie roli Administrator.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Użytkownik nie znaleziony.");
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                return Ok(new { message = "Użytkownik nie ma tej roli." });
            }

            var admins = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
            if (roleName == UserRoles.Admin && admins.Count == 1 && admins.Any(u => u.Id == userId))
            {
                return BadRequest("Nie można usunąć ostatniego administratora w systemie.");
            }

            var remove = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!remove.Succeeded)
            {
                return BadRequest(remove.Errors);
            }

            return NoContent();
        }
    }
}
