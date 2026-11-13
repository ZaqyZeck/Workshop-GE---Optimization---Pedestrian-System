using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Player player;

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
    }
    private void HandlePlayerRevive()
    {
        if (player != null) player.Revive();
    }
}
