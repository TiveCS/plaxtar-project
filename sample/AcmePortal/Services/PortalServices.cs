namespace AcmePortal.Services;

// Stand-ins for the real app's "heavy" dependencies (auth session, API/DB access).
// The point for #1 validation: the Composer renders the app's real Shell, which
// consumes these via DI — so the design surface must run inside the real container,
// not an isolated canvas.

public interface IAuthContext
{
    string UserName { get; }
    string Role { get; }
}

public sealed class FakeAuthContext : IAuthContext
{
    public string UserName => "Ada Lovelace";
    public string Role => "Administrator";
}

public interface ICustomerData
{
    Task<int> CountAsync();
}

public sealed class FakeCustomerData : ICustomerData
{
    public async Task<int> CountAsync()
    {
        await Task.Delay(20); // simulate an API/DB round-trip
        return 1287;
    }
}
