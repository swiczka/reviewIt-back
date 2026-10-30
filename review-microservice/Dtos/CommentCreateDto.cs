using review_microservice.Models;
using System.ComponentModel.DataAnnotations;

namespace review_microservice.Dtos
{
    public class CommentCreateDto
    {
        [Required(ErrorMessage = "Comment content is required.")]
        public string Content { get; set; }


        [Required(ErrorMessage = "Comment ReviewId has not been provided.")]
        public int ReviewId { get; set; }
    }
}
