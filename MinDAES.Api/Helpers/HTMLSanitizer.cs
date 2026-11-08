using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss; // NuGet: HtmlSanitizer

// Essay answers are stored as HTML and later shown to HR / proctors, so they MUST be
// sanitized on the server. The browser editor is not a security boundary: anyone can
// call the API directly with <script> or onerror= in essayText.
public static class EssayHtml
{
    // Keep this in step with the editor (EssayEditor.jsx): StarterKit + tables, no links/images.
    private static readonly string[] AllowedTags =
    {
        "p", "br", "strong", "em", "u", "s", "code", "pre", "blockquote", "hr",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li",
        "table", "thead", "tbody", "tr", "th", "td",
    };

    private static readonly string[] AllowedAttributes = { "colspan", "rowspan" };

    // Hard cap on stored HTML (characters). Tables add a lot of markup.
    public const int MaxHtmlLength = 200_000;

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in AllowedTags) s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        foreach (var a in AllowedAttributes) s.AllowedAttributes.Add(a);
        s.AllowedSchemes.Clear();
        s.AllowedCssProperties.Clear();
        s.AllowDataAttributes = false;
        return s;
    }

    /// <summary>Returns sanitized HTML, or null when there is no visible text.</summary>
    public static string? Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var clean = BuildSanitizer().Sanitize(html);
        return ToPlainText(clean).Length == 0 ? null : clean;
    }

    /// <summary>Visible text only. Use this for emptiness, min/max-length and word-count checks.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        // Block-level closers become spaces so "<p>a</p><p>b</p>" reads as "a b", not "ab".
        var spaced = Regex.Replace(html, @"</(p|li|tr|td|th|h[1-6]|blockquote)>|<br\s*/?>", " ",
            RegexOptions.IgnoreCase);
        var noTags = Regex.Replace(spaced, "<[^>]*>", string.Empty);
        var decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    public static int WordCount(string? html)
    {
        var text = ToPlainText(html);
        return text.Length == 0 ? 0 : text.Split(' ').Length;
    }
}

// ---- Usage: call once at the top of BOTH SaveMyExamAnswers and SubmitMyExam,
// ---- before ExamAttemptRules.Validate, so validation sees the cleaned values.
//
// foreach (var a in data.Answers)
// {
//     if (a.EssayText is { Length: > EssayHtml.MaxHtmlLength })
//         return BadRequest(new { message = "An essay answer is too long." });
//     a.EssayText = EssayHtml.Clean(a.EssayText);   // null when there is no real text
// }
//
// ---- And inside ExamAttemptRules.Validate, measure essays with
// ---- EssayHtml.ToPlainText(answer.EssayText).Length, not EssayText.Length.