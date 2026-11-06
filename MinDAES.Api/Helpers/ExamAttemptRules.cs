// ---- Rules (pure, no database) ----

using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

public static class ExamAttemptRules
{
    // How long after the deadline a save/submit is still accepted, so the
    // automatic submit at 0:00 isn't rejected for a slow request.
    public const int SubmissionGraceSeconds = 30;

    public static DateTime Deadline(DateTime startedAt, int durationMinutes) =>
        startedAt.AddMinutes(durationMinutes);

    public static int SecondsRemaining(DateTime startedAt, int durationMinutes, DateTime now) =>
        Math.Max(0, (int)Math.Ceiling((Deadline(startedAt, durationMinutes) - now).TotalSeconds));

    public static bool IsPastDeadline(DateTime startedAt, int durationMinutes, DateTime now, int graceSeconds = 0) =>
        now > Deadline(startedAt, durationMinutes).AddSeconds(graceSeconds);

    public static bool IsAnswered(SavedAnswerDto a) =>
        (a.SelectedOptionIds?.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(a.EssayText);

    public static string ToClientType(QuestionType type) => type switch
    {
        QuestionType.MultipleChoice => "mcq",
        QuestionType.Checkbox => "checkbox",
        QuestionType.TrueFalse => "truefalse",
        QuestionType.Essay => "essay",
        _ => "mcq",
    };

    // Returns an error message, or null if the answers are acceptable.
    // Never trusts the client: every question and option must belong to this
    // exam, and the number of selections must fit the question type.
    public static string? Validate(IEnumerable<Question> questions, IEnumerable<SavedAnswerDto> answers)
    {
        var byId = questions.ToDictionary(q => q.QuestionId);
        var seen = new HashSet<Guid>();

        foreach (var a in answers)
        {
            if (!byId.TryGetValue(a.QuestionId, out var q))
                return "An answer refers to a question that isn't part of this exam.";
            if (!seen.Add(a.QuestionId))
                return "There is more than one answer for the same question.";

            var selected = (a.SelectedOptionIds ?? new List<Guid>()).Distinct().ToList();

            if (q.Type == QuestionType.Essay)
            {
                if (selected.Count > 0)
                    return "Essay questions don't take selected options.";
                continue;
            }

            if (!string.IsNullOrWhiteSpace(a.EssayText))
                return "Only essay questions take a written answer.";

            var validIds = q.Options.Select(o => o.QuestionOptionId).ToHashSet();
            if (selected.Any(id => !validIds.Contains(id)))
                return "An answer selects an option that doesn't belong to its question.";
            if (q.Type != QuestionType.Checkbox && selected.Count > 1)
                return "Only one option can be selected for this question.";
        }

        return null;
    }
}