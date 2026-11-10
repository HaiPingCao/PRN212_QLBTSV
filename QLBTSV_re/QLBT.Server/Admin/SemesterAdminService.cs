using Microsoft.EntityFrameworkCore;
using QLBT.Server.Data;
using QLBT.Server.Domain;
using QLBT.Server.Models;

namespace QLBT.Server.Admin;

/// <summary>
/// Semesters. Before the start: edit anything, delete if it has no classes.
/// After the start: start date locked, end date can only move later, no delete.
/// </summary>
public sealed class SemesterAdminService
{
    /// <summary>Newest start date first.</summary>
    public async Task<List<SemesterRow>> List()
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Semesters.AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .Select(s => new SemesterRow
                {
                    Id = s.Id,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    ClassCount = s.Classes.Count
                })
                .ToListAsync();
        }
    }

    public async Task<AdminResult> Create(string name, DateOnly startDate, DateOnly endDate)
    {
        name = name.Trim();
        string? invalid = Validate(name, startDate, endDate);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            if (await db.Semesters.AnyAsync(s => s.Name == name))
            {
                return AdminResult.Fail("Semester " + name + " already exists.");
            }

            Semester semester = new Semester { Name = name, StartDate = startDate, EndDate = endDate };
            db.Semesters.Add(semester);
            await db.SaveChangesAsync();
            AdminLog.Write("Created semester " + name + " (" + startDate + " - " + endDate + ")");
            return AdminResult.Success(semester.Id);
        }
    }

    public async Task<AdminResult> Update(int id, string name, DateOnly startDate, DateOnly endDate)
    {
        name = name.Trim();
        string? invalid = Validate(name, startDate, endDate);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }

        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Semester? semester = await db.Semesters.FirstOrDefaultAsync(s => s.Id == id);
            if (semester == null)
            {
                return AdminResult.Fail("Semester no longer exists.");
            }
            if (SemesterRules.HasStarted(semester))
            {
                if (startDate != semester.StartDate)
                {
                    return AdminResult.Fail("Semester " + semester.Name + " has started: the start date is locked.");
                }
                if (!SemesterRules.CanSetEndDate(semester, endDate))
                {
                    return AdminResult.Fail("Semester " + semester.Name + " has started: the end date can only move later.");
                }
            }
            if (await db.Semesters.AnyAsync(s => s.Name == name && s.Id != id))
            {
                return AdminResult.Fail("Semester " + name + " already exists.");
            }

            semester.Name = name;
            semester.StartDate = startDate;
            semester.EndDate = endDate;
            await db.SaveChangesAsync();
            AdminLog.Write("Updated semester " + name + " (" + startDate + " - " + endDate + ")");
            return AdminResult.Success(semester.Id);
        }
    }

    /// <summary>Only a semester that has not started and has no classes (delete its classes first).</summary>
    public async Task<AdminResult> Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Semester? semester = await db.Semesters.FirstOrDefaultAsync(s => s.Id == id);
            if (semester == null)
            {
                return AdminResult.Fail("Semester no longer exists.");
            }
            if (SemesterRules.HasStarted(semester))
            {
                return AdminResult.Fail("Semester " + semester.Name + " has started and cannot be deleted.");
            }
            if (await db.Classes.AnyAsync(c => c.SemesterId == id))
            {
                return AdminResult.Fail("Semester " + semester.Name + " still has classes. Delete them first.");
            }

            db.Semesters.Remove(semester);
            await db.SaveChangesAsync();
            AdminLog.Write("Deleted semester " + semester.Name);
            return AdminResult.Success();
        }
    }

    private static string? Validate(string name, DateOnly startDate, DateOnly endDate)
    {
        if (name.Length == 0)
        {
            return "Name is required.";
        }
        if (name.Length > 20)
        {
            return "Name is longer than 20 characters.";
        }
        if (endDate <= startDate)
        {
            return "End date must be after the start date.";
        }
        return null;
    }
}
