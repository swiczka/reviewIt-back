using System.ComponentModel.DataAnnotations;

namespace review_microservice.Dtos
{
    public class ReviewUpdateDto
    {
        [Required(ErrorMessage = "Value is required.")]
        [Range(1, 10, ErrorMessage = "Value must be in range (1, 10)")]
        public int Value { get; set; }

        [MaxLength(100, ErrorMessage = "Title is too long. Use up to 100 characters.")]
        public string? Title { get; set; }


        [MaxLength(10000, ErrorMessage = "Content is too long. Use up to 10000 characters.")]
        public string? Content { get; set; }
    }
}
