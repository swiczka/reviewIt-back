using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using review_microservice.Controllers;
using review_microservice.Dtos;
using review_microservice.Interfaces;
using review_microservice.Models;
using System.Security.Claims;
using Xunit;

namespace ReviewMicroservice.Tests
{
    public class ReviewControllerTests
    {
        private readonly Mock<IReviewRepository> _mockReviewRepo;
        private readonly ReviewController _controller;

        public ReviewControllerTests()
        {
            _mockReviewRepo = new Mock<IReviewRepository>();

            _controller = new ReviewController(_mockReviewRepo.Object);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────────

        private void SetupUser(string? userId)
        {
            var claims = new List<Claim>();
            if (userId != null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

            var identity = new ClaimsIdentity(claims, userId != null ? "TestAuth" : null);
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static Review MakeReview(
            int id = 1,
            string userId = "user-1",
            string? title = "Tytuł recenzji",
            string? content = "Treść recenzji",
            int value = 8,
            int albumId = 100)
            => new Review
            {
                Id = id,
                AppUserId = userId,
                Title = title,
                Content = content,
                Value = value,
                AlbumId = albumId,
                CreatedAt = DateTime.UtcNow
            };

        // ─────────────────────────────────────────────────────────────
        // GET /api/review  –  Index
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task Index_ReturnsOk_WithEmptyList_WhenNoReviews()
        {
            _mockReviewRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Review>());

            var result = await _controller.Index();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<ReviewReadDto>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task Index_ReturnsOk_WithMappedDtos()
        {
            var reviews = new List<Review>
            {
                MakeReview(id: 1, userId: "user-1"),
                MakeReview(id: 2, userId: "user-2")
            };
            _mockReviewRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(reviews);

            var result = await _controller.Index();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<ReviewReadDto>>(ok.Value).ToList();

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0].Id);
            Assert.Equal("user-1", list[0].AppUserId);
            Assert.Equal(2, list[1].Id);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/review/{id}  –  GetReviewById
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetReviewById_ReturnsNotFound_WhenReviewDoesNotExist()
        {
            _mockReviewRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Review)null!);

            var result = await _controller.GetReviewById(99);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetReviewById_ReturnsOk_WithCorrectDto()
        {
            var review = MakeReview(id: 5, userId: "user-42", value: 9, albumId: 200);
            _mockReviewRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(review);

            var result = await _controller.GetReviewById(5);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ReviewReadDto>(ok.Value);

            Assert.Equal(5, dto.Id);
            Assert.Equal("user-42", dto.AppUserId);
            Assert.Equal(9, dto.Value);
            Assert.Equal(200, dto.AlbumId);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/review  –  CreateReview
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateReview_ReturnsForbid_WhenCallerIdIsNull()
        {
            SetupUser(userId: null);

            var dto = new ReviewCreateDto
            {
                AppUserId = "user-1",
                Value = 8,
                AlbumId = 100,
                Title = "T",
                Content = "C"
            };

            var result = await _controller.CreateReview(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task CreateReview_ReturnsForbid_WhenCallerIdDoesNotMatchDto()
        {
            SetupUser("user-2"); // zalogowany jako user-2

            var dto = new ReviewCreateDto
            {
                AppUserId = "user-1",
                Value = 8,
                AlbumId = 100 // próbuje stworzyć dla user-1
            };

            var result = await _controller.CreateReview(dto);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task CreateReview_ReturnsBadRequest_WhenRepositoryFails()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.AddAsync(It.IsAny<Review>())).ReturnsAsync(false);

            var dto = new ReviewCreateDto
            {
                AppUserId = "user-1",
                Value = 7,
                AlbumId = 100,
                Title = "T",
                Content = "C"
            };

            var result = await _controller.CreateReview(dto);

            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task CreateReview_ReturnsCreatedAtRoute_WithCorrectDto_WhenSuccessful()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.AddAsync(It.IsAny<Review>())).ReturnsAsync(true);

            var dto = new ReviewCreateDto
            {
                AppUserId = "user-1",
                Value = 9,
                AlbumId = 100,
                Title = "Świetny album",
                Content = "Bardzo polecam."
            };

            var result = await _controller.CreateReview(dto);

            var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
            Assert.Equal("GetReviewById", created.RouteName);

            var readDto = Assert.IsType<ReviewReadDto>(created.Value);
            Assert.Equal("user-1", readDto.AppUserId);
            Assert.Equal(9, readDto.Value);
            Assert.Equal(100, readDto.AlbumId);
            Assert.Equal("Świetny album", readDto.Title);
        }

        [Fact]
        public async Task CreateReview_AllowsNullTitleAndContent_WhenRatingOnly()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.AddAsync(It.IsAny<Review>())).ReturnsAsync(true);

            var dto = new ReviewCreateDto
            {
                AppUserId = "user-1",
                Value = 6,
                AlbumId = 100,
                Title = null,
                Content = null
            };

            var result = await _controller.CreateReview(dto);

            var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
            var readDto = Assert.IsType<ReviewReadDto>(created.Value);
            Assert.Equal(6, readDto.Value);
        }

