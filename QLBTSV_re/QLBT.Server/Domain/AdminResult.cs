namespace QLBT.Server.Domain;

/// <summary>Outcome of an admin operation. Error is a message for the admin; Id is the saved row on create/update.</summary>
public sealed record AdminResult(bool Ok, string? Error, int? Id)
{
    public static AdminResult Success(int? id = null)
    {
        return new AdminResult(true, null, id);
    }

    public static AdminResult Fail(string error)
    {
        return new AdminResult(false, error, null);
    }
}
