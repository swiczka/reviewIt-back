using review_microservice.Dtos;

namespace review_microservice.Validators
{
    /// <summary>
    /// Walidacja biznesowa komentarzy.
    /// Uzupełnia [DataAnnotations] z CommentCreateDto o reguły:
    ///   - treść nie może składać się tylko z białych znaków
    ///   - treść po trim() musi mieć co najmniej 1 znak
    ///   - AppUserId musi być niepustym stringiem (format GUID lub dowolny id z user-ms)
    /// </summary>
    public class CommentValidator
    {
        public ValidationResult Validate(CommentCreateDto dto)
        {
            var result = new ValidationResult();

            ValidateContent(dto.Content, result);
            ValidateAppUserId(dto.AppUserId, result);
            ValidateReviewId(dto.ReviewId, result);

            return result;
        }

        // ─────────────────────────────────────────────
        // Reguły
        // ─────────────────────────────────────────────

        private static void ValidateContent(string content, ValidationResult result)
        {
            if (string.IsNullOrEmpty(content))
                return; // [Required] w DTO już to łapie

            if (string.IsNullOrWhiteSpace(content))
            {
                result.AddError(ValidationConstants.Comment.BlankContentError);
                return;
            }

            // Sprawdź długość po trim — żeby spacje na początku/końcu nie „nadmuchiwały" treści
            var trimmed = content.Trim();

            if (trimmed.Length < ValidationConstants.Comment.MinLength)
            {
                result.AddError(
                    $"Treść komentarza musi mieć co najmniej {ValidationConstants.Comment.MinLength} znak.");
            }

            if (trimmed.Length > ValidationConstants.Comment.MaxLength)
            {
                result.AddError(
                    $"Treść komentarza może mieć maksymalnie {ValidationConstants.Comment.MaxLength} znaków " +
                    $"(po usunięciu spacji: {trimmed.Length}).");
            }
        }

        private static void ValidateAppUserId(string appUserId, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(appUserId))
                return; // [Required] w DTO już to łapie

            // User microservice używa GUID lub stringa — weryfikujemy że to nie same białe znaki
            // i że nie przekracza rozsądnej długości (np. max 128 znaków)
            if (appUserId.Trim().Length == 0)
            {
                result.AddError("Identyfikator użytkownika nie może składać się tylko z białych znaków.");
            }

            if (appUserId.Length > 128)
            {
                result.AddError("Identyfikator użytkownika jest za długi.");
            }
        }

        private static void ValidateReviewId(int reviewId, ValidationResult result)
        {
            if (reviewId <= 0)
            {
                result.AddError("Identyfikator recenzji musi być liczbą dodatnią.");
            }
        }
    }
}
