using System.Collections;
using UnityEngine;

public class SandboxItem : MonoBehaviour
{
    [Header("Id")]
    public string itemId;

    [Header("Fuse data")]
    public FusionDatabase fusionDatabase;

    [Tooltip("Only fuse with other placeable items on these layers")]
    public LayerMask itemLayerMask;

    [Tooltip("Prevents unintended double-fusion")]
    public float armDelay = 0.1f;

    private bool armed;
    private bool fusionLocked;

    private void OnEnable()
    {
        armed = false;
        fusionLocked = false;
        StartCoroutine(ArmAfterDelay());
    }

    private IEnumerator ArmAfterDelay()
    {
        yield return new WaitForSeconds(armDelay);
        armed = true;
    }

    private void OnTriggerEnter(Collider other) => TryFuse(other);
    private void OnTriggerStay(Collider other) => TryFuse(other);

    private void TryFuse(Collider other)
    {
        if (!armed || fusionLocked) return;

        // layer filter
        if (((1 << other.gameObject.layer) & itemLayerMask) == 0)
            return;

        // find the other item
        SandboxItem otherItem = other.GetComponentInParent<SandboxItem>();
        if (otherItem == null) return;
        if (otherItem == this) return;
        if (!otherItem.armed || otherItem.fusionLocked) return;

        // must have a database
        if (fusionDatabase == null)
        {
            Debug.LogWarning($"[SandboxItem] '{name}' has no FusionDatabase assigned.");
            return;
        }

        // Must reference the same database
        if (otherItem.fusionDatabase != fusionDatabase)
        {
            Debug.LogWarning($"[SandboxItem] '{name}' and '{otherItem.name}' have different FusionDatabase assets. Not fusing.");
            return;
        }

        // look up recipe
        if (!fusionDatabase.TryGetResult(itemId, otherItem.itemId, out GameObject resultPrefab))
            return;

        // lock both to only fuse once
        fusionLocked = true;
        otherItem.fusionLocked = true;

        // spawn result at midpoint
        Vector3 spawnPos = (transform.position + otherItem.transform.position) * 0.5f;
        Quaternion spawnRot = transform.rotation;

        Instantiate(resultPrefab, spawnPos, spawnRot);

        // destroy both inputs
        Destroy(otherItem.gameObject);
        Destroy(gameObject);
    }
}
