/// <summary>
/// Shared option for movement abilities that can spend PlayerStamina but should remain reusable on prefabs without a stamina resource.
/// AllowForFree keeps the ability active without costs, while DisableAbility makes the missing resource intentionally block the ability.
/// </summary>
public enum MissingStaminaPolicy
{
    AllowForFree,
    DisableAbility
}
