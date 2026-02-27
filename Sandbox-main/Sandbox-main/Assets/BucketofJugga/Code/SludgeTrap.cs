using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SludgeTrap : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Impact Damage (one time)")]
    [SerializeField] private float impactDamage = 40f;

    [Header("Knockback")]
    [SerializeField] private float knockbackImpulse = 16f;
    [SerializeField] private float upwardLiftImpulse = 10f;
    [SerializeField] private bool zeroVelocityBeforeKnockback = true;

    [Tooltip("Seconds to let physics control the slime before NavMeshAgent resumes.")]
    [SerializeField] private float knockbackControlTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly HashSet<int> alreadyHitInstanceIds = new HashSet<int>();

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;

        SlimeDefeat slime = other.GetComponentInParent<SlimeDefeat>();
        if (slime == null) return;

        int slimeId = slime.GetInstanceID();
        if (!alreadyHitInstanceIds.Add(slimeId))
            return;

        // Cache components before possibly slime dying from damage
        Transform slimeTransform = slime.transform;
        Variable healthVar = FindHealthVariable(slimeTransform);
        Rigidbody rb = slime.GetComponentInParent<Rigidbody>();
        NavMeshAgent agent = slime.GetComponentInParent<NavMeshAgent>();

        // Damage
        if (healthVar != null && impactDamage > 0f)
            healthVar.ChangeValue(-Mathf.Abs(impactDamage));

        if (slime == null || slimeTransform == null) 
            return;

        if (rb == null) 
            return;

        Vector3 dir = RandomHorizontalDirection();
        Vector3 impulse = dir * knockbackImpulse + Vector3.up * upwardLiftImpulse;

        if (zeroVelocityBeforeKnockback)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // prevent agent from cancelling y movement
        if (agent != null && knockbackControlTime > 0f)
            StartCoroutine(KnockbackWindow(agent, knockbackControlTime));

        rb.AddForce(impulse, ForceMode.Impulse);

        if (debugLogs)
            Debug.Log($"[SludgeTrap] Impulse applied to '{slime.name}': {impulse} (dir={dir})");
    }

    private IEnumerator KnockbackWindow(NavMeshAgent agent, float time)
    {
        //save old settings
        bool oldUpdatePos = agent.updatePosition;
        bool oldUpdateRot = agent.updateRotation;
        bool oldIsStopped = agent.isStopped;

        //stop agent from overriding transform while physics acts
        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        yield return new WaitForSeconds(time);

        // Re-enable agent control
        agent.updatePosition = oldUpdatePos;
        agent.updateRotation = oldUpdateRot;
        agent.isStopped = oldIsStopped;

        // Re-sync agent to the new transform position after physics moved it
        agent.nextPosition = agent.transform.position;
    }

    private Vector3 RandomHorizontalDirection()
    {
        Vector2 v = Random.insideUnitCircle;
        if (v.sqrMagnitude < 0.0001f) v = Vector2.right;
        v.Normalize();
        return new Vector3(v.x, 0f, v.y);
    }

    private Variable FindHealthVariable(Transform slimeTransform)
    {
        Variable[] vars = slimeTransform.GetComponentsInChildren<Variable>(true);
        foreach (var v in vars)
        {
            if (v != null && v.gameObject.name == "Health")
                return v;
        }
        return null;
    }
}