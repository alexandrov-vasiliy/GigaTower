using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Light))]
public sealed class RomanTorchLight : MonoBehaviour
{
    [SerializeField, Range(2, 12), Tooltip("Number of brightness intervals in this torch's light.")]
    private int steps = 4;

    [SerializeField, Range(32, 720), Tooltip("Virtual screen height of the light grid. Lower values make larger light pixels.")]
    private int pixelHeight = 163;

    [SerializeField, Range(0f, 1f), Tooltip("0 restores the normal point light; 1 applies fully stepped lighting.")]
    private float strength = 1f;

    private static readonly int PositionRange = Shader.PropertyToID("_RomanTorchPositionRange");
    private static readonly int Settings = Shader.PropertyToID("_RomanTorchSettings");
    private Light torch;

    private void OnEnable()
    {
        torch = GetComponent<Light>();
        RenderPipelineManager.beginCameraRendering += BindLight;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BindLight;
        Shader.SetGlobalVector(Settings, Vector4.zero);
    }

    private void BindLight(ScriptableRenderContext context, Camera camera)
    {
        bool sameScene = camera.gameObject.scene == gameObject.scene ||
            (camera.cameraType == CameraType.SceneView && SceneManager.GetActiveScene() == gameObject.scene);
        bool active = sameScene && torch != null && torch.isActiveAndEnabled && torch.type == LightType.Point;
        Vector3 position = transform.position;
        Shader.SetGlobalVector(PositionRange, new Vector4(position.x, position.y, position.z,
            torch != null ? Mathf.Max(torch.range, 0.001f) : 1f));
        Shader.SetGlobalVector(Settings, new Vector4(Mathf.Clamp(steps, 2, 12),
            Mathf.Clamp(pixelHeight, 32, 720), Mathf.Clamp01(strength), active ? 1f : 0f));
    }
}
