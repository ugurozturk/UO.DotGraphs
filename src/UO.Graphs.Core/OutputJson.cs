using System.Text.Json;
using System.Text.Json.Nodes;

namespace UO.Graphs.Core;

// Shared stdout measurement: serialized JSON plus exactly one LF, in Unicode scalars.
public static class OutputJson
{
    public static JsonSerializerOptions Options { get; } = new(GraphJson.Options) { WriteIndented = false };
    public static int Characters(string text) => text.EnumerateRunes().Count();
    public static string SerializeCounted(JsonObject document)
    {
        while (true)
        {
            var text = document.ToJsonString(Options) + "\n";
            var count = Characters(text);
            if (document["budget"]!["actualChars"]!.GetValue<int>() == count) return text;
            document["budget"]!["actualChars"] = count;
        }
    }
}
