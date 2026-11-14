using QLBT.Server.DAO;
using QLBT.Server.Domain;
using QLBT.Server.Files;
using QLBT.Server.Models;

namespace QLBT.Server.Admin;

/// <summary>
/// Classes, enrolment and suspension. A class = group name + course + semester (e.g. SE1802 · PRN212 · FA26).
/// A class never changes its semester. A student with submissions cannot be removed (suspend instead).
/// Rule: a student is active in at most one class per course per semester. The DB enforces it with the
/// filtered unique index uq_class_students_course; the checks here give a readable message first.
/// </summary>
public sealed class ClassAdminService
{
    private const string SameCourseRejected = "A student may be enrolled in only one class per course per semester, and the database rejected this change. Please refresh and try again.";

    private readonly ClassDAO _classes = new ClassDAO();
    private readonly CourseDAO _courses = new CourseDAO();
    private readonly SemesterDAO _semesters = new SemesterDAO();
    private readonly UserDAO _users = new UserDAO();
    private readonly AssignmentDAO _assignments = new AssignmentDAO();

    /// <summary>Read-only view of a class's assignments (the admin does not edit them; teachers do, from the client).</summary>
    public Task<List<AssignmentRow>> ListAssignments(int classId)
    {
        return _assignments.ListByClass(classId);
    }

    public Task<List<SubmissionRow>> ListSubmissions(int assignmentId)
    {
        return _assignments.ListSubmissions(assignmentId);
    }

    /// <summary>Full storage path of an assignment's attached file (built from ids; the file has no extension).</summary>
    public string AssignmentFilePath(ClassRow owner, int assignmentId)
    {
        return DbFactory.Storage().AssignmentFile(owner.SemesterId, owner.Id, assignmentId);
    }

    /// <summary>Full storage path of a submitted file (built from ids; the file has no extension).</summary>
    public string SubmissionFilePath(ClassRow owner, SubmissionRow submission)
    {
        return DbFactory.Storage().SubmissionFile(owner.SemesterId, owner.Id, submission.AssignmentId, submission.Id);
    }

    /// <param name="semesterId">null = every semester.</param>
    public Task<List<ClassRow>> ListClasses(int? semesterId)
    {
        return _classes.List(semesterId);
    }

    public Task<List<UserRow>> ListTeachers()
    {
        return _users.ListTeachers();
    }

    public async Task<AdminResult> Create(int semesterId, int courseId, string name, int teacherId)
    {
        name = name.Trim();
        string? invalid = Validate(name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }
        Semester? semester = await _semesters.GetById(semesterId);
        if (semester == null)
        {
            return AdminResult.Fail("The semester no longer exists.");
        }
        string? conflict = await CheckReferencesAndUnique(null, semesterId, courseId, name, teacherId);
        if (conflict != null)
        {
            return AdminResult.Fail(conflict);
        }

        int id = await _classes.Insert(new Class { SemesterId = semesterId, CourseId = courseId, Name = name, TeacherId = teacherId });
        AdminLog.Write("Created class " + name + " (" + semester.Name + ")");
        return AdminResult.Success(id);
    }

    /// <summary>Name, course and teacher. The semester never changes.</summary>
    public async Task<AdminResult> Update(int id, int courseId, string name, int teacherId)
    {
        name = name.Trim();
        string? invalid = Validate(name);
        if (invalid != null)
        {
            return AdminResult.Fail(invalid);
        }
        Class? existing = await _classes.GetById(id);
        if (existing == null)
        {
            return AdminResult.Fail("The class no longer exists.");
        }
        string? conflict = await CheckReferencesAndUnique(id, existing.SemesterId, courseId, name, teacherId);
        if (conflict != null)
        {
            return AdminResult.Fail(conflict);
        }
        if (existing.CourseId != courseId)
        {
            // The active students must not already be in another class of the new course this semester.
            List<int> activeIds = await _classes.ActiveStudentIds(id);
            string? courseConflict = await CheckSameCourse(existing.SemesterId, courseId, activeIds, id);
            if (courseConflict != null)
            {
                return AdminResult.Fail("The course cannot be changed. " + courseConflict);
            }
        }

        await _classes.Update(id, courseId, name, teacherId);
        AdminLog.Write("Updated class " + name + " (" + existing.Semester.Name + ")");
        return AdminResult.Success(id);
    }

    /// <summary>For the delete confirmation: how much goes with the class.</summary>
    public Task<(int Assignments, int Submissions)> CountContents(int id)
    {
        return _classes.CountContents(id);
    }

    /// <summary>DB first (submissions, assignments, enrolments and the class), then the class folder.</summary>
    public async Task<AdminResult> Delete(int id)
    {
        Class? existing = await _classes.GetById(id);
        if (existing == null)
        {
            return AdminResult.Fail("The class no longer exists.");
        }

        await _classes.Delete(id);
        FileStorage.DeleteDirectoryQuietly(DbFactory.Storage().ClassDirectory(existing.SemesterId, existing.Id));
        AdminLog.Write("Deleted class " + existing.Name + " (" + existing.Semester.Name + ")");
        return AdminResult.Success();
    }

