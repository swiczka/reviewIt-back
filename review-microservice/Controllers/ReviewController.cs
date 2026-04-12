using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using review_microservice.Dtos;
using review_microservice.Interfaces;
using review_microservice.Models;
using review_microservice.Validators;

namespace review_microservice.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly ReviewValidator _reviewValidator;

        public ReviewController(IReviewRepository repository, ReviewValidator reviewValidator)
        {
            _reviewRepository = repository;
            _reviewValidator = reviewValidator;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<ReviewReadDto>>> Index()
        {
            var reviews = await _reviewRepository.GetAllAsync();

            var reviewDtos = reviews.Select(review => new ReviewReadDto
            {
                AlbumId = review.AlbumId,
                AppUserId = review.AppUserId,
                Content = review.Content,
                CreatedAt = review.CreatedAt,
                Id = review.Id,
                Title = review.Title,
                Value = review.Value
            });

            return Ok(reviewDtos);
        }

        [AllowAnonymous]
        [HttpGet("{id}", Name = "GetReviewById")]
        public async Task<ActionResult<ReviewReadDto>> GetReviewById(int id)
        {
            var review = await _reviewRepository.GetByIdAsync(id);

            if (review != null)
            {
                return Ok(new ReviewReadDto
                {
                    AlbumId = review.AlbumId,
                    AppUserId = review.AppUserId,
                    Content = review.Content,
                    CreatedAt = review.CreatedAt,
                    Id = review.Id,
                    Title = review.Title,
                    Value = review.Value
                });
            }

            return NotFound();
        }

        [HttpPost]
        public async Task<ActionResult<ReviewReadDto>> CreateReview([FromBody] ReviewCreateDto dto)
        {
            var callerId = JwtUserClaims.GetUserId(User);
            if (callerId == null || dto.AppUserId != callerId)
                return Forbid("You can only create reviews for yourself.");

            // ── Walidacja biznesowa ────────────────────────────────
            var validation = _reviewValidator.ValidateCreate(dto);
            if (!validation.IsValid)
                return BadRequest(new { errors = validation.Errors });
            // ──────────────────────────────────────────────────────

            var review = new Review
            {
                Title = dto.Title,
                Value = dto.Value,
                AlbumId = dto.AlbumId,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow,
                AppUserId = dto.AppUserId
            };

            var success = await _reviewRepository.AddAsync(review);
            if (!success)
                return BadRequest();

            var reviewRead = new ReviewReadDto
            {
                AlbumId = review.AlbumId,
                AppUserId = review.AppUserId,
                Content = review.Content,
                CreatedAt = review.CreatedAt,
                Id = review.Id,
                Title = review.Title,
                Value = review.Value
            };

            return CreatedAtRoute(nameof(GetReviewById), new { id = reviewRead.Id }, reviewRead);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateReview(int id, [FromBody] ReviewUpdateDto dto)
        {
            var review = await _reviewRepository.GetByIdAsync(id);
            if (review == null)
                return NotFound($"Review with id {id} not found.");

            var callerId = JwtUserClaims.GetUserId(User);
            if (review.AppUserId != dto.AppUserId || callerId != review.AppUserId)
                return Forbid("You can only update your own reviews.");

            // ── Walidacja biznesowa ────────────────────────────────
            var validation = _reviewValidator.ValidateUpdate(dto);
            if (!validation.IsValid)
                return BadRequest(new { errors = validation.Errors });
            // ──────────────────────────────────────────────────────

            bool isRatingOnly = string.IsNullOrWhiteSpace(review.Title)
                             && string.IsNullOrWhiteSpace(review.Content);
            bool updateHasEmptyContent = string.IsNullOrWhiteSpace(dto.Title)
                                      || string.IsNullOrWhiteSpace(dto.Content);

            if (isRatingOnly && updateHasEmptyContent)
            {
                review.Value = dto.Value;
            }
            else if (!isRatingOnly && updateHasEmptyContent)
            {
                return BadRequest("Cannot update review with empty title and content. " +
                    "If you want to update only the rating, please provide the existing title and content.");
            }
            else
            {
                review.Title = dto.Title;
                review.Value = dto.Value;
                review.Content = dto.Content;
            }

            var success = await _reviewRepository.UpdateAsync(review);
            if (success)
                return NoContent();

            return BadRequest($"Unable to update review with id {id}");
        }
    }
}