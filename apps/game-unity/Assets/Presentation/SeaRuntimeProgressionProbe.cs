using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe
    {
        private bool progressionEnabledForThisRun;
        private bool progressionBaselineCaptured;
        private bool progressionSunkObserved;
        private bool progressionLootObserved;
        private bool progressionLootStopRequested;
        private bool combatValidated;
        private ulong progressionInitialExperience;
        private uint progressionInitialGold;
        private ulong progressionInitialEncounterId;
        private Vector2 progressionSinkPosition;
        private SubscriptionHandle progressionTargetSubscription;

        private bool ObserveProgressionTarget(Ship player, Ship target)
        {
            if (!progressionEnabledForThisRun)
            {
                return false;
            }

            CaptureProgressionBaseline(target);
            if (!target.IsActive || !target.IsAlive)
            {
                progressionSunkObserved = true;
                progressionSinkPosition = LivePosition(target);
                SailToProgressionLoot(player);
                return true;
            }

            if (!progressionSunkObserved)
            {
                return false;
            }

            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(
                connection.LocalIdentity);
            if (progressionLootObserved && progression != null &&
                progression.Experience > progressionInitialExperience &&
                progression.Gold > progressionInitialGold &&
                target.EncounterId != progressionInitialEncounterId)
            {
                progressionEnabledForThisRun = false;
                combatEnabledForThisRun = false;
                MarkRuntimeMilestone(SeaRuntimeMilestone.Progression);
                Debug.Log(
                    "Sea runtime observed NPC sinking, atomic loot, XP, and NPC respawn.",
                    this);
            }

            return true;
        }

        private void CaptureProgressionBaseline(Ship target)
        {
            if (progressionBaselineCaptured)
            {
                return;
            }

            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(
                connection.LocalIdentity);
            if (progression == null)
            {
                return;
            }

            progressionInitialExperience = progression.Experience;
            progressionInitialGold = progression.Gold;
            progressionInitialEncounterId = target.EncounterId;
            progressionTargetSubscription = connection.Connection.SubscriptionBuilder()
                .Subscribe(new[]
                {
                    $"SELECT * FROM ship WHERE entity_id = {target.EntityId}",
                    $"SELECT * FROM ship_movement WHERE entity_id = {target.EntityId}",
                });
            progressionBaselineCaptured = true;
        }

        private void SailToProgressionLoot(Ship player)
        {
            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(
                connection.LocalIdentity);
            if (progression != null && progression.Gold > progressionInitialGold)
            {
                progressionLootObserved = true;
                if (!progressionLootStopRequested)
                {
                    progressionLootStopRequested = true;
                    StopCourse();
                }

                return;
            }

            var loot = connection.Connection.Db.Loot.Iter()
                .Where(item => item.IsActive)
                .OrderBy(item => Vector2.Distance(
                    progressionSinkPosition,
                    new Vector2(item.PositionX, item.PositionY)))
                .FirstOrDefault();
            var destination = loot == null
                ? progressionSinkPosition
                : new Vector2(loot.PositionX, loot.PositionY);
            if (Vector2.Distance(
                    LivePosition(player),
                    destination) <= 2f)
            {
                return;
            }

            if (Time.unscaledTime >= nextCombatCourseTime)
            {
                SetCourse(destination.x, destination.y);
                nextCombatCourseTime = Time.unscaledTime + 1f;
            }
        }
    }
}
