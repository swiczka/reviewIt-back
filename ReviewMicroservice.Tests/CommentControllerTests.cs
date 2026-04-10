using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using review_microservice.Controllers;
using review_microservice.Dtos;
using review_microservice.Interfaces;
using review_microservice.Models;
using System.Security.Claims;
using System.Xml.Linq;
using Xunit;

namespace ReviewMicroservice.Tests
{
    public class CommentControllerTests
    {
        private readonly Mock<ICommentRepository> _mockCommentRepo;
        private readonly Mock<IReviewRepository> _mockReviewRepo;
        private readonly CommentController _controller;

        public CommentControllerTests()
        {
            _mockCommentRepo = new Mock<ICommentRepository>();
            _mockReviewRepo = new Mock<IReviewRepository>();
            _controller = new CommentController(_mockCommentRepo.Object, _mockReviewRepo.Object);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────────

        private void SetupUser(string? userId, bool isAdmin = false, bool isModerator = false)
        {
            var claims = new List<Claim>();
            if (userId != null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            if (isAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            if (isModerator)
                claims.Add(new Claim(ClaimTypes.Role, "Moderator"));

            var identity = new ClaimsIdentity(claims, userId != null ? "TestAuth" : null);
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static Review MakeReview(int id = 1, string userId = "user-1")
            => new Review { Id = id, AppUserId = userId, AlbumId = 100, Value = 8, CreatedAt = DateTime.UtcNow };

        private static Comment MakeComment(int id = 1, int reviewId = 1, string userId = "user-1")
            => new Comment { Id = id, ReviewId = reviewId, AppUserId = userId, Content = "Treść komentarza", CreatedDate = DateTime.UtcNow };

        // ─────────────────────────────────────────────────────────────
        // GET /api/comment/review/{reviewId}  –  GetCommentsByReview
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetCommentsByReview_ReturnsNotFound_WhenReviewDoesNotExist()
        {
            _mockReviewRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Review)null!);

            var result = await _controller.GetCommentsByReview(99);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Contains("99", notFound.Value!.ToString());
        }

        [Fact]
        public async Task GetCommentsByReview_ReturnsOk_WithEmptyList_WhenNoComments()
        {
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeReview());
            _mockCommentRepo
                .Setup(c => c.GetByReviewAsync(1))
                .ReturnsAsync(new List<Comment>());

            var result = await _controller.GetCommentsByReview(1);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CommentReadDto>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetCommentsByReview_ReturnsOk_WithMappedDtos()
        {
            var review = MakeReview();
            var comments = new List<Comment>
            {
                MakeComment(id: 1, reviewId: 1, userId: "user-1"),
                MakeComment(id: 2, reviewId: 1, userId: "user-2")
            };

            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);
            _mockCommentRepo.Setup(c => c.GetByReviewAsync(1)).ReturnsAsync(comments);

            var result = await _controller.GetCommentsByReview(1);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CommentReadDto>>(ok.Value).ToList();

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0].Id);
            Assert.Equal("user-1", list[0].AppUserId);
            Assert.Equal(1, list[0].ReviewId);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/comment/me  –  GetMyComments
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetMyComments_ReturnsUnauthorized_WhenUserIdMissing()
        {
            SetupUser(userId: null);

            var result = await _controller.GetMyComments();

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task GetMyComments_ReturnsOk_WithUsersComments()
        {
            SetupUser("user-1");

            var comments = new List<Comment>
            {
                MakeComment(id: 1, reviewId: 1, userId: "user-1"),
                MakeComment(id: 2, reviewId: 2, userId: "user-1")
            };

            _mockCommentRepo.Setup(c => c.GetByAppUserIdAsync("user-1")).ReturnsAsync(comments);

            var result = await _controller.GetMyComments();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CommentReadDto>>(ok.Value).ToList();

            Assert.Equal(2, list.Count);
            Assert.All(list, dto => Assert.Equal("user-1", dto.AppUserId));
        }

        [Fact]
        public async Task GetMyComments_ReturnsOk_WithEmptyList_WhenUserHasNoComments()
        {
            SetupUser("user-1");

            _mockCommentRepo
                .Setup(c => c.GetByAppUserIdAsync("user-1"))
                .ReturnsAsync(new List<Comment>());

            var result = await _controller.GetMyComments();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<CommentReadDto>>(ok.Value);
            Assert.Empty(list);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/comment  –  CreateComment
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateComment_ReturnsBadRequest_WhenReviewDoesNotExist()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Review)null!);

