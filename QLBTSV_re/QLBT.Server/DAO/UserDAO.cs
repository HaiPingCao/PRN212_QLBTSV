using Microsoft.EntityFrameworkCore;
using QLBT.Server.Admin;
using QLBT.Server.Models;
using QLBT.Shared;

namespace QLBT.Server.DAO;

/// <summary>Queries on the users table for the admin GUI. One short-lived context per call.</summary>
public sealed class UserDAO
{
    /// <summary>Teachers first, then by username.</summary>
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

    public async Task<List<UserRow>> ListTeachers()
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AsNoTracking()
                .Where(u => u.RoleId == (int)UserRole.Teacher)
                .OrderBy(u => u.Username)
                .Select(u => new UserRow { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, Role = UserRole.Teacher })
                .ToListAsync();
        }
    }

    public async Task<User?> GetById(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        }
    }

    public async Task<bool> IsTeacher(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AnyAsync(u => u.Id == id && u.RoleId == (int)UserRole.Teacher);
        }
    }

    public async Task<bool> UsernameExists(string username)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AnyAsync(u => u.Username == username);
        }
    }

    /// <param name="excludeId">The user being edited, or null when adding.</param>
    public async Task<bool> EmailExists(string email, int? excludeId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Users.AnyAsync(u => u.Email == email && u.Id != excludeId);
        }
    }

    /// <summary>Taught or enrolled in at least one class.</summary>
    public async Task<bool> HasClasses(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Classes.AnyAsync(c => c.TeacherId == id) || await db.ClassStudents.AnyAsync(cs => cs.StudentId == id);
        }
    }

    /// <summary>Returns the new id.</summary>
    public async Task<int> Insert(User user)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }
    }

    /// <param name="passwordHash">null keeps the current password.</param>
    public async Task Update(int id, string fullName, string email, string? passwordHash)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            User user = await db.Users.FirstAsync(u => u.Id == id);
            user.FullName = fullName;
            user.Email = email;
            if (passwordHash != null)
            {
                user.PasswordHash = passwordHash;
            }
            await db.SaveChangesAsync();
        }
    }

    public async Task Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
        }
    }
}
