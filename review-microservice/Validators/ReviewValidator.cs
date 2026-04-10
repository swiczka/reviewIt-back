using review_microservice.Dtos;

namespace review_microservice.Validators
{
    /// <summary>
    /// Walidacja biznesowa recenzji.
    /// Uzupełnia [DataAnnotations] z ReviewCreateDto / ReviewUpdateDto o reguły,
    /// których atrybuty nie obsługują:
    ///   - tytuł wymagany gdy treść podana (i odwrotnie)
    ///   - treść nie może składać się tylko z białych znaków
    ///   - tytuł nie może składać się tylko z białych znaków
    /// </summary>
    public class ReviewValidator
    {
        public ValidationResult ValidateCreate(ReviewCreateDto dto)
        {
            var result = new ValidationResult();
            ValidateTitleContentPair(dto.Title, dto.Content, result);
            ValidateNoWhitespaceOnly(dto.Title, "Tytuł", result);
            ValidateNoWhitespaceOnly(dto.Content, "Treść", result);
            return result;
        }

        public ValidationResult ValidateUpdate(ReviewUpdateDto dto)
        {
            var result = new ValidationResult();
            ValidateTitleContentPair(dto.Title, dto.Content, result);
            ValidateNoWhitespaceOnly(dto.Title, "Tytuł", result);
            ValidateNoWhitespaceOnly(dto.Content, "Treść", result);
            return result;
        }

        // ─────────────────────────────────────────────
        // Reguły
        // ─────────────────────────────────────────────

        /// <summary>
        /// Recenzja może być:
        ///   a) tylko oceną (title null/empty AND content null/empty) — dozwolone
        ///   b) pełną recenzją (title niepusty AND content niepusty)  — dozwolone
        ///   c) tylko tytułem bez treści lub tylko treścią bez tytułu — NIEDOZWOLONE
        /// </summary>
        private static void ValidateTitleContentPair(
            string? title, string? content, ValidationResult result)
        {
            bool hasTitle   = !string.IsNullOrWhiteSpace(title);
            bool hasContent = !string.IsNullOrWhiteSpace(content);

            if (hasTitle && !hasContent)
                result.AddError(ValidationConstants.Review.ContentRequiredWithTitle);

            if (hasContent && !hasTitle)
                result.AddError(ValidationConstants.Review.TitleRequiredWithContent);
        }

        /// <summary>
        /// Pole, które zostało podane, nie może składać się wyłącznie z białych znaków.
        /// (Puste/null jest dozwolone — tym zajmuje się ValidateTitleContentPair.)
        /// </summary>
        private static void ValidateNoWhitespaceOnly(
            string? value, string fieldName, ValidationResult result)
        {
            if (value is not null && value.Length > 0 && string.IsNullOrWhiteSpace(value))
                result.AddError($"{fieldName} nie może składać się tylko z białych znaków.");
        }
    }
}
