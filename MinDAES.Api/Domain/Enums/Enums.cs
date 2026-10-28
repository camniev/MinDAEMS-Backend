namespace MinDAES.Api.Domain.Enums
{
    public enum ExamStatus { Upcoming, Ongoing, Closed, Completed, Archived }
    public enum ExamAccessType { Private, Public }
    public enum QuestionType { MultipleChoice, Checkbox, TrueFalse, Essay }
    public enum EnrollmentStatus { Invited, Registered, Completed, Disqualified }
    public enum AppRole { Admin, Examiner, Applicant }
}
