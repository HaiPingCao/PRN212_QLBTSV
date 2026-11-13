using QLBT.Server.DAO;
using QLBT.Server.Domain;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.Admin;

/// <summary>Students and teachers. Username and role are fixed after creation; passwords are stored as BCrypt hashes.</summary>
public sealed class UserAdminService
{
    public const int MinPasswordLength = 6;

    private readonly UserDAO _users = new UserDAO();

    public Task<List<UserRow>> List()
    {
        return _users.List();
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
        if (await _users.UsernameExists(username))
        {
            return AdminResult.Fail("The username " + username + " is already in use.");
        }
        if (await _users.EmailExists(email, null))
        {
            return AdminResult.Fail("The email address " + email + " is already in use.");
        }

        int id = await _users.Insert(new User
        {
            Username = username,
            FullName = fullName,
            Email = email,
            RoleId = (int)role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });
        AdminLog.Write("Created " + role + " " + username);
        return AdminResult.Success(id);
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
        User? user = await _users.GetById(id);
        if (user == null)
        {
            return AdminResult.Fail("The user account no longer exists.");
        }
        if (await _users.EmailExists(email, id))
        {
            return AdminResult.Fail("The email address " + email + " is already in use.");
        }

        await _users.Update(id, fullName, email, resetPassword ? BCrypt.Net.BCrypt.HashPassword(newPassword) : null);
        AdminLog.Write("Updated user " + user.Username + (resetPassword ? " (password reset)" : ""));
        return AdminResult.Success(id);
    }

    /// <summary>Only users with no classes (taught or enrolled); others should be suspended in their classes instead.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        User? user = await _users.GetById(id);
        if (user == null)
        {
            return AdminResult.Fail("The user account no longer exists.");
        }
        if (await _users.HasClasses(id))
        {
            return AdminResult.Fail("The account " + user.Username + " cannot be deleted because it belongs to one or more classes. Please suspend the user in the class instead.");
        }

        await _users.Delete(id);
        AdminLog.Write("Deleted user " + user.Username);
        return AdminResult.Success();
    }

    private static string? ValidateUsername(string username)
    {
        if (username.Length == 0)
        {
            return "Please enter a username.";
        }
        if (username.Length > 20)
        {
            return "The username must not exceed 20 characters.";
        }
        if (username.Any(char.IsWhiteSpace))
        {
            return "The username must not contain spaces.";
        }
        return null;
    }

    private static string? ValidateProfile(string fullName, string email)
    {
        if (fullName.Length == 0)
        {
            return "Please enter a full name.";
        }
        if (fullName.Length > 100)
        {
            return "The full name must not exceed 100 characters.";
        }
        if (email.Length == 0)
        {
            return "Please enter an email address.";
        }
        if (email.Length > 100)
        {
            return "The email address must not exceed 100 characters.";
        }
        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return "Please enter a valid email address.";
        }
        return null;
    }

    private static string? ValidatePassword(string password)
    {
        if (password.Length < MinPasswordLength)
        {
            return "The password must contain at least " + MinPasswordLength + " characters.";
        }
        return null;
    }
}
