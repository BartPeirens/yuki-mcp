namespace YukiMcp.Yuki;

/// <summary>
/// The Yuki WSDL exposes several closed value sets (sort orders, search options, ...) as SOAP
/// enums. Tool parameters take their string names instead of the generated enum type directly, so
/// this validates and converts, with an error message that lists the valid values for the model
/// to self-correct on.
/// </summary>
internal static class YukiEnumHelper
{
    public static TEnum Parse<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result))
        {
            return result;
        }

        var validValues = string.Join(", ", Enum.GetNames<TEnum>());
        throw new ArgumentException($"'{value}' is not a valid value for '{paramName}'. Valid values: {validValues}.", paramName);
    }
}
