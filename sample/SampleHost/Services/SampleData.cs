namespace SampleHost.Services;

// A stand-in "heavy dependency": a scoped service that loads data asynchronously,
// mimicking a real component that hits a DB/API in OnInitializedAsync.
public record AuditRow(string Time, string Actor, string Action);

public interface ISampleData
{
    Task<List<AuditRow>> GetAsync();
}

public sealed class SampleData : ISampleData
{
    public async Task<List<AuditRow>> GetAsync()
    {
        await Task.Delay(120); // fake IO latency
        return new()
        {
            new("09:42:15", "a.hanif", "login.success"),
            new("09:41:02", "system",  "export.csv"),
            new("09:38:47", "m.rizal", "role.update"),
        };
    }
}
