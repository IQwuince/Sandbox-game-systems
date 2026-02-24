using UnityEngine;

public class ResetWaves : MonoBehaviour
{
    public Spawner spawner;
    public Variable variable;
    public EnemySpawner enemySpawner;
    public void Reset()
    {
        // Reset spawned blocks
        spawner.spawnedObjects.Clear();
        foreach (Transform child in spawner.spawnLocation)
        {
            Destroy(child.gameObject);
        }

        // Destroy all live enemies
        foreach (var enemy in enemySpawner.liveEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        enemySpawner.liveEnemies.Clear();

        // Reset wave counter
        enemySpawner.currentWave = 0;

        // Reset base health (if Variable is used for base health)
        if (variable != null)
        {
            variable.SetValue(variable.maxValue);
        }
    }
}
