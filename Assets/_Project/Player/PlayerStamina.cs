using System;
using UnityEngine;

/// <summary>
/// Owns the player stamina resource used by movement and melee combat. Its public API supports availability checks, generic spending, sprint drain, and jump spending; Update handles delayed regeneration and exhaustion recovery, while Changed, Emptied, and Recovered notify presentation or gameplay listeners.
/// </summary>
public sealed class PlayerStamina : MonoBehaviour
{
    [SerializeField, Min(0f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 15f;
    [SerializeField, Min(0f)] private float jumpCost = 15f;
    [SerializeField, Min(0f)] private float regenPerSecond = 20f;
    [SerializeField, Min(0f)] private float regenDelayAfterSpend = 1f;
    [SerializeField, Range(0f, 1f)] private float sprintRecoveryRatio = 0.2f;

    private float currentStamina;
    private float lastSpendTime = float.NegativeInfinity;
    private bool sprintLockedByExhaustion;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float Normalized => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool IsEmpty => currentStamina <= 0f;
    public bool CanSprint => !sprintLockedByExhaustion && currentStamina > 0f;

    public event Action<float, float> Changed;
    public event Action Emptied;
    public event Action Recovered;

    private void Awake()
    {
        currentStamina = maxStamina;
        sprintLockedByExhaustion = false;
        RaiseChanged();
    }

    private void Update()
    {
        if (currentStamina >= maxStamina || Time.time < lastSpendTime + regenDelayAfterSpend)
            return;

        SetCurrentStamina(currentStamina + regenPerSecond * Time.deltaTime);

        if (sprintLockedByExhaustion && currentStamina >= GetSprintRecoveryThreshold())
        {
            sprintLockedByExhaustion = false;
            Recovered?.Invoke();
        }
    }

    public bool Has(float amount)
    {
        return amount <= 0f || currentStamina >= amount;
    }

    public bool TrySpend(float amount)
    {
        if (!Has(amount))
            return false;

        Spend(amount);
        return true;
    }

    public bool TryDrainSprint(float deltaTime)
    {
        if (!CanSprint || deltaTime <= 0f)
            return false;

        Spend(Mathf.Min(currentStamina, sprintDrainPerSecond * deltaTime));
        return true;
    }

    public bool TrySpendJump()
    {
        return TrySpend(jumpCost);
    }

    private void Spend(float amount)
    {
        if (amount <= 0f)
            return;

        lastSpendTime = Time.time;
        SetCurrentStamina(currentStamina - amount);

        if (currentStamina > 0f || sprintLockedByExhaustion)
            return;

        sprintLockedByExhaustion = true;
        Emptied?.Invoke();
    }

    private void SetCurrentStamina(float value)
    {
        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(value, 0f, maxStamina);

        if (!Mathf.Approximately(previousStamina, currentStamina))
            RaiseChanged();
    }

    private float GetSprintRecoveryThreshold()
    {
        return maxStamina * sprintRecoveryRatio;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(currentStamina, maxStamina);
    }

    private void OnValidate()
    {
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }
}
