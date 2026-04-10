namespace user_microservice.Validators
{
    /// <summary>
    /// Wynik walidacji zwracany przez wszystkie walidatory.
    /// Umożliwia zwrócenie listy błędów bez rzucania wyjątków.
    /// </summary>
    public class ValidationResult
    {
        private readonly List<string> _errors = new();

        public bool IsValid       => _errors.Count == 0;
        public IReadOnlyList<string> Errors => _errors;
        public string FirstError  => _errors.FirstOrDefault() ?? string.Empty;

        public void AddError(string message) => _errors.Add(message);

        public static ValidationResult Success() => new();

        public static ValidationResult Failure(string error)
        {
            var result = new ValidationResult();
            result.AddError(error);
            return result;
        }
    }
}
