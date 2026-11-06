using Server.Models;

namespace Tcp.Server;

/// <summary>
/// Handles every request. Implement with a switch on context.Request.Type (see RequestHandler).
/// Every case except Login (and the test ops) must call context.RequireRole first.
/// Must send exactly one response.
/// </summary>
public interface IRequestHandler
{
    Task Handle(RequestContext context);
}
