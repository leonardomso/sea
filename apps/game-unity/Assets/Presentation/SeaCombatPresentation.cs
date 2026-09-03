using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaCombatVisualPool
    {
        private readonly SeaBoundedPool<GameObject> pool;

        public SeaCombatVisualPool(
            Func<GameObject> visualFactory,
            int maximumCapacity = 512)
        {
            pool = new SeaBoundedPool<GameObject>(
                visualFactory ?? throw new ArgumentNullException(nameof(visualFactory)),
                Reset,
                initialCapacity: 0,
                maximumCapacity);
        }

        public int CreatedCount => pool.CreatedCount;

        public GameObject Acquire()
        {
            if (!pool.TryAcquire(out var visual))
            {
                throw new InvalidOperationException("The combat presentation pool reached its limit.");
            }

            visual.SetActive(true);
            Cache(visual).PrepareForAcquire();

            return visual;
        }

        public void Release(GameObject visual) => pool.Release(visual);

        private static void Reset(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Cache(visual).PrepareForRelease();
            visual.SetActive(false);
        }

        private static SeaCombatVisualCache Cache(GameObject visual)
        {
            var cache = visual.GetComponent<SeaCombatVisualCache>();
            if (cache == null)
            {
                cache = visual.AddComponent<SeaCombatVisualCache>();
                cache.Capture();
            }

            return cache;
        }
    }

    public sealed class SeaCombatVisualCache : MonoBehaviour
    {
        private TrailRenderer[] trails = Array.Empty<TrailRenderer>();
        private ParticleSystem particles;
        private AudioSource audioSource;

        public ParticleSystem Particles => particles;

        public AudioSource AudioSource => audioSource;

        public void Capture()
        {
            trails = GetComponentsInChildren<TrailRenderer>(true);
            particles = GetComponent<ParticleSystem>();
            audioSource = GetComponent<AudioSource>();
        }

        public void PrepareForAcquire()
        {
            foreach (var trail in trails)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        public void PrepareForRelease()
        {
            foreach (var trail in trails)
            {
                trail.emitting = false;
                trail.Clear();
            }

            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
    }

    public static class SeaCombatVisualFactory
    {
        public static GameObject CreateVolley(Material material)
        {
            var root = new GameObject("Pooled Cannon Volley");
            for (var index = 0; index < 5; index++)
            {
                var cannonball = SeaPrimitive.Create(PrimitiveType.Sphere, $"Cannonball {index + 1}", material);
                cannonball.transform.SetParent(root.transform, false);
                cannonball.transform.localPosition = new Vector3(
                    (index - 2) * 0.18f,
                    (index % 2) * 0.10f,
                    -index * 0.16f);
                cannonball.transform.localScale = Vector3.one * 0.24f;

                var trail = cannonball.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = 0.35f;
                trail.minVertexDistance = 0.08f;
                trail.startWidth = 0.12f;
                trail.endWidth = 0.015f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.emitting = false;
            }

            var cache = root.AddComponent<SeaCombatVisualCache>();
            cache.Capture();
            return root;
        }

        public static GameObject CreateEffect(string name, Material material)
        {
            var effect = new GameObject($"Pooled {name} Effect");
            var particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.6f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

            var audio = effect.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.85f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 85f;
            var cache = effect.AddComponent<SeaCombatVisualCache>();
            cache.Capture();
            return effect;
        }
    }

    public sealed class SeaCombatPresenter
    {
        private const float EffectLifetimeSeconds = 0.8f;

        private readonly SeaCombatVisualPool volleyPool;
        private readonly SeaCombatVisualPool effectPool;
        private readonly Dictionary<ulong, ActiveVolley> active = new();
        private readonly List<ulong> stale = new();
        private readonly List<TimedEffect> effects = new();
        private readonly AudioClip cannonClip;
        private readonly AudioClip impactClip;
        private int frame;

        public SeaCombatPresenter(Material cannonballMaterial, Material effectMaterial)
        {
            volleyPool = new SeaCombatVisualPool(
                () => SeaCombatVisualFactory.CreateVolley(cannonballMaterial));
            effectPool = new SeaCombatVisualPool(
                () => SeaCombatVisualFactory.CreateEffect("Combat", effectMaterial));
            cannonClip = SeaCombatAudioFactory.CreateCannonClip();
            impactClip = SeaCombatAudioFactory.CreateImpactClip();
        }

        public void BeginFrame()
        {
            frame++;
            for (var index = effects.Count - 1; index >= 0; index--)
            {
                if (Time.unscaledTime < effects[index].ReleaseAt)
                {
                    continue;
                }

                effectPool.Release(effects[index].Visual);
                effects.RemoveAt(index);
            }
        }

        public void Show(
            Volley volley,
            ulong currentTick,
            Transform source,
            Transform target,
            SeaShipFeedback sourceFeedback)
        {
            if (!active.TryGetValue(volley.VolleyId, out var presentation))
            {
                var visual = volleyPool.Acquire();
                var origin = source != null
                    ? source.TransformPoint(SeaVolleyPresentationRules.LocalSideOffset(volley.Side, 2.6f) + Vector3.up * 0.7f)
                    : new Vector3(volley.OriginX, SeaWorldView.WaterSurfaceHeight + 0.8f, volley.OriginY);
                presentation = new ActiveVolley(visual, origin, target, frame);
                active.Add(volley.VolleyId, presentation);
                sourceFeedback?.PlayBroadside(volley.Side);
                PlayEffect(origin, cannonClip, new Color(0.28f, 0.24f, 0.20f, 1f));
            }

            var destination = target != null
                ? target.position + Vector3.up * 0.55f
                : presentation.LastPosition;
            var progress = SeaVolleyPresentationRules.Progress(
                volley.FiredAtTick,
                volley.ImpactAtTick,
                currentTick);
            var position = Vector3.Lerp(presentation.Origin, destination, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 1.7f;
            presentation.Visual.transform.position = position;
            if (destination != position)
            {
                presentation.Visual.transform.rotation = Quaternion.LookRotation(destination - position);
            }

            active[volley.VolleyId] = presentation.WithFrame(frame, position, target);
        }

        public void EndFrame()
        {
            stale.Clear();
            foreach (var entry in active)
            {
                if (entry.Value.LastSeenFrame != frame)
                {
                    stale.Add(entry.Key);
                }
            }

            foreach (var volleyId in stale)
            {
                var presentation = active[volleyId];
                var impactPosition = presentation.Target != null
                    ? presentation.Target.position + Vector3.up * 0.15f
                    : presentation.LastPosition;
                PlayEffect(impactPosition, impactClip, new Color(0.72f, 0.91f, 1f, 1f));
                volleyPool.Release(presentation.Visual);
                active.Remove(volleyId);
            }
        }

        public void Reset()
        {
            foreach (var presentation in active.Values)
            {
                volleyPool.Release(presentation.Visual);
            }

            active.Clear();
            foreach (var effect in effects)
            {
                effectPool.Release(effect.Visual);
            }

            effects.Clear();
            stale.Clear();
        }

        private void PlayEffect(Vector3 position, AudioClip clip, Color color)
        {
            var visual = effectPool.Acquire();
            visual.transform.position = position;
            var cache = visual.GetComponent<SeaCombatVisualCache>();
            var particles = cache.Particles;
            var main = particles.main;
            main.startColor = color;
            particles.Play(true);
            var audio = cache.AudioSource;
            audio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            audio.PlayOneShot(clip, 0.7f);
            effects.Add(new TimedEffect(visual, Time.unscaledTime + EffectLifetimeSeconds));
        }

        private readonly struct ActiveVolley
        {
            public ActiveVolley(
                GameObject visual,
                Vector3 origin,
                Transform target,
                int lastSeenFrame,
                Vector3 lastPosition = default)
            {
                Visual = visual;
                Origin = origin;
                Target = target;
                LastSeenFrame = lastSeenFrame;
                LastPosition = lastPosition == default ? origin : lastPosition;
            }

            public GameObject Visual { get; }
            public Vector3 Origin { get; }
            public Transform Target { get; }
            public int LastSeenFrame { get; }
            public Vector3 LastPosition { get; }

            public ActiveVolley WithFrame(int value, Vector3 position, Transform target) =>
                new(Visual, Origin, target ?? Target, value, position);
        }

        private readonly struct TimedEffect
        {
            public TimedEffect(GameObject visual, float releaseAt)
            {
                Visual = visual;
                ReleaseAt = releaseAt;
            }

            public GameObject Visual { get; }
            public float ReleaseAt { get; }
        }
    }

    public static class SeaCombatAudioFactory
    {
        private const int SampleRate = 22050;

        public static AudioClip CreateCannonClip() => CreateClip("Cannon Report", 0.32f, false);

        public static AudioClip CreateImpactClip() => CreateClip("Water Impact", 0.24f, true);

        private static AudioClip CreateClip(string name, float duration, bool bright)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[sampleCount];
            var seed = bright ? 173 : 91;
            for (var index = 0; index < sampleCount; index++)
            {
                seed = unchecked(seed * 1103515245 + 12345);
                var noise = ((seed >> 16) & 0x7fff) / 16384f - 1f;
                var time = (float)index / SampleRate;
                var envelope = Mathf.Exp(-time * (bright ? 18f : 10f));
                var tone = Mathf.Sin(time * Mathf.PI * 2f * (bright ? 180f : 72f));
                samples[index] = (noise * 0.65f + tone * 0.35f) * envelope * 0.6f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
