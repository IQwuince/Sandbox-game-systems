using System.Collections;
using UnityEngine;

/// Put this on the SLIME ROOT.
/// Requires that a Variable component exists on a child GameObject named "Health"
/// (can assign it manually in inspector).
public class SlimeEffectManager : MonoBehaviour
{
    [Header("Health Variable")]
    [Tooltip("assign directly. If empty, will auto-find a Variable child named 'Health'.")]
    [SerializeField] private Variable healthVar;

    [Header("Burn Stacking")]
    [Tooltip("Maximum burn DPS after stacking multiple sources.")]
    [SerializeField] private float maxBurnDps = 25f;

    [Tooltip("If true, each new burn adds to current DPS (up to maxBurnDps). If false, it replaces DPS if stronger.")]
    [SerializeField] private bool burnStacksAdditively = true;

    // ---- Burn runtime state ----
    private Coroutine burnRoutine;
    private float burnTimeRemaining;
    private float burnDps;

    // ---- Slow runtime state ----
    private Coroutine slowRoutine;
    private float slowTimeRemaining;
    private float slowMultiplier = 1f;

    private void Awake()
    {
        ResolveHealthVar();
    }

    private void ResolveHealthVar()
    {
        if (healthVar != null) return;

        Variable[] vars = GetComponentsInChildren<Variable>(true);
        for (int i = 0; i < vars.Length; i++)
        {
            var v = vars[i];
            if (v == null) continue;

            string n = v.gameObject.name;
            
            if (n == "Health")
            {
                healthVar = v;
                return;
            }
            
        }

        Debug.LogWarning($"[SlimeEffectManager] No Variable named 'Health' found under '{name}'. Burn/DoT damage will do nothing.");
    }

    // ---------------- BURN ----------------

    /// Applies burn. Multiple calls can stack/refresh depending on settings.
    public void ApplyBurn(float dps, float duration)
    {
        if (duration <= 0f || dps <= 0f) return;

        ResolveHealthVar();

        // duration: refresh/extend (choose behavior)
        // refresh to the max remaining (so repeated hits keep it burning).
        burnTimeRemaining = Mathf.Max(burnTimeRemaining, duration);

        // intensity: stack or replace
        if (burnStacksAdditively)
            burnDps = Mathf.Min(maxBurnDps, burnDps + dps);
        else
            burnDps = Mathf.Min(maxBurnDps, Mathf.Max(burnDps, dps));

        if (burnRoutine == null)
            burnRoutine = StartCoroutine(BurnCo());
    }

    private IEnumerator BurnCo()
    {
        while (burnTimeRemaining > 0f)
        {
            // If slime is dying/destroyed, healthVar may be null (or become null)
            if (healthVar != null)
            {
                float dmg = burnDps * Time.deltaTime;
                healthVar.ChangeValue(-Mathf.Abs(dmg));
            }

            burnTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // reset
        burnRoutine = null;
        burnDps = 0f;
        burnTimeRemaining = 0f;
    }

    // ---------------- SLOW ----------------
    // slowdown trap.

    /// Applies a slow multiplier for duration. Example: multiplier=0.5 => 50% speed.
    public void ApplySlow(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        if (duration <= 0f) return;

        // strongest slow wins (smallest multiplier), and refresh duration.
        slowMultiplier = Mathf.Min(slowMultiplier, multiplier);
        slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);

        // TODO: Call into movement script here:

        if (slowRoutine == null)
            slowRoutine = StartCoroutine(SlowCo());
    }

    private IEnumerator SlowCo()
    {
        while (slowTimeRemaining > 0f)
        {
            slowTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // reset to normal
        slowRoutine = null;
        slowMultiplier = 1f;

        // TODO: slimeAgent.SetSpeedMultiplier(1f);
    }

    // expose states
    public float CurrentBurnDps => burnDps;
    public float BurnTimeRemaining => burnTimeRemaining;
    public float CurrentSlowMultiplier => slowMultiplier;
    public float SlowTimeRemaining => slowTimeRemaining;
}