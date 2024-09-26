using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Common.Utils;
public class JsonUtils
{
    public static string? GetJsonProperties(string jsonString, string fieldName)
    {
        if (string.IsNullOrEmpty(jsonString) || string.IsNullOrEmpty(fieldName)) return null;
        fieldName = char.ToLower(fieldName[0]) + fieldName.Substring(1);
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            JsonElement root = doc.RootElement;
            string? idValue = root.GetProperty(fieldName).GetString();
            return string.IsNullOrEmpty(idValue) ? null : idValue;
        }
    }

    public static string[] SplitStringArray(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString)) return Array.Empty<string>();
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            JsonElement root = doc.RootElement;
            return root.EnumerateArray()
                       .Select(x => x.GetString())
                       .ToArray();
        }
    }
}
