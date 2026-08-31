using System;

namespace SpacetimeDB
{
    [Serializable]
    public sealed class WebSocketUpgradeException : Exception
    {
        public WebSocketUpgradeException(int statusCode, string reason)
            : base($"WebSocket upgrade failed with HTTP {statusCode}: {reason}")
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}
