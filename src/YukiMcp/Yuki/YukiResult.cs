using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace YukiMcp.Yuki;

/// <summary>
/// Turns whatever a generated Yuki SOAP client hands back into MCP tool output text. The Yuki
/// WSDL rarely defines real return schemas, so most operations return an untyped
/// <see cref="XmlNode"/> - that gets pretty-printed as XML. Everything else (typed DTOs, arrays
/// of DTOs, scalars) is JSON-serialized so every tool has a working, inspectable result without a
/// bespoke formatter per operation.
/// </summary>
internal static class YukiResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true, // the generated DTOs use public fields, not properties
    };

    public static string Format(object? value)
    {
        switch (value)
        {
            case null:
                return "(no result)";
            case string s:
                return s.Length == 0 ? "(empty string)" : s;
            case XmlNode node:
                return FormatXml(node);
            case byte[] bytes:
                return Convert.ToBase64String(bytes);
            case decimal or int or long or bool:
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(no result)";
            case DateTime dt:
                return dt.ToString("O", CultureInfo.InvariantCulture);
            default:
                return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        }
    }

    private static string FormatXml(XmlNode node)
    {
        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
        {
            node.WriteTo(xmlWriter);
        }

        var text = stringWriter.ToString();
        return text.Length == 0 ? "(empty result)" : text;
    }
}