            var dto = new CommentCreateDto { Content = "tekst", AppUserId = "user-1", ReviewId = 99 };

            var result = await _controller.CreateComment(dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("99", bad.Value!.ToString());
        }

        [Fact]
        public async Task CreateComment_ReturnsForbid_WhenCallerIdDoesNotMatchDto()
        {
            SetupUser("user-2"); // zalogowany jako user-2
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeReview());

            // Dto twierdzi, że komentarz należy do user-1
            var dto = new CommentCreateDto { Content = "tekst", AppUserId = "user-1", ReviewId = 1 };

            var result = await _controller.CreateComment(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task CreateComment_ReturnsForbid_WhenCallerIdIsNull()
        {
            SetupUser(userId: null);
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeReview());

            var dto = new CommentCreateDto { Content = "tekst", AppUserId = "user-1", ReviewId = 1 };

            var result = await _controller.CreateComment(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task CreateComment_ReturnsBadRequest_WhenRepositoryFails()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeReview());
            _mockCommentRepo.Setup(c => c.AddAsync(It.IsAny<Comment>())).ReturnsAsync(false);

            var dto = new CommentCreateDto { Content = "tekst", AppUserId = "user-1", ReviewId = 1 };

            var result = await _controller.CreateComment(dto);

            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task CreateComment_ReturnsCreatedAtRoute_WithCorrectDto_WhenSuccessful()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeReview());
            _mockCommentRepo.Setup(c => c.AddAsync(It.IsAny<Comment>())).ReturnsAsync(true);

            var dto = new CommentCreateDto { Content = "Nowy komentarz", AppUserId = "user-1", ReviewId = 1 };

            var result = await _controller.CreateComment(dto);

            var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
            Assert.Equal("GetCommentsByReview", created.RouteName);

            var readDto = Assert.IsType<CommentReadDto>(created.Value);
            Assert.Equal("user-1", readDto.AppUserId);
            Assert.Equal("Nowy komentarz", readDto.Content);
            Assert.Equal(1, readDto.ReviewId);
        }

        // ─────────────────────────────────────────────────────────────
        // DELETE /api/comment/{id}  –  DeleteComment
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteComment_ReturnsNotFound_WhenCommentDoesNotExist()
        {
            SetupUser("user-1");
            _mockCommentRepo.Setup(c => c.GetByIdAsync(99)).ReturnsAsync((Comment)null!);

            var result = await _controller.DeleteComment(99);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("99", notFound.Value!.ToString());
        }

        [Fact]
        public async Task DeleteComment_ReturnsForbid_WhenCallerIsNotOwnerNorAdminNorModerator()
        {
            SetupUser("user-2"); // inny user, bez ról
            var comment = MakeComment(userId: "user-1");
            _mockCommentRepo.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(comment);

            var result = await _controller.DeleteComment(1);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task DeleteComment_ReturnsNoContent_WhenCallerIsOwner()
        {
            SetupUser("user-1");
            var comment = MakeComment(userId: "user-1");
            _mockCommentRepo.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(comment);
            _mockCommentRepo.Setup(c => c.DeleteAsync(comment)).ReturnsAsync(true);

            var result = await _controller.DeleteComment(1);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteComment_ReturnsNoContent_WhenCallerIsAdmin()
        {
            SetupUser("admin-1", isAdmin: true);
            var comment = MakeComment(userId: "user-1"); // inny właściciel
            _mockCommentRepo.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(comment);
            _mockCommentRepo.Setup(c => c.DeleteAsync(comment)).ReturnsAsync(true);

            var result = await _controller.DeleteComment(1);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteComment_ReturnsNoContent_WhenCallerIsModerator()
        {
            SetupUser("mod-1", isModerator: true);
            var comment = MakeComment(userId: "user-1");
            _mockCommentRepo.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(comment);
            _mockCommentRepo.Setup(c => c.DeleteAsync(comment)).ReturnsAsync(true);

            var result = await _controller.DeleteComment(1);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteComment_ReturnsBadRequest_WhenRepositoryFails()
        {
            SetupUser("user-1");
            var comment = MakeComment(userId: "user-1");
            _mockCommentRepo.Setup(c => c.GetByIdAsync(1)).ReturnsAsync(comment);
            _mockCommentRepo.Setup(c => c.DeleteAsync(comment)).ReturnsAsync(false);

            var result = await _controller.DeleteComment(1);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("1", bad.Value!.ToString());
        }
    }
}