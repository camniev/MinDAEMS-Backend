namespace MinDAES.Api.Domain.Enums
{
    public static class AppRoles
    {
        public const string Administrator = "Administrator";
        public const string Proctor = "Proctor";
        public const string Checker = "Checker";
        public const string Applicant = "Applicant";

        public const string Grading = Administrator + "," + Proctor + "," + Checker; // may use Checking & Grading
        public const string Assigners = Administrator + "," + Proctor;               // may assign checkers to exams
    }
}
