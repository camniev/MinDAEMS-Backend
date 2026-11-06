using System.ComponentModel.DataAnnotations;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

// ---- Request DTOs ----

public class StartExamDto
{
    // Temporary identity, same as MySchedules — replace with the signed-in
    // applicant once real login exists.
    [Required]
    public string Email { get; set; } = string.Empty;
}

public class SavedAnswerDto
{
    public Guid QuestionId { get; set; }

    // Option ids for MCQ / True-False (at most one) and Checkbox (any number).
    public List<Guid> SelectedOptionIds { get; set; } = new();

    // Essay questions only.
    [MaxLength(10000)]
    public string? EssayText { get; set; }
}

public class ExamAnswersDto
{
    [Required]
    public string Email { get; set; } = string.Empty;

    public List<SavedAnswerDto> Answers { get; set; } = new();
}

// ---- Response DTOs ----
// What the applicant's browser receives. There is deliberately NO IsCorrect
// anywhere in here — unlike ExamQuestionOptionResponseDto (HR's view), this
// must never contain the answer key.

public class ApplicantOptionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class ApplicantQuestionDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // mcq | checkbox | truefalse | essay
    public string Text { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool Required { get; set; }
    public List<ApplicantOptionDto> Options { get; set; } = new();
}

public class ExamSessionResponseDto
{
    public Guid ExamEnrollmentId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }

    // Server-authoritative time left. Sent as a duration (not a timestamp) so
    // the browser's clock being wrong doesn't matter.
    public int SecondsRemaining { get; set; }

    public List<ApplicantQuestionDto> Questions { get; set; } = new();
    public List<SavedAnswerDto> Answers { get; set; } = new(); // previously saved, for resuming
}