using System.Text;

namespace XlightsQueue.Services;

public static class DisplayNameHelper {
    public const int MaxLength = 30;

    /// <summary>
    /// Cleans a name for the public supporters list: trims, collapses whitespace, strips control and
    /// invisible characters, caps the length. Returns null (= anonymous) when nothing usable is left.
    /// Output is always rendered as text by the frontend, never as HTML.
    /// </summary>
    public static string? Clean(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sb = new StringBuilder();
        var lastSpace = false;
        foreach (var ch in raw.Normalize(NormalizationForm.FormC)) {
            var cat = char.GetUnicodeCategory(ch);
            if (char.IsControl(ch) || cat == System.Globalization.UnicodeCategory.Format) continue;
            if (char.IsWhiteSpace(ch)) {
                if (!lastSpace && sb.Length > 0) sb.Append(' ');
                lastSpace = true;
                continue;
            }
            sb.Append(ch);
            lastSpace = false;
        }
        var name = sb.ToString().Trim();
        if (name.Length > MaxLength) name = name[..MaxLength].TrimEnd();
        return name.Length == 0 || name.Equals("anonymous", StringComparison.OrdinalIgnoreCase) ? null : name;
    }
}
