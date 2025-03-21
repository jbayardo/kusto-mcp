namespace KustoMcp;

public static class StringExtensionMethods
{
    public static string IntersperseNewlines(this IEnumerable<string> strings)
    {
        return string.Join(Environment.NewLine, strings);
    }
}
