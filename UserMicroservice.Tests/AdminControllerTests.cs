using Moq;
using Xunit;
using user_microservice.Controllers;
using user_microservice.Models;
using user_microservice.Dtos;
using user_microservice.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace user_microservice.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockUserManager = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);

            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                Mock.Of<IRoleStore<IdentityRole>>(), null, null, null, null);

            _controller = new AdminController(
                _mockUserManager.Object,
                _mockRoleManager.Object);
        }

        /// <summary>
        /// Ustawia HttpContext z podanym callerId w claimsach (potrzebne dla RemoveRole).
        /// </summary>
        private void SetupHttpContext(string callerId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, callerId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/admin/users  –  GetUsers
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetUsers_ReturnsOk_WithEmptyList_WhenNoUsers()
        {
            _mockUserManager
                .Setup(m => m.Users)
                .Returns(new List<AppUser>().AsQueryable());

            var result = await _controller.GetUsers();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IReadOnlyList<UserListItemDto>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetUsers_ReturnsOk_WithMappedUsers_AndSortedRoles()
        {
            var users = new List<AppUser>
            {
                new AppUser { Id = "1", UserName = "alice", Email = "alice@example.com" },
                new AppUser { Id = "2", UserName = "bob",   Email = "bob@example.com" }
            };

            _mockUserManager
                .Setup(m => m.Users)
                .Returns(users.AsQueryable());

            _mockUserManager
                .Setup(m => m.GetRolesAsync(users[0]))
                .ReturnsAsync(new List<string> { "User", "Moderator" });
            _mockUserManager
                .Setup(m => m.GetRolesAsync(users[1]))
                .ReturnsAsync(new List<string> { "User" });

            var result = await _controller.GetUsers();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IReadOnlyList<UserListItemDto>>(ok.Value);

            Assert.Equal(2, list.Count);

            var alice = list.First(u => u.Id == "1");
            Assert.Equal("alice", alice.Username);
            Assert.Equal("alice@example.com", alice.Email);
            Assert.Equal(new[] { "Moderator", "User" }, alice.Roles); // posortowane

            var bob = list.First(u => u.Id == "2");
            Assert.Equal(new[] { "User" }, bob.Roles);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/admin/users/{userId}/roles  –  AssignRole
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task AssignRole_ReturnsBadRequest_WhenRoleNameIsEmpty()
        {
            var result = await _controller.AssignRole("user-1", new AssignRoleDto { RoleName = "" });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nieprawidłowa rola. Dozwolone: User, Moderator, Admin.", bad.Value);
        }

        [Fact]
        public async Task AssignRole_ReturnsBadRequest_WhenRoleNameIsInvalid()
        {
            var result = await _controller.AssignRole("user-1", new AssignRoleDto { RoleName = "SuperAdmin" });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nieprawidłowa rola. Dozwolone: User, Moderator, Admin.", bad.Value);
        }

        [Fact]
        public async Task AssignRole_ReturnsBadRequest_WhenRoleDoesNotExistInSystem()
        {
            _mockRoleManager
                .Setup(m => m.RoleExistsAsync("User"))
                .ReturnsAsync(false);

            var result = await _controller.AssignRole("user-1", new AssignRoleDto { RoleName = "User" });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Rola nie istnieje w systemie.", bad.Value);
        }

        [Fact]
        public async Task AssignRole_ReturnsNotFound_WhenUserDoesNotExist()
        {
            _mockRoleManager.Setup(m => m.RoleExistsAsync("User")).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.FindByIdAsync("ghost"))
                .ReturnsAsync((AppUser)null!);

            var result = await _controller.AssignRole("ghost", new AssignRoleDto { RoleName = "User" });

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Użytkownik nie znaleziony.", notFound.Value);
        }

        [Fact]
        public async Task AssignRole_ReturnsOk_WhenUserAlreadyHasRole()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("User")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "User")).ReturnsAsync(true);

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "User" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task AssignRole_ReturnsForbidden_WhenTryingToAssignAdminRole()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("Admin")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "Admin" });

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task AssignRole_ReturnsBadRequest_WhenAddToRoleFails()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("User")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "User")).ReturnsAsync(false);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "User"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error" }));

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "User" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AssignRole_ReturnsNoContent_WhenUserRoleAssignedSuccessfully()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("User")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "User")).ReturnsAsync(false);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "User"))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "User" });

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task AssignRole_ReturnsNoContent_AndAlsoAssignsUserRole_WhenModeratorAssignedWithoutBaseRoles()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("Moderator")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "Moderator")).ReturnsAsync(false);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "Moderator"))
                .ReturnsAsync(IdentityResult.Success);

            // Użytkownik nie ma ani User ani Admin — powinien dostać User automatycznie
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Moderator" });
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "User"))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "Moderator" });

            Assert.IsType<NoContentResult>(result);
            _mockUserManager.Verify(m => m.AddToRoleAsync(user, "User"), Times.Once);
        }

        [Fact]
        public async Task AssignRole_DoesNotAssignUserRole_WhenModeratorAlreadyHasAdminRole()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("Moderator")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "Moderator")).ReturnsAsync(false);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "Moderator"))
                .ReturnsAsync(IdentityResult.Success);

            // Ma już Admin — nie trzeba dodawać User
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Moderator", "Admin" });

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "Moderator" });

            Assert.IsType<NoContentResult>(result);
            _mockUserManager.Verify(m => m.AddToRoleAsync(user, "User"), Times.Never);
        }

        [Fact]
        public async Task AssignRole_DoesNotAssignUserRole_WhenModeratorAlreadyHasUserRole()
        {
            var user = new AppUser { Id = "1", UserName = "alice" };

            _mockRoleManager.Setup(m => m.RoleExistsAsync("Moderator")).ReturnsAsync(true);
            _mockUserManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "Moderator")).ReturnsAsync(false);
            _mockUserManager
                .Setup(m => m.AddToRoleAsync(user, "Moderator"))
                .ReturnsAsync(IdentityResult.Success);

            // Ma już User — nie trzeba dodawać ponownie
            _mockUserManager
                .Setup(m => m.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Moderator", "User" });

            var result = await _controller.AssignRole("1", new AssignRoleDto { RoleName = "Moderator" });

            Assert.IsType<NoContentResult>(result);
            _mockUserManager.Verify(m => m.AddToRoleAsync(user, "User"), Times.Never);
        }

        // ─────────────────────────────────────────────────────────────
        // DELETE /api/admin/users/{userId}/roles/{roleName}  –  RemoveRole
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveRole_ReturnsBadRequest_WhenRoleNameIsInvalid()
        {
            SetupHttpContext("caller-1");

            var result = await _controller.RemoveRole("user-1", "InvalidRole");

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nieprawidłowa rola.", bad.Value);
        }

        [Fact]
        public async Task RemoveRole_ReturnsBadRequest_WhenCallerTriesToRemoveOwnAdminRole()
        {
            SetupHttpContext("caller-1");

            var result = await _controller.RemoveRole("caller-1", UserRoles.Admin);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nie możesz odebrać sobie roli Administrator.", bad.Value);
        }

        [Fact]
        public async Task RemoveRole_ReturnsNotFound_WhenUserDoesNotExist()
        {
            SetupHttpContext("caller-1");

            _mockUserManager
                .Setup(m => m.FindByIdAsync("ghost"))
                .ReturnsAsync((AppUser)null!);

            var result = await _controller.RemoveRole("ghost", UserRoles.User);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Użytkownik nie znaleziony.", notFound.Value);
        }

        [Fact]
        public async Task RemoveRole_ReturnsOk_WhenUserDoesNotHaveRole()
        {
            SetupHttpContext("caller-1");

            var user = new AppUser { Id = "user-2", UserName = "bob" };
            _mockUserManager.Setup(m => m.FindByIdAsync("user-2")).ReturnsAsync(user);
            _mockUserManager.Setup(m => m.IsInRoleAsync(user, "User")).ReturnsAsync(false);

            var result = await _controller.RemoveRole("user-2", UserRoles.User);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task RemoveRole_ReturnsBadRequest_WhenRemovingLastAdmin()
        {
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "admin-1", UserName = "lastAdmin" };

            _mockUserManager.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.Admin)).ReturnsAsync(true);

            // Tylko jeden admin w systemie
            _mockUserManager
                .Setup(m => m.GetUsersInRoleAsync(UserRoles.Admin))
                .ReturnsAsync(new List<AppUser> { targetUser });

            var result = await _controller.RemoveRole("admin-1", UserRoles.Admin);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nie można usunąć ostatniego administratora w systemie.", bad.Value);
        }

        [Fact]
        public async Task RemoveRole_ReturnsBadRequest_WhenRemoveFromRoleFails()
        {
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "user-2", UserName = "bob" };

            _mockUserManager.Setup(m => m.FindByIdAsync("user-2")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.User)).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.RemoveFromRoleAsync(targetUser, UserRoles.User))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error" }));

            var result = await _controller.RemoveRole("user-2", UserRoles.User);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RemoveRole_ReturnsNoContent_WhenUserRoleRemovedSuccessfully()
        {
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "user-2", UserName = "bob" };

            _mockUserManager.Setup(m => m.FindByIdAsync("user-2")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.User)).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.RemoveFromRoleAsync(targetUser, UserRoles.User))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.RemoveRole("user-2", UserRoles.User);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveRole_ReturnsNoContent_WhenRemovingAdminRole_WithMultipleAdmins()
        {
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "admin-2", UserName = "secondAdmin" };
            var anotherAdmin = new AppUser { Id = "admin-1", UserName = "firstAdmin" };

            _mockUserManager.Setup(m => m.FindByIdAsync("admin-2")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.Admin)).ReturnsAsync(true);

            // Dwóch adminów — można usunąć
            _mockUserManager
                .Setup(m => m.GetUsersInRoleAsync(UserRoles.Admin))
                .ReturnsAsync(new List<AppUser> { targetUser, anotherAdmin });

            _mockUserManager
                .Setup(m => m.RemoveFromRoleAsync(targetUser, UserRoles.Admin))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.RemoveRole("admin-2", UserRoles.Admin);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveRole_AllowsCaller_ToRemoveOtherUserAdminRole()
        {
            // Caller i target to różne osoby — powinno działać normalnie
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "admin-99", UserName = "someone" };
            var anotherAdmin = new AppUser { Id = "caller-1", UserName = "caller" };

            _mockUserManager.Setup(m => m.FindByIdAsync("admin-99")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.Admin)).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.GetUsersInRoleAsync(UserRoles.Admin))
                .ReturnsAsync(new List<AppUser> { targetUser, anotherAdmin });
            _mockUserManager
                .Setup(m => m.RemoveFromRoleAsync(targetUser, UserRoles.Admin))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.RemoveRole("admin-99", UserRoles.Admin);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveRole_ReturnsNoContent_WhenRemovingModeratorRole()
        {
            SetupHttpContext("caller-1");

            var targetUser = new AppUser { Id = "mod-1", UserName = "moderator" };

            _mockUserManager.Setup(m => m.FindByIdAsync("mod-1")).ReturnsAsync(targetUser);
            _mockUserManager.Setup(m => m.IsInRoleAsync(targetUser, UserRoles.Moderator)).ReturnsAsync(true);
            _mockUserManager
                .Setup(m => m.RemoveFromRoleAsync(targetUser, UserRoles.Moderator))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _controller.RemoveRole("mod-1", UserRoles.Moderator);

            Assert.IsType<NoContentResult>(result);
        }
    }
}