using System;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaAuthTokenStore
    {
        public const string DefaultTokenKey = "spacetimedb.identity_token";

        private readonly string tokenKey;

        public SeaAuthTokenStore(string tokenKey = DefaultTokenKey)
        {
            if (string.IsNullOrWhiteSpace(tokenKey))
            {
                throw new ArgumentException("Token key is required.", nameof(tokenKey));
            }

            this.tokenKey = tokenKey;
        }

        public string Token => PlayerPrefs.GetString(tokenKey, string.Empty);

        public void Save(string token)
        {
            PlayerPrefs.SetString(tokenKey, token ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(tokenKey);
            PlayerPrefs.Save();
        }
    }
}
