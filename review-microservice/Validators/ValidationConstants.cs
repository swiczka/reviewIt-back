namespace review_microservice.Validators
{
    public static class ValidationConstants
    {
        public static class Review
        {
            public const int MinValue   = 1;
            public const int MaxValue   = 10;
            public const int MaxTitleLength   = 100;
            public const int MaxContentLength = 10_000;

            // Tytuł jest wymagany, gdy treść jest podana — i na odwrót
            // (recenzja może być tylko oceną, albo mieć i tytuł, i treść)
            public const string TitleRequiredWithContent =
                "Tytuł jest wymagany, jeśli podajesz treść recenzji.";
            public const string ContentRequiredWithTitle =
                "Treść jest wymagana, jeśli podajesz tytuł recenzji.";
        }

        public static class Comment
        {
            public const int MinLength = 1;
            public const int MaxLength = 2_000;

            // Komentarz nie może składać się wyłącznie z białych znaków
            public const string BlankContentError =
                "Treść komentarza nie może być pusta ani składać się tylko z białych znaków.";
        }
    }
}
