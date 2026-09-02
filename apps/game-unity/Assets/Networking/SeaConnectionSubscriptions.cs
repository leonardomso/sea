using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaConnectionController
    {
        private readonly SeaSpatialInterest spatialInterest = new();
        private readonly SeaSubscriptionGeneration spatialGenerations = new();
        private readonly SeaSubscriptionGeneration focusGenerations = new();
        private readonly Dictionary<ulong, VolleyEndpoint> relevantVolleys = new();
        private SubscriptionHandle activeSpatialSubscription;
        private SubscriptionHandle pendingSpatialSubscription;
        private SubscriptionHandle activeFocusSubscription;
        private SubscriptionHandle pendingFocusSubscription;
        private ulong selectedTargetEntityId;
        private string focusKey = string.Empty;

        public event Action<ulong> ShipLeftInterest;
        public event Action<ulong> WorldObjectLeftInterest;

        private void RefreshSpatialScope(DbConnection connection, Ship ship)
        {
            if (ship.EntityId != subscribedPlayerEntityId)
            {
                return;
            }

            spatialInterest.Observe(
                ship.ChunkX,
                ship.ChunkY,
                Time.realtimeSinceStartupAsDouble);
            if (selectedTargetEntityId != ship.TargetEntityId)
            {
                selectedTargetEntityId = ship.TargetEntityId;
                RefreshFocusScope(connection);
            }
        }

        private void RefreshSpatialScope(DbConnection connection, ShipMovement movement)
        {
            if (movement.EntityId != subscribedPlayerEntityId)
            {
                return;
            }

            spatialInterest.Observe(
                movement.ChunkX,
                movement.ChunkY,
                Time.realtimeSinceStartupAsDouble);
        }

        private void SubscribeSpatialScope(DbConnection connection, int chunkX, int chunkY)
        {
            spatialInterest.Observe(
                chunkX,
                chunkY,
                Time.realtimeSinceStartupAsDouble);
            ApplyPendingSpatialInterest(Time.realtimeSinceStartupAsDouble);
        }

        private void ApplyPendingSpatialInterest(double nowSeconds)
        {
            if (Connection == null ||
                !spatialInterest.TryTakeDue(nowSeconds, out var chunk))
            {
                return;
            }

            StartSpatialSubscription(Connection, chunk);
        }

        private void StartSpatialSubscription(DbConnection connection, SeaChunk chunk)
        {
            var generation = spatialGenerations.Begin();
            var previous = activeSpatialSubscription;
            SubscriptionHandle next = null;
            next = connection.SubscriptionBuilder()
                .OnApplied(_ => ApplySpatialSubscription(
                    generation,
                    chunk,
                    next,
                    previous))
                .OnError((_, error) => HandleSpatialSubscriptionError(
                    generation,
                    chunk,
                    error))
                .Subscribe(SeaSubscriptionPlan.Spatial(chunk.X, chunk.Y, radius: 1).ToArray());
            pendingSpatialSubscription = next;
        }

        private void ApplySpatialSubscription(
            ulong generation,
            SeaChunk chunk,
            SubscriptionHandle next,
            SubscriptionHandle previous)
        {
            if (!spatialGenerations.IsCurrent(generation))
            {
                UnsubscribeIfActive(next);
                return;
            }

            activeSpatialSubscription = next;
            pendingSpatialSubscription = null;
            spatialInterest.Applied(chunk);
            if (previous != next)
            {
                UnsubscribeIfActive(previous);
            }

            IsSubscribed = true;
            Status = "Ready";
            Debug.Log($"Sea client ready. Chunk {chunk.X},{chunk.Y}.", this);
        }

        private void HandleSpatialSubscriptionError(
            ulong generation,
            SeaChunk chunk,
            Exception error)
        {
            if (!spatialGenerations.IsCurrent(generation))
            {
                return;
            }

            pendingSpatialSubscription = null;
            spatialInterest.Failed(chunk, Time.realtimeSinceStartupAsDouble);
            IsSubscribed = activeSpatialSubscription?.IsActive == true;
            Status = "Spatial subscription error: " + error.Message;
            Debug.LogException(error, this);
        }

        private void HandleVolleyInserted(EventContext _context, Volley volley)
        {
            TrackVolley(Connection, volley);
            NotifyVolleyChanged(volley);
        }

        private void HandleVolleyUpdated(EventContext _context, Volley _oldVolley, Volley volley)
        {
            TrackVolley(Connection, volley);
            NotifyVolleyChanged(volley);
        }

        private void HandleVolleyDeleted(EventContext _context, Volley volley)
        {
            VolleyLeftInterest?.Invoke(volley.VolleyId);
            if (relevantVolleys.Remove(volley.VolleyId))
            {
                RefreshFocusScope(Connection);
            }
        }

        private void TrackVolley(DbConnection connection, Volley volley)
        {
            var relevant = volley.IsActive &&
                (volley.SourceEntityId == subscribedPlayerEntityId ||
                 volley.TargetEntityId == subscribedPlayerEntityId ||
                 volley.SourceEntityId == selectedTargetEntityId ||
                 volley.TargetEntityId == selectedTargetEntityId);
            if (relevant)
            {
                relevantVolleys[volley.VolleyId] = new VolleyEndpoint(
                    volley.SourceEntityId,
                    volley.TargetEntityId);
            }
            else
            {
                relevantVolleys.Remove(volley.VolleyId);
            }

            RefreshFocusScope(connection);
        }

        private void RefreshFocusScope(DbConnection connection)
        {
            if (subscribedPlayerEntityId == 0)
            {
                return;
            }

            var targetIds = new HashSet<ulong>();
            AddFocusTarget(targetIds, selectedTargetEntityId);
            foreach (var endpoint in relevantVolleys.Values)
            {
                AddFocusTarget(targetIds, endpoint.SourceEntityId);
                AddFocusTarget(targetIds, endpoint.TargetEntityId);
            }

            var orderedTargets = targetIds.OrderBy(entityId => entityId).ToArray();
            var nextKey = string.Join(",", orderedTargets);
            if (string.Equals(focusKey, nextKey, StringComparison.Ordinal))
            {
                return;
            }

            focusKey = nextKey;
            var generation = focusGenerations.Begin();
            if (orderedTargets.Length == 0)
            {
                UnsubscribeIfActive(activeFocusSubscription);
                activeFocusSubscription = null;
                pendingFocusSubscription = null;
                return;
            }

            StartFocusSubscription(connection, generation, orderedTargets);
        }

        private void StartFocusSubscription(
            DbConnection connection,
            ulong generation,
            IReadOnlyCollection<ulong> targetIds)
        {
            var previous = activeFocusSubscription;
            SubscriptionHandle next = null;
            next = connection.SubscriptionBuilder()
                .OnApplied(_ => ApplyFocusSubscription(generation, next, previous))
                .OnError((_, error) => HandleFocusSubscriptionError(generation, error))
                .Subscribe(SeaSubscriptionPlan.Focus(
                    subscribedPlayerEntityId,
                    targetIds).ToArray());
            pendingFocusSubscription = next;
        }

        private void ApplyFocusSubscription(
            ulong generation,
            SubscriptionHandle next,
            SubscriptionHandle previous)
        {
            if (!focusGenerations.IsCurrent(generation))
            {
                UnsubscribeIfActive(next);
                return;
            }

            activeFocusSubscription = next;
            pendingFocusSubscription = null;
            if (previous != next)
            {
                UnsubscribeIfActive(previous);
            }
        }

        private void HandleFocusSubscriptionError(ulong generation, Exception error)
        {
            if (!focusGenerations.IsCurrent(generation))
            {
                return;
            }

            pendingFocusSubscription = null;
            Status = "Target subscription error: " + error.Message;
            Debug.LogException(error, this);
        }

        private void HandleShipDeleted(EventContext _context, Ship ship)
        {
            ShipLeftInterest?.Invoke(ship.EntityId);
            NotifyHudStateChanged();
            if (ship.EntityId == selectedTargetEntityId)
            {
                selectedTargetEntityId = 0;
                RefreshFocusScope(Connection);
            }
        }

        private void HandleWorldObjectDeleted(EventContext _context, WorldObject worldObject) =>
            WorldObjectLeftInterest?.Invoke(worldObject.EntityId);

        private void AddFocusTarget(ISet<ulong> targets, ulong entityId)
        {
            if (entityId != 0 && entityId != subscribedPlayerEntityId)
            {
                targets.Add(entityId);
            }
        }

        private static void UnsubscribeIfActive(SubscriptionHandle subscription)
        {
            if (subscription?.IsActive == true)
            {
                subscription.Unsubscribe();
            }
        }

        private void ResetInterestSubscriptions()
        {
            activeSpatialSubscription = null;
            pendingSpatialSubscription = null;
            activeFocusSubscription = null;
            pendingFocusSubscription = null;
            selectedTargetEntityId = 0;
            focusKey = string.Empty;
            relevantVolleys.Clear();
            spatialInterest.Reset();
            spatialGenerations.Reset();
            focusGenerations.Reset();
        }

        private readonly struct VolleyEndpoint
        {
            public VolleyEndpoint(ulong sourceEntityId, ulong targetEntityId)
            {
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
            }

            public ulong SourceEntityId { get; }
            public ulong TargetEntityId { get; }
        }
    }
}
