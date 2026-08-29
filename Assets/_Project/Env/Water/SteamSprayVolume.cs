using UnityEngine;

namespace GigaTower.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SteamSprayVolume : MonoBehaviour
    {
        public enum EffectMode { Steam, Spray }

        [Header("Look")]
        [SerializeField] private EffectMode mode = EffectMode.Steam;
        [SerializeField] private Texture2D particleTexture;
        [SerializeField] private Color color = new(0.85f, 0.95f, 1f, 0.55f);
        [SerializeField, Range(0f, 4f)] private float brightness = 1f;
        [SerializeField, Range(0f, 1f)] private float softEdge = 0.65f;
        [SerializeField, Range(-100, 100)] private int sortingOrder = 20;

        [Header("Emission")]
        [SerializeField, Min(0f)] private float density = 120f;
        [SerializeField] private Vector2 lifetime = new(1.5f, 3.5f);
        [SerializeField] private Vector2 particleSize = new(0.25f, 0.7f);
        [SerializeField, Range(0f, 1f)] private float fadeIn = 0.12f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float speed = 0.8f;
        [SerializeField, Min(0f)] private float spread = 0.35f;
        [SerializeField, Range(0f, 3f)] private float turbulence = 0.7f;
        [SerializeField, Range(-3f, 3f)] private float gravity = 0f;

        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private Material runtimeMaterial;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        private static readonly int SoftEdge = Shader.PropertyToID("_SoftEdge");

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void OnDestroy()
        {
            if (!runtimeMaterial) return;
            if (Application.isPlaying) Destroy(runtimeMaterial);
            else DestroyImmediate(runtimeMaterial);
        }

        [ContextMenu("Apply Settings")]
        public void Apply()
        {
            particles = GetComponent<ParticleSystem>();
            particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (!particles || !particleRenderer) return;

            var shader = Shader.Find("GigaTower/Particles/Steam Spray");
            if (!shader) return;

            if (!runtimeMaterial || runtimeMaterial.shader != shader)
            {
                if (runtimeMaterial)
                {
                    if (Application.isPlaying) Destroy(runtimeMaterial);
                    else DestroyImmediate(runtimeMaterial);
                }

                runtimeMaterial = new Material(shader) { name = "Steam Spray (Runtime)", hideFlags = HideFlags.HideAndDontSave };
                runtimeMaterial.renderQueue = 3100;
                particleRenderer.sharedMaterial = runtimeMaterial;
            }

            runtimeMaterial.SetTexture(BaseMap, particleTexture ? particleTexture : Texture2D.whiteTexture);
            runtimeMaterial.SetColor(Tint, color);
            runtimeMaterial.SetFloat(Brightness, brightness);
            runtimeMaterial.SetFloat(SoftEdge, softEdge);

            float minLifetime = Mathf.Max(0.05f, Mathf.Min(lifetime.x, lifetime.y));
            float maxLifetime = Mathf.Max(minLifetime, Mathf.Max(lifetime.x, lifetime.y));
            float minSize = Mathf.Max(0.01f, Mathf.Min(particleSize.x, particleSize.y));
            float maxSize = Mathf.Max(minSize, Mathf.Max(particleSize.x, particleSize.y));

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = 0f;
            main.startColor = Color.white;
            main.gravityModifier = mode == EffectMode.Spray ? gravity : 0f;
            main.maxParticles = Mathf.Max(64, Mathf.CeilToInt(density * maxLifetime * 1.5f));

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = density;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
            velocity.y = mode == EffectMode.Steam
                ? new ParticleSystem.MinMaxCurve(speed * 0.65f, speed)
                : new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-spread, spread);

            var noise = particles.noise;
            noise.enabled = turbulence > 0f;
            noise.strength = turbulence;
            noise.frequency = 0.45f;
            noise.scrollSpeed = speed * 0.25f;
            noise.damping = true;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = BuildAlphaGradient(fadeIn);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, mode == EffectMode.Steam
                ? new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1.35f))
                : new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 0.2f)));

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.sortingFudge = 1f;

            if (!particles.isPlaying) particles.Play();
        }

        private static Gradient BuildAlphaGradient(float fadeIn)
        {
            float edge = Mathf.Clamp(fadeIn, 0.001f, 0.49f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, edge), new GradientAlphaKey(1f, 1f - edge), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
