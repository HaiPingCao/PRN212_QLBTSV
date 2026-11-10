using Microsoft.EntityFrameworkCore;
using QLBT.Server.Data;
using QLBT.Server.Domain;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.Admin;

/// <summary>Students and teachers. Username and role are fixed after creation; passwords are stored as BCrypt hashes.</summary>
public sealed class UserAdminService
{
    public const int MinPasswordLength = 6;

    public async Task<List<UserRow>> List()
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AsNoTracking()
                .OrderBy(u => u.RoleId).ThenBy(u => u.Username)
                .Select(u => new UserRow
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = (UserRole)u.RoleId,
                    ClassCount = u.Classes.Count + u.ClassStudents.Count
                })
                .ToListAsync();
        }
    }

    public async Task<AdminResult> Create(UserRole role, string username, string fullName, string email, string password)
    {
        username = username.Trim();
        fullName = fullName.Trim();
        email = email.Trim();

        string? invalid = ValidateUsername(username) ?? ValidateProfile(fullName, email) ?? ValidatePassword(password);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            if (await db.Users.AnyAsync(u => u.Username == username))
            {
                return AdminResult.Fail("Username " + username + " is already used.");
            }
            if (await db.Users.AnyAsync(u => u.Email == email))
            {
                return AdminResult.Fail("Email " + email + " is already used.");
            }

            User user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                RoleId = (int)role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            AdminLog.Write("Created " + role + " " + username);
            return AdminResult.Success(user.Id);
        }
    }

    /// <param name="newPassword">null or empty keeps the current password.</param>
    public async Task<AdminResult> Update(int id, string fullName, string email, string? newPassword)
    {
        fullName = fullName.Trim();
        email = email.Trim();
        bool resetPassword = !string.IsNullOrEmpty(newPassword);

        string? invalid = ValidateProfile(fullName, email) ?? (resetPassword ? ValidatePassword(newPassword!) : null);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return AdminResult.Fail("User no longer exists.");
            }
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
            {
                return AdminResult.Fail("Email " + email + " is already used.");
            }

            user.FullName = fullName;
            user.Email = email;
            if (resetPassword)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }
            await db.SaveChangesAsync();
            AdminLog.Write("Updated user " + user.Username + (resetPassword ? " (password reset)" : ""));
            return AdminResult.Success(user.Id);
        }
    }

    /// <summary>Only users with no classes (taught or enrolled); others should be suspended in their classes instead.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return AdminResult.Fail("User no longer exists.");
            }
            bool hasClasses = await db.Classes.AnyAsync(c => c.TeacherId == id) || await db.ClassStudents.AnyAsync(cs => cs.StudentId == id);
            if (hasClasses)
            {
                return AdminResult.Fail(user.Username + " has classes and cannot be deleted. Suspend them in the class instead.");
            }

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            AdminLog.Write("Deleted user " + user.Username);
            return AdminResult.Success();
        }
    }

    private static string? ValidateUsername(string username)
    {
        if (username.Length == 0)
        {
            return "Username is required.";
        }
        if (username.Length > 20)
        {
            return "Username is longer than 20 characters.";
        }
        if (username.Any(char.IsWhiteSpace))
        {
            return "Username cannot contain spaces.";
        }
        return null;
    }

    private static string? ValidateProfile(string fullName, string email)
    {
        if (fullName.Length == 0)
        {
            return "Full name is required.";
        }
        if (fullName.Length > 100)
        {
            return "Full name is longer than 100 characters.";
        }
        if (email.Length == 0)
        {
            return "Email is required.";
        }
        if (email.Length > 100)
        {
            return "Email is longer than 100 characters.";
        }
        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return "Email is not valid.";
        }
        return null;
    }

    private static string? ValidatePassword(string password)
    {
        if (password.Length < MinPasswordLength)
        {
            return "Password must have at least " + MinPasswordLength + " characters.";
        }
        return null;
    }
}
