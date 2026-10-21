using System.ComponentModel.DataAnnotations;

namespace MinDAES.Api.Domain.DTOs
{
    public class SaveExamQuestionsDto
    {
        [Required]
        public List<QuestionDto> Questions { get; set; } = new();
    }

    public class QuestionDto
    {
        [Required]
        public string Type { get; set; } = string.Empty; // "mcq" | "checkbox" | "truefalse" | "essay"

        [Required, MaxLength(2000)]
        public string Text { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int Points { get; set; } = 5;

        public bool Required { get; set; } = true;

        public List<QuestionOptionDto> Options { get; set; } = new();
    }

    public class QuestionOptionDto
    {
        [Required, MaxLength(1000)]
        public string Text { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }
}
