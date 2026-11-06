using Server.Models;
using Tcp.Shared;

namespace Tcp.Server;

/// <summary>
/// Routes every request by MessageType. Operations that need the database answer
/// </summary>
public sealed class RequestHandler : IRequestHandler
{
    public async Task Handle(RequestContext ctx)
    {
        switch (ctx.Request.Type)
        {
            case MessageType.Login:
                // TODO (EF): find user, BCrypt.Verify, check role, ctx.Sessions.Create, reply with SessionId.
                await NotImplemented(ctx);
                break;

            case MessageType.Logout:
                if (await ctx.RequireRole(UserRole.Teacher, UserRole.Student))
                {
                    ctx.Sessions.Remove(ctx.Session.Id);
                    await ctx.Ok();
                }
                break;

            // Both roles
            case MessageType.GetProfile:
            case MessageType.DownloadSubmission:
                if (await ctx.RequireRole(UserRole.Teacher, UserRole.Student))
                {
                    await NotImplemented(ctx);
                }
                break;

            // Teacher
            case MessageType.GetMyClasses:
            case MessageType.GetClassStudents:
            case MessageType.CreateAssignment:
            case MessageType.UpdateAssignment:
            case MessageType.DeleteAssignment:
            case MessageType.GetSubmissions:
            case MessageType.GradeSubmission:
            case MessageType.DeleteSubmission:
                if (await ctx.RequireRole(UserRole.Teacher))
                {
                    await NotImplemented(ctx);
                }
                break;

            // Student
            case MessageType.GetMyAssignments:
            case MessageType.DownloadAssignment:
            case MessageType.SubmitAssignment:
            case MessageType.GetMySubmission:
                if (await ctx.RequireRole(UserRole.Student))
                {
                    await NotImplemented(ctx);
                }
                break;

            // Test only
            case MessageType.Echo:
                await Echo(ctx);
                break;

            case MessageType.EchoFile:
                await EchoFile(ctx);
                break;

            default:
                await ctx.Fail(ResponseStatus.Error, "Operation not supported.");
                break;
        }
    }

    private static Task NotImplemented(RequestContext ctx)
    {
        return ctx.Fail(ResponseStatus.Error, "Not implemented yet.");
    }

    // Replies with the request body unchanged (or no body).
    private static Task Echo(RequestContext ctx)
    {
        if (ctx.Request.Body == null)
        {
            return ctx.Ok();
        }
        return ctx.Ok(ctx.Request.Body.Value);
    }

    // Saves the uploaded file to a temporary path, sends it back, then deletes it.
    private static async Task EchoFile(RequestContext ctx)
    {
        string path = Path.Combine(ctx.Storage.Root, "echo", Guid.NewGuid().ToString("N"));
        await ctx.ReceiveFile(path);
        try
        {
            await ctx.SendFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
