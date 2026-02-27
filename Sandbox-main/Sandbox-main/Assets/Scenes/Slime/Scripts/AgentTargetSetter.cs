using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentTargetSetter : MonoBehaviour
{
    public string targetName;
    private Transform target;

    private NavMeshAgent agent;

    private SlimeEffectManager effects;
    private float baseSpeed;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseSpeed = agent.speed;

        effects = GetComponent<SlimeEffectManager>();

        target = GameObject.Find(targetName).transform;
    }

    private void Update()
    {
        //slow multiplier
        float multiplier = (effects != null) ? effects.CurrentSlowMultiplier : 1f;
        agent.speed = baseSpeed * multiplier;

        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
