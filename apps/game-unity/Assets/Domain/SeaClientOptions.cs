using System;

namespace Sea.Client
{
    public static class SeaClientOptions
    {
        public static string DatabaseName(string[] arguments, string fallback)
        {
            const string option = "-seaDatabaseName";
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index].StartsWith(option + "=", StringComparison.Ordinal))
                {
                    return arguments[index][(option.Length + 1)..];
                }

                if (arguments[index] == option && index + 1 < arguments.Length)
                {
                    return arguments[index + 1];
                }
            }

            return fallback;
        }
    }
}
