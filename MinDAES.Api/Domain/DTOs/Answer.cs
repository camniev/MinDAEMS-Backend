using System.ComponentModel.DataAnnotations;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

using System.ComponentModel.DataAnnotations;

// ---- Request DTOs ----

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
    // No email/identity field on purpose: who is answering comes from the
    // signed-in user's access token, never from the request body.
    public List<SavedAnswerDto> Answers { get; set; } = new();
}

// ---- Response DTOs ----

// One row on the applicant's "My Exam Schedules" page. Deliberately narrow —
// nothing about any other applicant is reachable through this shape.
public class ApplicantExamScheduleResponseDto
{
    public Guid ExamId { get; set; }
    public Guid ExamEnrollmentId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public DateOnly ExamDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? Instructions { get; set; }
    public string Status { get; set; } = string.Empty;           // the exam's own status — always "Ongoing" here
    public string EnrollmentStatus { get; set; } = string.Empty; // this applicant's own roster status
    public decimal? Score { get; set; }                          // this applicant's own score only
}

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
    public ApplicantAttachmentDto? Attachment { get; set; }
}

public class ApplicantAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
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