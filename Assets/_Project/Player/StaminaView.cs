using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DOTween-driven UI presenter for PlayerStamina. It animates the required Slider value and CanvasGroup visibility after stamina changes, schedules delayed hiding, and kills all owned tweens when disabled.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Slider))]
public class StaminaView : MonoBehaviour
{
    [SerializeField] private PlayerStamina _playerStamina;
    [SerializeField, Min(0f)] private float _hideDelay = 1.5f;
    [SerializeField, Min(0f)] private float _animationDuration = 0.25f;
    [SerializeField, Min(0f)] private float _valueAnimationDuration = 0.15f;
    [SerializeField] private float _hiddenYOffset = -40f;

    private Slider _slider;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector2 _shownPosition;
    private Vector2 _hiddenPosition;
    private float _previousStamina;
    private bool _isShown;
    private Tween _valueTween;
    private Tween _hideDelayTween;
    private Sequence _visibilityTween;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = (RectTransform)transform;

        _shownPosition = _rectTransform.anchoredPosition;
        _hiddenPosition = _shownPosition + Vector2.up * _hiddenYOffset;
    }

    private void OnEnable()
    {
        if (_playerStamina == null)
            return;

        _previousStamina = _playerStamina.CurrentStamina;
        _slider.value = _playerStamina.Normalized;

        if (_playerStamina.CurrentStamina < _playerStamina.MaxStamina)
            Show(immediate: true);
        else
            Hide(immediate: true);

        _playerStamina.Changed += OnStaminaChanged;
    }

    private void OnDisable()
    {
        if (_playerStamina != null)
            _playerStamina.Changed -= OnStaminaChanged;

        KillTweens();
    }

    private void OnStaminaChanged(float currentStamina, float maxStamina)
    {
        float normalized = maxStamina > 0f ? currentStamina / maxStamina : 0f;
        AnimateSlider(normalized);
        
        Show(immediate: false);
        ScheduleHide();

        _previousStamina = currentStamina;
    }

    private void AnimateSlider(float value)
    {
        _valueTween?.Kill();
        _valueTween = _slider.DOValue(value, _valueAnimationDuration).SetTarget(this);
    }

    private void Show(bool immediate)
    {
        _hideDelayTween?.Kill();

        if (_isShown && !immediate)
            return;

        _isShown = true;
        PlayVisibilityTween(_shownPosition, 1f, immediate, Ease.OutCubic);
    }

    private void ScheduleHide()
    {
        _hideDelayTween?.Kill();

        if (_hideDelay <= 0f)
        {
            Hide(immediate: false);
            return;
        }

        _hideDelayTween = DOVirtual.DelayedCall(_hideDelay, () => Hide(immediate: false)).SetTarget(this);
    }

    private void Hide(bool immediate)
    {
        _hideDelayTween?.Kill();

        if (!_isShown && !immediate)
            return;

        _isShown = false;
        PlayVisibilityTween(_hiddenPosition, 0f, immediate, Ease.InCubic);
    }

    private void PlayVisibilityTween(Vector2 targetPosition, float targetAlpha, bool immediate, Ease ease)
    {
        _visibilityTween?.Kill();

        if (immediate || _animationDuration <= 0f)
        {
            _rectTransform.anchoredPosition = targetPosition;
            _canvasGroup.alpha = targetAlpha;
            return;
        }

        _visibilityTween = DOTween.Sequence()
            .SetTarget(this)
            .SetEase(ease)
            .Join(_rectTransform.DOAnchorPos(targetPosition, _animationDuration))
            .Join(_canvasGroup.DOFade(targetAlpha, _animationDuration));
    }

    private void KillTweens()
    {
        _valueTween?.Kill();
        _hideDelayTween?.Kill();
        _visibilityTween?.Kill();
    }
}