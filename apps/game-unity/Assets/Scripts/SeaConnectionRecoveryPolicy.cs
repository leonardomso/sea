using System;
using SpacetimeDB;

namespace Sea.Client
{
    public enum SeaConnectionRecoveryAction
    {
        RetryAfterDelay,
        ClearIdentityAndRetry,
        Stop,
    }

    public readonly struct SeaConnectionRecoveryDecision
    {
        public SeaConnectionRecoveryDecision(SeaConnectionRecoveryAction action, float delaySeconds)
        {
            Action = action;
            DelaySeconds = delaySeconds;
        }

        public SeaConnectionRecoveryAction Action { get; }
        public float DelaySeconds { get; }
    }

    public static class SeaConnectionRecoveryPolicy
    {
        private const float InitialRetryDelaySeconds = 2f;
        private const float MaximumRetryDelaySeconds = 30f;

        public static SeaConnectionRecoveryDecision Decide(
            Exception exception,
            bool attemptedWithToken,
            int transientFailureCount)
        {
            var statusCode = FindHttpStatusCode(exception);
            if (statusCode == 401)
            {
                return attemptedWithToken
                    ? new SeaConnectionRecoveryDecision(SeaConnectionRecoveryAction.ClearIdentityAndRetry, 0f)
                    : new SeaConnectionRecoveryDecision(SeaConnectionRecoveryAction.Stop, 0f);
            }

            if (statusCode is 400 or 403 or 404)
            {
                return new SeaConnectionRecoveryDecision(SeaConnectionRecoveryAction.Stop, 0f);
            }

            var exponent = Math.Min(Math.Max(transientFailureCount, 0), 4);
            var delay = Math.Min(InitialRetryDelaySeconds * Math.Pow(2, exponent), MaximumRetryDelaySeconds);
            return new SeaConnectionRecoveryDecision(SeaConnectionRecoveryAction.RetryAfterDelay, (float)delay);
        }

        private static int? FindHttpStatusCode(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is WebSocketUpgradeException upgradeException)
                {
                    return upgradeException.StatusCode;
                }

                var message = current.Message;
                if (message.Contains("401 Unauthorized", StringComparison.OrdinalIgnoreCase)) return 401;
                if (message.Contains("400 Bad Request", StringComparison.OrdinalIgnoreCase)) return 400;
                if (message.Contains("403 Forbidden", StringComparison.OrdinalIgnoreCase)) return 403;
                if (message.Contains("404 Not Found", StringComparison.OrdinalIgnoreCase)) return 404;
            }

            return null;
        }
    }
}
