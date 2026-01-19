using UnityEngine;

// Simple helper that notifies the spawner when this GameObject is destroyed.
// This version keeps a private reference to the spawner and auto-finds it at runtime,
// so it is safe to add this component to a prefab without setting any inspector fields.
public class EnemyNotifier : MonoBehaviour
{
    private EnemySpawner spawner;

    private void Awake()
    {
        if (spawner == null)
        {
            // Updated to use the recommended method to find the spawner in the scene.
            spawner = Object.FindFirstObjectByType<EnemySpawner>();
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.UnregisterEnemy(gameObject);
        }
    }
}
