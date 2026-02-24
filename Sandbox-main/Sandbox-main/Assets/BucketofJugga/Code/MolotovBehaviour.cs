using UnityEngine;

public class MolotovBehaviour : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask enemyLayer; // set to Enemy in inspector

    [Header("Explosion")]
    [SerializeField] private bool useAoe = true;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float directDamage = 15f;
    [SerializeField] private bool destroyOnExplode = true;

    [Header("Burn (Damage Over Time)")]
    [SerializeField] private float burnDuration = 4f;
    [SerializeField] private float burnDamagePerSecond = 5f;

    [Header("Optional VFX")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool exploded;

    private void OnTriggerEnter(Collider other)
    {
        if (exploded) return;

        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;

        Explode(other);
    }

    private void Explode(Collider triggerHit)
    {
        if (exploded) return;
        exploded = true;

        if (debugLogs)
            Debug.Log($"[MolotovBehaviour] Explode triggered by '{triggerHit.name}' at {transform.position}");

        if (explosionVfxPrefab != null)
            Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);

        if (!useAoe)
        {
            ApplyEffectsToHit(triggerHit);
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, enemyLayer, QueryTriggerInteraction.Collide);

            if (debugLogs)
                Debug.Log($"[MolotovBehaviour] OverlapSphere hits={hits.Length} radius={radius}");

            // apply per-SLIME 
            // track already-processed slimes.
            System.Collections.Generic.HashSet<SlimeDefeat> processed = new System.Collections.Generic.HashSet<SlimeDefeat>();

            foreach (var hit in hits)
            {
                var slimeDefeat = hit.GetComponentInParent<SlimeDefeat>();
                if (slimeDefeat == null) continue;

                if (!processed.Add(slimeDefeat)) continue; // already applied to this slime

                ApplyEffectsToSlime(slimeDefeat.transform, hit.name);
            }
        }

        if (destroyOnExplode) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    private void ApplyEffectsToHit(Collider hit)
    {
        var slimeDefeat = hit.GetComponentInParent<SlimeDefeat>();
        if (slimeDefeat == null)
        {
            if (debugLogs) Debug.LogWarning($"[MolotovBehaviour] Trigger hit '{hit.name}' had Enemy layer but no SlimeDefeat in parents.");
            return;
        }

        ApplyEffectsToSlime(slimeDefeat.transform, hit.name);
    }

    private void ApplyEffectsToSlime(Transform slimeTransform, string viaColliderName)
    {
        if (debugLogs)
            Debug.Log($"[MolotovBehaviour] Applying effects to slime '{slimeTransform.name}' (via collider '{viaColliderName}').");

        // Direct damage
        Variable healthVar = FindHealthVariable(slimeTransform);
        if (healthVar != null && directDamage != 0f)
        {
            float before = healthVar.value;
            healthVar.ChangeValue(-Mathf.Abs(directDamage));
            if (debugLogs)
                Debug.Log($"[MolotovBehaviour] DirectDamage {directDamage}: health {before} -> {healthVar.value} on '{slimeTransform.name}'");
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[MolotovBehaviour] No Variable named 'Health' found under slime '{slimeTransform.name}'.");
        }

        // Burn
        if (burnDuration > 0f && burnDamagePerSecond > 0f)
        {
            SlimeEffectManager effects = slimeTransform.GetComponent<SlimeEffectManager>();
            if (effects != null)
            {
                effects.ApplyBurn(burnDamagePerSecond, burnDuration);
                if (debugLogs)
                    Debug.Log($"[MolotovBehaviour] Burn applied to '{slimeTransform.name}': {burnDamagePerSecond} dps for {burnDuration}s");
            }
            else if (debugLogs)
            {
                Debug.LogWarning($"[MolotovBehaviour] SlimeEffectManager missing on '{slimeTransform.name}'. (Also ensure file is named SlimeEffectManager.cs)");
            }
        }
    }

    private Variable FindHealthVariable(Transform slimeTransform)
    {
        // Search within THIS slime's hierarchy only
        Variable[] vars = slimeTransform.GetComponentsInChildren<Variable>(true);
        foreach (var v in vars)
        {
            if (v != null && v.gameObject.name == "Health")
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