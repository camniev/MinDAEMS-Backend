using BCrypt.Net;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Microsoft.Identity.Client;

namespace MinDAES.Api.Utilities
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}