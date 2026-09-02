using System;

namespace Sea.Client
{
    public static class SeaRuntimeArguments
    {
        public static bool Has(string name, string[] arguments, string absoluteUrl)
        {
            return Array.Exists(arguments, argument =>
                    string.Equals(argument, name, StringComparison.Ordinal)) ||
                QueryValue(name, absoluteUrl) != null;
        }

        public static string Value(string name, string[] arguments, string absoluteUrl)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return QueryValue(name, absoluteUrl);
        }

        private static string QueryValue(string name, string absoluteUrl)
        {
            if (string.IsNullOrWhiteSpace(absoluteUrl) ||
                !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var expectedName = name.TrimStart('-');
            foreach (var pair in uri.Query.TrimStart('?').Split('&'))
            {
                var separator = pair.IndexOf('=');
                var encodedName = separator < 0 ? pair : pair.Substring(0, separator);
                if (!string.Equals(
                        Uri.UnescapeDataString(encodedName),
                        expectedName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return separator < 0
                    ? string.Empty
                    : Uri.UnescapeDataString(pair.Substring(separator + 1));
            }

            return null;
        }
    }
}
