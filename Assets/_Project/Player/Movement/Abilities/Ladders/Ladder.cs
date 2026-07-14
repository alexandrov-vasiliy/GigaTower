using UnityEngine;

/// <summary>
/// Defines a climbable trigger volume and its local up/normal directions for LadderClimbing. ContainsHeight uses explicit endpoints when assigned or projects collider bounds otherwise; OnValidate forces the required Collider into trigger mode.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class Ladder : MonoBehaviour
{
    [SerializeField] private Transform bottomPoint;
    [SerializeField] private Transform topPoint;
    [SerializeField, Min(0f)] private float exitMargin = 0.1f;

    private Collider triggerVolume;

    public Vector3 Up => transform.up.normalized;
    public Vector3 Normal => transform.forward.normalized;

    private void Awake()
    {
        triggerVolume = GetComponent<Collider>();

        if (!triggerVolume.isTrigger)
            Debug.LogWarning("Ladder collider must be configured as a trigger.", this);
    }

    public bool ContainsHeight(Vector3 worldPosition)
    {
        GetHeightRange(out float bottomHeight, out float topHeight);
        float height = Vector3.Dot(worldPosition, Up);
        return height >= bottomHeight - exitMargin && height <= topHeight + exitMargin;
    }

    private void GetHeightRange(out float bottomHeight, out float topHeight)
    {
        if (bottomPoint != null && topPoint != null)
        {
            bottomHeight = Vector3.Dot(bottomPoint.position, Up);
            topHeight = Vector3.Dot(topPoint.position, Up);
        }
        else
        {
            Bounds bounds = triggerVolume.bounds;
            float centerHeight = Vector3.Dot(bounds.center, Up);
            Vector3 extents = bounds.extents;
            float projectedExtent =
                Mathf.Abs(Up.x) * extents.x +
                Mathf.Abs(Up.y) * extents.y +
                Mathf.Abs(Up.z) * extents.z;

            bottomHeight = centerHeight - projectedExtent;
            topHeight = centerHeight + projectedExtent;
        }

        if (bottomHeight > topHeight)
            (bottomHeight, topHeight) = (topHeight, bottomHeight);
    }

    private void OnValidate()
    {
        Collider ladderCollider = GetComponent<Collider>();
        if (ladderCollider != null)
            ladderCollider.isTrigger = true;
    }
}
