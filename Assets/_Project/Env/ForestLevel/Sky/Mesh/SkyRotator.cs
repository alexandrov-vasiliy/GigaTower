using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkyRotator : MonoBehaviour
{
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private bool useUnscaledTime;

    [SerializeField, Min(0.01f)] private float duration = 2f;
    [SerializeField] private Vector3 eulerPerLoop = new(0f, 360f, 0f);

    private Tween rotationTween;

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        rotationTween?.Kill();

        rotationTween = (useLocalRotation
                ? transform.DOLocalRotate(eulerPerLoop, duration, RotateMode.FastBeyond360)
                : transform.DORotate(eulerPerLoop, duration, RotateMode.FastBeyond360))
            .SetRelative()
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(useUnscaledTime);
    }

    public void Stop()
    {
        rotationTween?.Kill();
        rotationTween = null;
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
    }
}