        // ─────────────────────────────────────────────────────────────
        // PUT /api/review/{id}  –  UpdateReview
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateReview_ReturnsNotFound_WhenReviewDoesNotExist()
        {
            SetupUser("user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Review)null!);

            var dto = new ReviewUpdateDto { AppUserId = "user-1", Value = 8, Title = "T", Content = "C" };

            var result = await _controller.UpdateReview(99, dto);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("99", notFound.Value!.ToString());
        }

        [Fact]
        public async Task UpdateReview_ReturnsForbid_WhenCallerIsNotOwner()
        {
            SetupUser("user-2");
            var review = MakeReview(userId: "user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);

            var dto = new ReviewUpdateDto { AppUserId = "user-1", Value = 8, Title = "T", Content = "C" };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UpdateReview_ReturnsForbid_WhenDtoUserIdDoesNotMatchReviewOwner()
        {
            SetupUser("user-1");
            var review = MakeReview(userId: "user-1");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);

            // dto.AppUserId nie zgadza się z review.AppUserId
            var dto = new ReviewUpdateDto { AppUserId = "user-99", Value = 8, Title = "T", Content = "C" };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task UpdateReview_ReturnsNoContent_WhenUpdateSuccessful()
        {
            SetupUser("user-1");
            var review = MakeReview(userId: "user-1", title: "Stary tytuł", content: "Stara treść");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);
            _mockReviewRepo.Setup(r => r.UpdateAsync(review)).ReturnsAsync(true);

            var dto = new ReviewUpdateDto
            {
                AppUserId = "user-1",
                Value = 10,
                Title = "Nowy tytuł",
                Content = "Nowa treść"
            };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateReview_ReturnsBadRequest_WhenRepositoryFails()
        {
            SetupUser("user-1");
            var review = MakeReview(userId: "user-1", title: "T", content: "C");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);
            _mockReviewRepo.Setup(r => r.UpdateAsync(review)).ReturnsAsync(false);

            var dto = new ReviewUpdateDto
            {
                AppUserId = "user-1",
                Value = 5,
                Title = "Nowy",
                Content = "Nowa treść"
            };

            var result = await _controller.UpdateReview(1, dto);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("1", bad.Value!.ToString());
        }

        [Fact]
        public async Task UpdateReview_UpdatesOnlyRating_WhenReviewIsRatingOnlyAndDtoHasEmptyContent()
        {
            SetupUser("user-1");
            // Recenzja bez tytułu i treści (tylko ocena)
            var review = MakeReview(userId: "user-1", title: null, content: null, value: 5);
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);
            _mockReviewRepo.Setup(r => r.UpdateAsync(review)).ReturnsAsync(true);

            var dto = new ReviewUpdateDto
            {
                AppUserId = "user-1",
                Value = 9,
                Title = null,
                Content = null
            };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal(9, review.Value); // ocena zaktualizowana
        }

        [Fact]
        public async Task UpdateReview_ReturnsBadRequest_WhenReviewHasContentButDtoDoesNot()
        {
            SetupUser("user-1");
            // Recenzja MA tytuł i treść
            var review = MakeReview(userId: "user-1", title: "Stary tytuł", content: "Stara treść");
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);

            // Dto nie ma tytułu i treści — to powinno zwrócić BadRequest
            var dto = new ReviewUpdateDto
            {
                AppUserId = "user-1",
                Value = 7,
                Title = null,
                Content = null
            };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateReview_UpdatesAllFields_WhenBothReviewAndDtoHaveContent()
        {
            SetupUser("user-1");
            var review = MakeReview(userId: "user-1", title: "Stary", content: "Stara treść", value: 5);
            _mockReviewRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(review);
            _mockReviewRepo.Setup(r => r.UpdateAsync(review)).ReturnsAsync(true);

            var dto = new ReviewUpdateDto
            {
                AppUserId = "user-1",
                Value = 10,
                Title = "Nowy tytuł",
                Content = "Nowa treść recenzji"
            };

            var result = await _controller.UpdateReview(1, dto);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Nowy tytuł", review.Title);
            Assert.Equal("Nowa treść recenzji", review.Content);
            Assert.Equal(10, review.Value);
        }
    }
}