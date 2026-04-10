using Moq;
using Xunit;
using user_microservice.Controllers;
using user_microservice.Models;
using user_microservice.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Threading.Tasks;
using user_microservice.Interfaces;

namespace user_microservice.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockUserManager = new Mock<UserManager<AppUser>>(Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(), null, null, null, null);
            _controller = new AdminController(_mockUserManager.Object, _mockRoleManager.Object);
        }

        [Fact]
        public async Task GetUsers_ReturnsOkResult_WithListOfUsers()
        {
            // Arrange
            var mockUsers = new List<AppUser>
            {
                new AppUser { Id = "1", UserName = "user1", Email = "user1@example.com" },
                new AppUser { Id = "2", UserName = "user2", Email = "user2@example.com" }
            };
            _mockUserManager.Setup(m => m.Users).Returns(mockUsers.AsQueryable());

            // Act
            var result = await _controller.GetUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsAssignableFrom<List<UserListItemDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task AssignRole_ReturnsBadRequest_WhenRoleIsInvalid()
        {
            // Arrange
            var dto = new AssignRoleDto { RoleName = "InvalidRole" };

            // Act
            var result = await _controller.AssignRole("1", dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nieprawidłowa rola. Dozwolone: User, Moderator, Admin.", badRequestResult.Value);
        }
    }
}