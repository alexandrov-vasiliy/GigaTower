
using UnityEngine;

[ExecuteAlways]
public class SkyRiftController : MonoBehaviour
{
    [Header("Sky Material")]
    [SerializeField] private Material skyMaterial;

    [Header("Rift")]
    [SerializeField, Min(0.01f)]
    private float radiusMultiplier = 1f;

    [Header("Noise")]
    [SerializeField, Min(0f)]
    private float noiseScale = 0.25f;

    [SerializeField, Min(0f)]
    private float noiseStrength = 1.2f;

    [SerializeField, Min(0f)] private float noiseSpeed = 1f;

    private static readonly int RiftCenter =
        Shader.PropertyToID("_RiftCenter");

    private static readonly int RiftRadius =
        Shader.PropertyToID("_RiftRadius");

    private static readonly int NoiseScale =
        Shader.PropertyToID("_NoiseScale");

    private static readonly int NoiseStrength =
        Shader.PropertyToID("_NoiseStrength");
    
    
    private static readonly int NoiseSpeed =
        Shader.PropertyToID("_RiftNoiseSpeed");

    private void OnEnable() => UpdateRift();
    private void OnValidate() => UpdateRift();
    private void LateUpdate() => UpdateRift();

    private void UpdateRift()
    {
        if (skyMaterial == null)
            return;

        // Центр разрыва
        skyMaterial.SetVector(
            RiftCenter,
            transform.position
        );

        // Стандартный Unity Cylinder имеет диаметр 1.
        // Используем масштаб X/Z для радиуса.
        Vector3 scale = transform.lossyScale;

        float radius = Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.z)
        ) * 0.5f * radiusMultiplier;

        skyMaterial.SetFloat(RiftRadius, radius);
        skyMaterial.SetFloat(NoiseScale, noiseScale);
        skyMaterial.SetFloat(NoiseStrength, noiseStrength);
        skyMaterial.SetFloat(NoiseSpeed, noiseSpeed);
    }
}
