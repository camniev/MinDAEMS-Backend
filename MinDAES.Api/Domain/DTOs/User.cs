namespace MinDAES.Api.Domain.DTOs
{
    public class UserRegistrationModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string? PassWord { get; set; }
        public string? PKIFilePath { get; set; }
        public string? PKIPW { get; set; }
        public bool MustChangePasswordOnFirstLogin { get; set; }
        public Guid? RoomTypeId { get; set; }
        public UserTypeDto UserType { get; set; }
        public EmployeeDto Employee { get; set; }
    }
    
    public class UserLoginModel
    {
        public string UserName { get; set; }
        public string PassWord { get; set; }
    }

    public class PasswordUpdateModel
    {
        public Guid UserId { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class UserTypeDto
    {
        public Guid UtId { get; set; }
        public string? UtDesc { get; set; }
    }

    public class EmployeeDto
    {
        public Guid EmpId { get; set; }
        public string EmpName { get; set; }
        public string Position { get; set; }
        public string EmailAddress { get; set; }
        public Guid DivisionId { get; set; }
        public bool IsActive { get; set; }
    }
    public class UserStatusDto
    {
        public Guid UserId { get; set; }
        public bool IsActive { get; set; }
    }
}