using UnityEngine;

/// <summary>
/// Scene spawn point used by Main to instantiate exactly one player prefab. Spawn falls back to this transform when no explicit point is assigned and returns the existing instance on repeated calls.
/// </summary>
public sealed class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private GameObject spawnedPlayer;

    public GameObject Spawn(GameObject playerPrefab)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Cannot spawn a player without a prefab.", this);
            return null;
        }

        if (spawnedPlayer != null)
        {
            Debug.LogWarning("A player has already been spawned by this PlayerSpawner.", this);
            return spawnedPlayer;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        spawnedPlayer = Instantiate(playerPrefab, point.position, point.rotation);
        return spawnedPlayer;
    }
}
