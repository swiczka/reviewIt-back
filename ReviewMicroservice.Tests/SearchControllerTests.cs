using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using review_microservice.Controllers;
using review_microservice.Dtos;
using review_microservice.Interfaces;

namespace ReviewMicroservice.Tests
{
    public class SearchControllerTests
    {
        private readonly Mock<IDiscogsService> _mockDiscogsService;
        private readonly SearchController _controller;

        public SearchControllerTests()
        {
            _mockDiscogsService = new Mock<IDiscogsService>();
            _controller = new SearchController(_mockDiscogsService.Object);
        }

        // ─────────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────────

        private static PagedResponseDto<DiscogsSearchItemDto> MakePagedResponse(int count = 2)
        {
            var items = Enumerable.Range(1, count).Select(i => new DiscogsSearchItemDto
            {
                Id = i,
                Title = $"Album {i}",
                Type = "master",
                Year = 2000 + i
            }).ToList();

            return new PagedResponseDto<DiscogsSearchItemDto>
            {
                Items = items,
                CurrentPage = 1,
                PageSize = 10,
                TotalItems = count,
                TotalPages = 1
            };
        }

        private static DiscogsMasterDetailDto MakeMasterDetail(int id = 42)
            => new DiscogsMasterDetailDto
            {
                Id = id,
                Title = "Testowy Album",
                Year = 2010,
                Artists = new[] { "Artysta" },
                Genres = new[] { "Rock" },
                Styles = new[] { "Alternative Rock" }
            };

        // ─────────────────────────────────────────────────────────────
        // GET /api/search/discogs  –  SearchDiscogs
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task SearchDiscogs_ReturnsBadRequest_WhenQueryIsEmpty()
        {
            var result = await _controller.SearchDiscogs(q: "");

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Query parameter 'q' is required.", bad.Value);
        }

        [Fact]
        public async Task SearchDiscogs_ReturnsBadRequest_WhenQueryIsWhitespace()
        {
            var result = await _controller.SearchDiscogs(q: "   ");

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Query parameter 'q' is required.", bad.Value);
        }

