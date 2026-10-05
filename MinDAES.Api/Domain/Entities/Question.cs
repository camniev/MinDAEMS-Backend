using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Domain.Entities
{
    public class Question
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        public QuestionType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Points { get; set; } = 5;
        public bool IsRequired { get; set; } = true;
        public int OrderIndex { get; set; }

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }

    public class QuestionOption
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }

        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int OrderIndex { get; set; }
    }
}
