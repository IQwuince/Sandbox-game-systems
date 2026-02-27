using UnityEngine;

public class SlowdownTrap : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Slow Effect")]
    [Tooltip("0.7 = 30% slow.")]
    [SerializeField] private float slowMultiplier = 0.7f;

    [Tooltip("Seconds.")]
    [SerializeField] private float slowDuration = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;


        SlimeEffectManager effects = other.GetComponent<SlimeEffectManager>();
        if (effects == null)
        {
            if (debugLogs) Debug.LogWarning($"[SlowdownTrap] SlimeEffectManager missing on '{other.name}'.");
            return;
        }

        effects.ApplySlow(slowMultiplier, slowDuration);

        if (debugLogs)
            Debug.Log($"[SlowdownTrap] Applied slow x{slowMultiplier} for {slowDuration}s to '{other.name}'.");
    }
}