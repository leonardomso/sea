using System;
using System.Linq;

namespace Sea.Client
{
    public static class SeaClientOptions
    {
        public static string DatabaseName(string[] arguments, string fallback)
            => Value(arguments, "-seaDatabaseName", fallback);

        public static string Profile(string[] arguments, string fallback)
        {
            var profile = Value(arguments, "-seaProfile", fallback);
            if (string.IsNullOrWhiteSpace(profile) ||
                profile.Length > 32 ||
                profile.Any(character =>
                    !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
            {
                throw new ArgumentException("The local client profile is invalid.", nameof(arguments));
            }

            return profile;
        }

        public static string IdentityTokenKey(string profile) =>
            "spacetimedb.identity_token." + Profile(Array.Empty<string>(), profile);

        private static string Value(string[] arguments, string option, string fallback)
        {
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
