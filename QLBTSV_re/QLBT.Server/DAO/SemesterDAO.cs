using Microsoft.EntityFrameworkCore;
using QLBT.Server.Admin;
using QLBT.Server.Models;

namespace QLBT.Server.DAO;

/// <summary>Queries on the semesters table for the admin GUI. One short-lived context per call.</summary>
public sealed class SemesterDAO
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

    public async Task<Semester?> GetById(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        }
    }

    /// <param name="excludeId">The semester being edited, or null when adding.</param>
    public async Task<bool> NameExists(string name, int? excludeId)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Semesters.AnyAsync(s => s.Name == name && s.Id != excludeId);
        }
    }

    public async Task<bool> HasClasses(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            return await db.Classes.AnyAsync(c => c.SemesterId == id);
        }
    }

    /// <summary>Returns the new id.</summary>
    public async Task<int> Insert(Semester semester)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            db.Semesters.Add(semester);
            await db.SaveChangesAsync();
            return semester.Id;
        }
    }

    public async Task Update(int id, string name, DateOnly startDate, DateOnly endDate)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            Semester semester = await db.Semesters.FirstAsync(s => s.Id == id);
            semester.Name = name;
            semester.StartDate = startDate;
            semester.EndDate = endDate;
            await db.SaveChangesAsync();
        }
    }

    public async Task Delete(int id)
    {
        using (Prn212PQlbtContext db = DbFactory.Create())
        {
            await db.Semesters.Where(s => s.Id == id).ExecuteDeleteAsync();
        }
    }
}
