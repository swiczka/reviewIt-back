using System.Text.RegularExpressions;
using user_microservice.Dtos;

namespace user_microservice.Validators
{
    /// <summary>
    /// Walidacja biznesowa rejestracji — uzupełnia [DataAnnotations] z RegisterInputDto.
    /// Sprawdza reguły, których atrybuty nie obsługują: siłę hasła, niedozwolone znaki
    /// w nazwie użytkownika, czy hasło nie zawiera nazwy użytkownika itp.
    /// </summary>
    public class RegisterValidator
    {
        public ValidationResult Validate(RegisterInputDto dto)
        {
            var result = new ValidationResult();

            ValidateUsername(dto.Username, result);
            ValidateEmail(dto.Email, result);
            ValidatePassword(dto.Password, dto.Username, result);

            return result;
        }

        // ─────────────────────────────────────────────
        // Username
        // ─────────────────────────────────────────────

        private static void ValidateUsername(string username, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(username))
                return; // [Required] w DTO już to obsługuje

            if (username.Length < ValidationConstants.Username.MinLength)
            {
                result.AddError(
                    $"Nazwa użytkownika musi mieć co najmniej {ValidationConstants.Username.MinLength} znaki.");
                return;
            }

            if (username.Length > ValidationConstants.Username.MaxLength)
            {
                result.AddError(
                    $"Nazwa użytkownika może mieć maksymalnie {ValidationConstants.Username.MaxLength} znaków.");
                return;
            }

            if (!Regex.IsMatch(username, ValidationConstants.Username.AllowedPattern))
            {
                result.AddError(
                    "Nazwa użytkownika może zawierać tylko litery, cyfry, podkreślenia i myślniki.");
            }
        }

        // ─────────────────────────────────────────────
        // Email
        // ─────────────────────────────────────────────

        private static void ValidateEmail(string email, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(email))
                return; // [Required] + [EmailAddress] w DTO już to obsługuje

            if (email.Length > ValidationConstants.Email.MaxLength)
            {
                result.AddError(
                    $"Adres e-mail może mieć maksymalnie {ValidationConstants.Email.MaxLength} znaków.");
            }
        }

        // ─────────────────────────────────────────────
        // Password
        // ─────────────────────────────────────────────

        private static void ValidatePassword(string password, string username, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(password))
                return; // [Required] + [MinLength] w DTO już to obsługuje

            if (password.Length > ValidationConstants.Password.MaxLength)
            {
                result.AddError(
                    $"Hasło może mieć maksymalnie {ValidationConstants.Password.MaxLength} znaków.");
                return;
            }

            int uppercaseCount = password.Count(char.IsUpper);
            if (uppercaseCount < ValidationConstants.Password.MinUppercase)
            {
                result.AddError(
                    $"Hasło musi zawierać co najmniej {ValidationConstants.Password.MinUppercase} wielką literę.");
            }

            int digitCount = password.Count(char.IsDigit);
            if (digitCount < ValidationConstants.Password.MinDigits)
            {
                result.AddError(
                    $"Hasło musi zawierać co najmniej {ValidationConstants.Password.MinDigits} cyfrę.");
            }

            int specialCount = password.Count(c => ValidationConstants.Password.SpecialChars.Contains(c));
            if (specialCount < ValidationConstants.Password.MinSpecialChars)
            {
                result.AddError(
                    $"Hasło musi zawierać co najmniej {ValidationConstants.Password.MinSpecialChars} znak specjalny " +
                    $"(np. {ValidationConstants.Password.SpecialChars[..8]}...).");
            }

            // Hasło nie może zawierać nazwy użytkownika (case-insensitive)
            if (!string.IsNullOrWhiteSpace(username) &&
                password.Contains(username, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("Hasło nie może zawierać nazwy użytkownika.");
            }
        }
    }
}