        [Fact]
        public async Task SearchDiscogs_ReturnsOk_WithPagedResults_WhenSuccessful()
        {
            var response = MakePagedResponse(3);
            _mockDiscogsService
                .Setup(s => s.SearchAsync("rock", 1, 10))
                .ReturnsAsync(response);

            var result = await _controller.SearchDiscogs(q: "rock", page: 1, pageSize: 10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<PagedResponseDto<DiscogsSearchItemDto>>(ok.Value);
            Assert.Equal(3, dto.TotalItems);
            Assert.Equal(1, dto.CurrentPage);
            Assert.Equal(10, dto.PageSize);
            Assert.Equal(3, dto.Items.Count);
        }

        [Fact]
        public async Task SearchDiscogs_PassesCorrectPaginationParameters_ToService()
        {
            var response = MakePagedResponse();
            _mockDiscogsService
                .Setup(s => s.SearchAsync("jazz", 3, 5))
                .ReturnsAsync(response);

            var result = await _controller.SearchDiscogs(q: "jazz", page: 3, pageSize: 5);

            Assert.IsType<OkObjectResult>(result.Result);
            _mockDiscogsService.Verify(s => s.SearchAsync("jazz", 3, 5), Times.Once);
        }

        [Fact]
        public async Task SearchDiscogs_UsesDefaultPagination_WhenNotProvided()
        {
            var response = MakePagedResponse();
            _mockDiscogsService
                .Setup(s => s.SearchAsync("pop", 1, 10))
                .ReturnsAsync(response);

            // page i pageSize mają domyślne wartości 1 i 10
            var result = await _controller.SearchDiscogs(q: "pop");

            Assert.IsType<OkObjectResult>(result.Result);
            _mockDiscogsService.Verify(s => s.SearchAsync("pop", 1, 10), Times.Once);
        }

        [Fact]
        public async Task SearchDiscogs_Returns502_WhenHttpRequestExceptionThrown()
        {
            _mockDiscogsService
                .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var result = await _controller.SearchDiscogs(q: "metal");

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
        }

        [Fact]
        public async Task SearchDiscogs_Returns500_WhenUnexpectedExceptionThrown()
        {
            _mockDiscogsService
                .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            var result = await _controller.SearchDiscogs(q: "blues");

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task SearchDiscogs_ReturnsOk_WithEmptyItems_WhenNoResultsFound()
        {
            var emptyResponse = new PagedResponseDto<DiscogsSearchItemDto>
            {
                Items = Array.Empty<DiscogsSearchItemDto>(),
                CurrentPage = 1,
                PageSize = 10,
                TotalItems = 0,
                TotalPages = 0
            };

            _mockDiscogsService
                .Setup(s => s.SearchAsync("xyznotexist", 1, 10))
                .ReturnsAsync(emptyResponse);

            var result = await _controller.SearchDiscogs(q: "xyznotexist");

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<PagedResponseDto<DiscogsSearchItemDto>>(ok.Value);
            Assert.Empty(dto.Items);
            Assert.Equal(0, dto.TotalItems);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/search/discogs/master/{id}  –  GetDiscogsMaster
        // ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetDiscogsMaster_ReturnsOk_WithMasterDetail_WhenFound()
        {
            var master = MakeMasterDetail(id: 42);
            _mockDiscogsService.Setup(s => s.GetMasterByIdAsync(42)).ReturnsAsync(master);

            var result = await _controller.GetDiscogsMaster(42);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<DiscogsMasterDetailDto>(ok.Value);
            Assert.Equal(42, dto.Id);
            Assert.Equal("Testowy Album", dto.Title);
            Assert.Equal(2010, dto.Year);
        }

        [Fact]
        public async Task GetDiscogsMaster_ReturnsNotFound_WhenServiceReturnsNull()
        {
            _mockDiscogsService
                .Setup(s => s.GetMasterByIdAsync(999))
                .ReturnsAsync((DiscogsMasterDetailDto)null!);

            var result = await _controller.GetDiscogsMaster(999);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetDiscogsMaster_ReturnsBadRequest_WhenArgumentOutOfRangeExceptionThrown()
        {
            _mockDiscogsService
                .Setup(s => s.GetMasterByIdAsync(It.IsAny<int>()))
                .ThrowsAsync(new ArgumentOutOfRangeException());

            var result = await _controller.GetDiscogsMaster(-1);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GetDiscogsMaster_Returns502_WhenHttpRequestExceptionThrown()
        {
            _mockDiscogsService
                .Setup(s => s.GetMasterByIdAsync(It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Discogs unavailable"));

            var result = await _controller.GetDiscogsMaster(1);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetDiscogsMaster_Returns500_WhenUnexpectedExceptionThrown()
        {
            _mockDiscogsService
                .Setup(s => s.GetMasterByIdAsync(It.IsAny<int>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            var result = await _controller.GetDiscogsMaster(1);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetDiscogsMaster_ReturnsOk_WithTracklist_WhenMasterHasTracks()
        {
            var master = new DiscogsMasterDetailDto
            {
                Id = 10,
                Title = "Album z trackami",
                Year = 2020,
                Artists = new[] { "Artysta A" },
                Genres = new[] { "Electronic" },
                Tracklist = new[]
                {
                    new DiscogsTrackItemDto { Position = "A1", Title = "Utwór 1", Duration = "3:45" },
                    new DiscogsTrackItemDto { Position = "A2", Title = "Utwór 2", Duration = "4:20" }
                }
            };

            _mockDiscogsService.Setup(s => s.GetMasterByIdAsync(10)).ReturnsAsync(master);

            var result = await _controller.GetDiscogsMaster(10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<DiscogsMasterDetailDto>(ok.Value);

            Assert.Equal(2, dto.Tracklist.Count);
            Assert.Equal("Utwór 1", dto.Tracklist[0].Title);
            Assert.Equal("A2", dto.Tracklist[1].Position);
        }
    }
}