    public Task<List<ClassStudentRow>> ListStudents(int classId)
    {
        return _classes.ListStudents(classId);
    }

    /// <summary>
    /// Students for the Add students picker: not in the class yet, and not active in another class of the
    /// same course in the same semester.
    /// </summary>
    public async Task<List<SelectableStudent>> ListAvailableStudents(int classId)
    {
        Class? target = await _classes.GetById(classId);
        if (target == null)
        {
            return new List<SelectableStudent>();
        }
        return await _classes.ListAvailableStudents(target.SemesterId, target.CourseId, classId);
    }

    /// <summary>
    /// Non-students and students already in the class are skipped. All or nothing: if any student is already
    /// active in another class of the same course this semester, nobody is added.
    /// </summary>
    public async Task<AdminResult> AddStudents(int classId, IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return AdminResult.Fail("Please select at least one student.");
        }
        Class? existing = await _classes.GetById(classId);
        if (existing == null)
        {
            return AdminResult.Fail("The class no longer exists.");
        }

        List<int> toAdd = await _classes.FilterNewStudents(classId, studentIds);
        string? courseConflict = await CheckSameCourse(existing.SemesterId, existing.CourseId, toAdd, classId);
        if (courseConflict != null)
        {
            return AdminResult.Fail(courseConflict);
        }
        if (!await _classes.AddStudents(existing, toAdd))
        {
            return AdminResult.Fail(SameCourseRejected);
        }
        AdminLog.Write("Added " + toAdd.Count + " student(s) to class " + existing.Name);
        return AdminResult.Success(classId);
    }

    /// <summary>Hard delete of the enrolment, only without submissions.</summary>
    public async Task<AdminResult> RemoveStudent(int classId, int studentId)
    {
        ClassStudent? member = await _classes.GetEnrolment(classId, studentId);
        if (member == null)
        {
            return AdminResult.Fail("The student is no longer enrolled in this class.");
        }
        if (await _classes.HasSubmissions(classId, studentId))
        {
            return AdminResult.Fail("The student " + member.Student.Username + " cannot be removed because they already have submissions in this class. Please suspend the student instead.");
        }

        await _classes.RemoveStudent(classId, studentId);
        AdminLog.Write("Removed " + member.Student.Username + " from class " + member.Class.Name);
        return AdminResult.Success();
    }

    /// <summary>Allowed at any time. Suspend: is_active = 0, left_at = now. Un-suspend: is_active = 1, left_at = null.</summary>
    public async Task<AdminResult> SetSuspended(int classId, int studentId, bool suspended)
    {
        ClassStudent? member = await _classes.GetEnrolment(classId, studentId);
        if (member == null)
        {
            return AdminResult.Fail("The student is no longer enrolled in this class.");
        }
        if (!suspended)
        {
            // A suspended enrolment counts as removed, so the student may have joined another class of this course since.
            string? courseConflict = await CheckSameCourse(member.SemesterId, member.CourseId, new[] { studentId }, classId);
            if (courseConflict != null)
            {
                return AdminResult.Fail("The suspension cannot be lifted. " + courseConflict);
            }
        }

        if (!await _classes.SetSuspended(classId, studentId, suspended))
        {
            return AdminResult.Fail(SameCourseRejected);
        }
        AdminLog.Write((suspended ? "Suspended " : "Un-suspended ") + member.Student.Username + " in class " + member.Class.Name);
        return AdminResult.Success();
    }

    /// <summary>
    /// Rule: a student is active in at most one class per course per semester. Suspended enrolments count as removed.
    /// Returns a message naming the students and their current class, or null when there is no conflict.
    /// </summary>
    private async Task<string?> CheckSameCourse(int semesterId, int courseId, IReadOnlyCollection<int> studentIds, int excludeClassId)
    {
        List<string> conflicts = await _classes.FindSameCourseConflicts(semesterId, courseId, studentIds, excludeClassId);
        if (conflicts.Count == 0)
        {
            return null;
        }
        Course? course = await _courses.GetById(courseId);
        return "A student may be enrolled in only one " + course?.Code + " class per semester. The following students are already enrolled in another class: "
            + string.Join(", ", conflicts) + ".";
    }

    private async Task<string?> CheckReferencesAndUnique(int? classId, int semesterId, int courseId, string name, int teacherId)
    {
        if (!await _users.IsTeacher(teacherId))
        {
            return "Please select a teacher.";
        }
        Course? course = await _courses.GetById(courseId);
        if (course == null)
        {
            return "Please select a course.";
        }
        if (await _classes.NameExists(semesterId, courseId, name, classId))
        {
            return "A class named " + name + " for course " + course.Code + " already exists in this semester.";
        }
        return null;
    }

    private static string? Validate(string name)
    {
        if (name.Length == 0)
        {
            return "Please enter a name.";
        }
        if (name.Length > 50)
        {
            return "The name must not exceed 50 characters.";
        }
        return null;
    }
}
