namespace user_microservice.Validators
{
    public static class ValidationConstants
    {
        public static class Password
        {
            public const int MinLength        = 8;
            public const int MaxLength        = 128;
            public const int MinUppercase     = 1;
            public const int MinDigits        = 1;
            public const int MinSpecialChars  = 1;
            public const string SpecialChars  = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        }

        public static class Username
        {
            public const int MinLength = 3;
            public const int MaxLength = 32;
            // Tylko litery, cyfry, podkreślenia i myślniki
            public const string AllowedPattern = @"^[a-zA-Z0-9_\-]+$";
        }

        public static class Email
        {
            public const int MaxLength = 254; // RFC 5321
        }
    }
}
