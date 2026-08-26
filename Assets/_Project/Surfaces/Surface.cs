using UnityEngine;

/// <summary>Identifies the semantic surface represented by this collider or its children.</summary>
public sealed class Surface : MonoBehaviour
{
    [field: SerializeField] public SurfaceType Type { get; private set; } = SurfaceType.Earth;
}
