using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private PlayerBridge playerBridge;

    private void OnEnable()
    {
        GameEvent.OnPlayerDie += HandlePlayerDie;
        GameEvent.OnPlayerRevive += HandlePlayerRevive;
    }

    private void OnDisable()
    {
        GameEvent.OnPlayerDie -= HandlePlayerDie;
        GameEvent.OnPlayerRevive -= HandlePlayerRevive;
    }

    private void HandlePlayerDie()
    {
        if (player != null) player.Die();
        if (playerBridge != null) playerBridge.Die();
    }

    private void HandlePlayerRevive()
    {
        if (player != null) player.Revive();
        if (playerBridge != null) playerBridge.Revive();
    }
}
