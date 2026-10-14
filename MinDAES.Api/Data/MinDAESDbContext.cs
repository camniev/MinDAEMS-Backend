using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Domain.Entities;

namespace MinDAES.Api.Data
{
    public class MinDAESDbContext : DbContext
    {
        public MinDAESDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<Exam> Exams { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<ExamEnrollment> ExamEnrollments { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserType> UserTypes { get; set; }
        public DbSet<Employee> Employees { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Exam>()
                .HasOne(x => x.CreatedBy)
                .WithMany(u => u.CreatedExams)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Exam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuestionOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExamEnrollment>()
                .HasOne(x => x.Exam)
                .WithMany(e => e.Enrollments)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExamEnrollment>()
                .HasOne(x => x.Applicant)
                .WithMany(e => e.Enrollments)
                .HasForeignKey(x => x.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.ExamEnrollment)
                .WithMany(en => en.Answers)
                .HasForeignKey(a => a.ExamEnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnswerOption>()
                .HasOne(o => o.Answer)
                .WithMany(a => a.SelectedOptions)
                .HasForeignKey(o => o.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AnswerOption>()
                .HasOne(o => o.QuestionOption)
                .WithMany()
                .HasForeignKey(o => o.QuestionOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
