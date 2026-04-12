using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using review_microservice.Dtos;
using review_microservice.Interfaces;
using review_microservice.Models;
using review_microservice.Validators;

namespace review_microservice.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommentController : ControllerBase
    {
        private readonly ICommentRepository _commentRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly CommentValidator _commentValidator;

        public CommentController(
            ICommentRepository repository,
            IReviewRepository reviewRepository,
            CommentValidator commentValidator)
        {
            _commentRepository = repository;
            _reviewRepository = reviewRepository;
            _commentValidator = commentValidator;
        }

        [AllowAnonymous]
        [HttpGet("review/{reviewId}", Name = "GetCommentsByReview")]
        public async Task<ActionResult<IReadOnlyCollection<ReviewReadDto>>> GetCommentsByReview(int reviewId)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);
            if (review == null)
                return NotFound($"Review with id {reviewId} not found.");

            var comments = await _commentRepository.GetByReviewAsync(reviewId);

            var commentDtos = comments.Select(c => new CommentReadDto
            {
                AppUserId = c.AppUserId,
                Content = c.Content,
                CreatedDate = c.CreatedDate,
                Id = c.Id,
                ReviewId = c.ReviewId
            });

            return Ok(commentDtos);
        }

        [HttpGet("me", Name = "GetMyComments")]
        public async Task<ActionResult<IReadOnlyCollection<CommentReadDto>>> GetMyComments()
        {
            var userId = JwtUserClaims.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var comments = await _commentRepository.GetByAppUserIdAsync(userId);

            var commentDtos = comments.Select(c => new CommentReadDto
            {
                AppUserId = c.AppUserId,
                Content = c.Content,
                CreatedDate = c.CreatedDate,
                Id = c.Id,
                ReviewId = c.ReviewId
            });

            return Ok(commentDtos);
        }

        [HttpPost]
        public async Task<ActionResult<CommentReadDto>> CreateComment([FromBody] CommentCreateDto dto)
        {
            var review = await _reviewRepository.GetByIdAsync(dto.ReviewId);
            if (review == null)
                return BadRequest($"Review with id {dto.ReviewId} not found.");

            var callerId = JwtUserClaims.GetUserId(User);
            if (callerId == null || callerId != dto.AppUserId)
                return Forbid();

            // ── Walidacja biznesowa ────────────────────────────────
            var validation = _commentValidator.Validate(dto);
            if (!validation.IsValid)
                return BadRequest(new { errors = validation.Errors });
            // ──────────────────────────────────────────────────────

            var comment = new Comment
            {
                Content = dto.Content.Trim(),
                CreatedDate = DateTime.UtcNow,
                AppUserId = dto.AppUserId,
                ReviewId = dto.ReviewId
            };

            var success = await _commentRepository.AddAsync(comment);
            if (!success)
                return BadRequest();

            var commentRead = new CommentReadDto
            {
                AppUserId = comment.AppUserId,
                Content = comment.Content,
                CreatedDate = comment.CreatedDate,
                Id = comment.Id,
                ReviewId = comment.ReviewId
            };

            return CreatedAtRoute(nameof(GetCommentsByReview), new { reviewId = review.Id }, commentRead);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteComment(int id)
        {
            var comment = await _commentRepository.GetByIdAsync(id);
            if (comment == null)
                return NotFound($"Comment with id {id} not found.");

            var callerId = JwtUserClaims.GetUserId(User);
            if (User.IsInRole("Admin") ||
                User.IsInRole("Moderator") ||
                callerId == comment.AppUserId)
            {
                var success = await _commentRepository.DeleteAsync(comment);
                if (success)
                    return NoContent();

                return BadRequest($"Unable to delete comment with id: {id}");
            }

            return Forbid();
        }
    }
}