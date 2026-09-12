using System.Xml;

namespace YukiMcp.Yuki;

/// <summary>Parses a raw XML string tool argument into the XmlNode the generated clients expect.</summary>
internal static class YukiXml
{
    public static XmlNode Parse(string xml, string paramName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException($"'{paramName}' must be a well-formed XML document.", paramName);
        }

        var document = new XmlDocument();
        try
        {
            document.LoadXml(xml);
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"'{paramName}' is not well-formed XML: {ex.Message}", paramName, ex);
        }

        return (XmlNode?)document.DocumentElement ?? document;
    }
}
