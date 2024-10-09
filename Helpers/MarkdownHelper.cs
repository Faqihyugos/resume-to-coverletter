using Markdig;

public static class MarkdownHelper
{
    public static string ToHtml(this string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown);
    }
}