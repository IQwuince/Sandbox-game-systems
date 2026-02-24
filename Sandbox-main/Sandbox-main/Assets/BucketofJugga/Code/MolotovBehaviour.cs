using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MolotovBehaviour : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask enemyLayer; // set to Enemy in inspector

    [Header("Explosion")]
    [SerializeField] private float radius = 3f;
    [SerializeField] private float directDamage = 15f; // applied once on explosion
    [SerializeField] private bool destroyOnExplode = true;

    [Header("Burn (Damage Over Time)")]
    [SerializeField] private float burnDuration = 4f;
    [SerializeField] private float burnDamagePerSecond = 5f;
    [SerializeField] private float burnTickRate = 10f; // ticks per second

    [Header("Optional VFX")]
    [SerializeField] private GameObject explosionVfxPrefab;

    private bool exploded;

    private void OnTriggerEnter(Collider other)
    {
        if (exploded) return;

        // explode if collide with Enemy layer
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Explode();
        }
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (explosionVfxPrefab != null)
            Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);

        // Find enemies in radius
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer, QueryTriggerInteraction.Collide);

        // Apply immediate damage + start DoT per enemy
        foreach (var hit in hits)
        {
            Variable healthVar = FindHealthVariable(hit.transform);
            if (healthVar == null) continue;

            if (directDamage != 0f)
                healthVar.ChangeValue(-Mathf.Abs(directDamage));

            if (burnDuration > 0f && burnDamagePerSecond > 0f)
                StartCoroutine(BurnCoroutine(healthVar, burnDuration, burnDamagePerSecond, burnTickRate));
        }

        if (destroyOnExplode)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private IEnumerator BurnCoroutine(Variable healthVar, float duration, float dps, float tickRate)
    {
        // tickRate safety
        float interval = (tickRate <= 0f) ? 0.1f : (1f / tickRate);
        float elapsed = 0f;

        // apply damage in small steps so it feels consistent.
        while (elapsed < duration)
        {
            if (healthVar == null) yield break; // slime likely destroyed

            float damageThisTick = dps * interval;
            healthVar.ChangeValue(-Mathf.Abs(damageThisTick));

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    private Variable FindHealthVariable(Transform hit)
    {
        // search in parents/children around the hit collider.

        // 1) try in root (common case)
        Transform root = hit.root;

        // include inactive in case health object is disabled:
        Variable[] vars = root.GetComponentsInChildren<Variable>(true);
        foreach (var v in vars)
        {
            if (v != null && v.gameObject.name == "health")
                return v;
        }

        // 2) fallback: also try from the hit object
        vars = hit.GetComponentsInChildren<Variable>(true);
        foreach (var v in vars)
        {
            if (v != null && v.gameObject.name == "health")
                return v;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, radius);
    }
#endif
}