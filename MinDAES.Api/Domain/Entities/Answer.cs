namespace MinDAES.Api.Domain.Entities
{
    public class Answer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ExamEnrollmentId { get; set; }
        public ExamEnrollment? ExamEnrollment { get; set; }

        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }

        public string? EssayText { get; set; }
        public decimal? PointsAwarded { get; set; }

        public ICollection<AnswerOption> SelectedOptions { get; set; } = new List<AnswerOption>();
    }

    public class AnswerOption
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AnswerId { get; set; }
        public Answer? Answer { get; set; }

        public Guid QuestionOptionId { get; set; }
        public QuestionOption? QuestionOption { get; set; }
    }
}
