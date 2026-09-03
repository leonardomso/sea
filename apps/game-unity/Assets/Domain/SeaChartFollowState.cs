using System;

namespace Sea.Client
{
    /// <summary>
    /// Whether the chart camera follows the player's ship. Manual camera input interrupts
    /// following; it resumes on its own once the input has been idle for the ease-back delay.
    /// </summary>
    public sealed class SeaChartFollowState
    {
        public const float EaseBackDelaySeconds = 3f;

        private float idleSeconds;

        public bool IsFollowing { get; private set; } = true;

        public void Interrupt()
        {
            IsFollowing = false;
            idleSeconds = 0f;
        }

        public void Resume() => IsFollowing = true;

        public void Advance(float deltaSeconds)
        {
            if (IsFollowing)
            {
                return;
            }

            idleSeconds += Math.Max(0f, deltaSeconds);
            if (idleSeconds >= EaseBackDelaySeconds)
            {
                IsFollowing = true;
            }
        }
    }
}
