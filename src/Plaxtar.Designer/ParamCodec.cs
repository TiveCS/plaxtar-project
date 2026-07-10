using System.Text.Json;

namespace Plaxtar.Designer;

// Converts primitive/enum param values between JSON (schema encoding) and CLR
// values usable by DynamicComponent. Enums use the `{ "$enum": "Type.Member" }`
// encoding (SPEC §7.3).
public static class ParamCodec
{
    public static object? FromJson(JsonElement el, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("$enum", out var enumEl))
        {
            var member = enumEl.GetString() ?? "";
            var dot = member.LastIndexOf('.');
            return Enum.Parse(underlying, dot >= 0 ? member[(dot + 1)..] : member);
        }
        if (underlying.IsEnum && el.ValueKind == JsonValueKind.String)
            return Enum.Parse(underlying, el.GetString()!);

        return el.Deserialize(target);
    }

    public static object? ToJson(object? value, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (value is not null && underlying.IsEnum)
            return new Dictionary<string, string> { ["$enum"] = $"{underlying.Name}.{value}" };
        return value;
    }
}
