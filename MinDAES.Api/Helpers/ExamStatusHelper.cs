// Shared by ExamController (GetExamSchedules) and the new ApplicantController
// endpoint below. Previously this lived as a private method inside
// ExamController only — pulling it out here so the applicant-facing endpoint
// can't drift out of sync with the admin one on what "Ongoing" actually means.

using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

public static class ExamStatusHelper
{
    public static string ComputeDisplayStatus(Exam exam, DateTime nowPh)
    {
        if (exam.IsArchived) return "Archived";
 
        if (exam.Status == ExamStatus.Ongoing)
        {
            var examEnd = exam.ExamDate.ToDateTime(exam.StartTime).AddMinutes(exam.DurationMinutes);
            if (nowPh > examEnd) return "Closed";
        }
 
        return exam.Status.ToString();
    }
}
 
// In ExamController, replace the existing private ComputeDisplayStatus
// method with a call to ExamStatusHelper.ComputeDisplayStatus(exam, now).