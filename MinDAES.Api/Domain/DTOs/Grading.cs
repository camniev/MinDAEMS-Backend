// ---- DTOs -------------------------------------------------------------------
public record RosterItemDto(
    Guid ExamEnrollmentId, string ApplicantName, string Email, string ApplicantCode,
    string EnrollmentStatus, DateTime? SubmittedAt, decimal? Score);

public record OptionDto(Guid Id, string Text, bool IsCorrect, int OrderIndex);

public record QuestionDto(
    Guid Id, string Type, string Text, int Points, bool IsRequired, int OrderIndex,
    List<OptionDto> Options);

public record AnswerDto(
    Guid QuestionId, string? EssayText, List<Guid> SelectedOptionIds, decimal? PointsAwarded);

public class MarkDto
{
    public Guid QuestionId { get; set; }
    public decimal PointsAwarded { get; set; }
}

public class GradeRequest
{
    public bool Finalize { get; set; }
    public List<MarkDto> Marks { get; set; } = new();
}