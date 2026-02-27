using System.Collections.Generic;
using UnityEngine;

public class ToxicTrap : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Slow (stronger than slowdown trap)")]
    [SerializeField] private float slowMultiplier = 0.6f;

    [SerializeField] private float slowDuration = 1.0f;

    [Header("Burn (while on trap)")]
    [SerializeField] private float burnDamagePerInterval = 5f;
    [SerializeField] private float burnDuration = 1.0f;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Track which slimes are inside, to refresh them on a timer
    private readonly HashSet<SlimeEffectManager> inside = new HashSet<SlimeEffectManager>();
    private float nextRefreshTime;

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;

        SlimeDefeat slime = other.GetComponentInParent<SlimeDefeat>();
        if (slime == null) return;

        SlimeEffectManager effects = slime.GetComponent<SlimeEffectManager>();
        if (effects == null) return;

        inside.Add(effects);

        ApplyEffects(effects);

        if (debugLogs)
            Debug.Log($"[ToxicTrap] '{slime.name}' entered. insideCount={inside.Count}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;

        SlimeDefeat slime = other.GetComponentInParent<SlimeDefeat>();
        if (slime == null) return;

        SlimeEffectManager effects = slime.GetComponent<SlimeEffectManager>();
        if (effects == null) return;

        inside.Remove(effects);

        if (debugLogs)
            Debug.Log($"[ToxicTrap] '{slime.name}' exited. insideCount={inside.Count}");
    }

    private void Update()
    {
        if (inside.Count == 0) return;

        if (Time.time < nextRefreshTime) return;

        nextRefreshTime = Time.time + Mathf.Max(0.01f, refreshInterval);

        // clean up slimes that died while inside
        inside.RemoveWhere(e => e == null);

        foreach (var effects in inside)
        {
            ApplyEffects(effects);
        }
    }

    private void ApplyEffects(SlimeEffectManager effects)
    {
        // Refresh slow + burn while inside
        effects.ApplySlow(slowMultiplier, slowDuration);
        effects.ApplyBurn(burnDamagePerInterval, burnDuration);
    }
}
