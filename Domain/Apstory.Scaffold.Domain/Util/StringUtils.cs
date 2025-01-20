namespace Apstory.Scaffold.Domain.Util
{
    public static class StringUtils
    {
        public static string ToCamelCase(this string toConvert)
        {
            return char.ToLowerInvariant(toConvert[0]) + toConvert.Substring(1);
        }

        public static string ToPascalCase(this string toConvert)
        {
            return char.ToUpperInvariant(toConvert[0]) + toConvert.Substring(1);
        }
    }
}
