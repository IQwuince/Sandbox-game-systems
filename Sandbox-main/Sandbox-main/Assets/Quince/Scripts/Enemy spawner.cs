using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    public enum WaveState { Inactive, Active, Remaining }

    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public List<BoxCollider> spawnBoxes = new List<BoxCollider>();
    public AnimationCurve enemiesPerWaveCurve; // X: waveIndex, Y: enemyCount
    public AnimationCurve waveDurationCurve;   // X: waveIndex, Y: seconds
    [Tooltip("If true you can start a new wave while previous wave still has enemies -> Remaining state used.")]
    public bool allowRemainingState = true;

    [Header("UI")]
    public TMP_Text roundText;
    public TMP_Text waveStatusText;   // will show: "Wave: Active/Remaining/Inactive"
    public TMP_Text enemiesLeftText;  // shows number of enemies remaining

    private int currentWave = 0;
    private WaveState state = WaveState.Inactive;

    // Tracking enemies
    private HashSet<GameObject> liveEnemies = new HashSet<GameObject>();

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        // Example input to start next wave
        if (Input.GetKeyDown(KeyCode.P))
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        // Disallow starting a new wave when an active wave is running
        if (state == WaveState.Active) return;

        // If in Remaining state but Remaining is disabled, disallow starting
        if (state == WaveState.Remaining && !allowRemainingState) return;

        currentWave++;
        state = WaveState.Active;
        UpdateUI();

        Debug.Log($"Wave {currentWave} started.");

        // Determine counts/duration from curves
        int enemiesToSpawn = Mathf.RoundToInt(enemiesPerWaveCurve.Evaluate(currentWave));
        float waveDuration = waveDurationCurve.Evaluate(currentWave);

        // Distribute enemies across boxes
        int[] boxEnemyCounts = RandomlyDistribute(enemiesToSpawn, spawnBoxes.Count);

        StartCoroutine(SpawnWave(boxEnemyCounts, waveDuration));
    }

    private IEnumerator SpawnWave(int[] boxEnemyCounts, float duration)
    {
        int totalToSpawn = 0;
        foreach (int c in boxEnemyCounts) totalToSpawn += c;

        // Avoid division by zero
        float spawnInterval = (totalToSpawn > 0) ? (duration / totalToSpawn) : 0f;

        // Spawn across boxes (sequentially per-box)
        for (int boxIdx = 0; boxIdx < spawnBoxes.Count; boxIdx++)
        {
            for (int i = 0; i < boxEnemyCounts[boxIdx]; i++)
            {
                SpawnEnemyInBox(spawnBoxes[boxIdx]);
                yield return spawnInterval > 0f ? new WaitForSeconds(spawnInterval) : null;
            }
        }

        // Spawning phase finished, now decide what to do according to allowRemainingState
        if (allowRemainingState)
        {
            // If no live enemies remain, immediately finish
            if (liveEnemies.Count == 0)
            {
                state = WaveState.Inactive;
                Debug.Log($"Wave {currentWave} ended (no remaining enemies).");
            }
            else
            {
                state = WaveState.Remaining;
                Debug.Log($"Wave {currentWave} ended (remaining enemies: {liveEnemies.Count}).");
            }
            UpdateUI();
            yield break;
        }
        else
        {
            // Wait until all enemies are dead before marking inactive (you cannot start a new wave while waiting)
            while (liveEnemies.Count > 0)
            {
                yield return null;
            }
            state = WaveState.Inactive;
            UpdateUI();
            Debug.Log($"Wave {currentWave} ended (all enemies dead).");
            yield break;
        }
    }

    private void SpawnEnemyInBox(BoxCollider box)
    {
        Vector3 center = box.transform.position + box.center;
        Vector3 size = box.size;
        Vector3 localPos = new Vector3(
            Random.Range(-size.x / 2, size.x / 2),
            Random.Range(-size.y / 2, size.y / 2),
            Random.Range(-size.z / 2, size.z / 2)
        );
        Vector3 spawnPos = center + box.transform.rotation * localPos;
        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // Register enemy with the spawner so it is tracked in the HashSet
        RegisterEnemy(enemy);

        // Attach an EnemyNotifier only if the prefab doesn't already contain one
        EnemyNotifier notifier = enemy.GetComponent<EnemyNotifier>();
        if (notifier == null)
        {
            enemy.AddComponent<EnemyNotifier>();
        }
        // EnemyNotifier will auto-find the spawner (it keeps a private reference)
    }

    // Adds an enemy to the HashSet and updates UI
    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        if (liveEnemies.Add(enemy))
        {
            UpdateUI();
        }
    }

    // Removes an enemy (called by EnemyNotifier.OnDestroy or from enemy death logic)
    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        if (liveEnemies.Remove(enemy))
        {
            UpdateUI();

            // If we were in Active or Remaining and now have zero enemies, handle state transitions
            if (liveEnemies.Count == 0)
            {
                if (state == WaveState.Remaining)
                {
                    state = WaveState.Inactive;
                    UpdateUI();
                    Debug.Log($"All remaining enemies from previous waves are dead. State -> Inactive.");
                }
                else if (state == WaveState.Active && !allowRemainingState)
                {
                    state = WaveState.Inactive;
                    UpdateUI();
                    Debug.Log($"All enemies dead. Wave {currentWave} ended. State -> Inactive.");
                }
            }
        }
    }

    private int[] RandomlyDistribute(int total, int boxes)
    {
        int[] result = new int[boxes];
        int remaining = total;
        for (int i = 0; i < boxes; i++)
        {
            if (i == boxes - 1)
            {
                result[i] = remaining;
            }
            else
            {
                int val = (remaining > 0) ? Random.Range(0, remaining + 1) : 0;
                result[i] = val;
                remaining -= val;
            }
        }
        return result;
    }

    private void UpdateUI()
    {
        if (roundText) roundText.text = $"Round: {currentWave}";
        if (waveStatusText) waveStatusText.text = $"Wave: {state}";
        if (enemiesLeftText) enemiesLeftText.text = $"Enemies: {liveEnemies.Count}";
    }

    // For compatibility: if your enemy has a death hook, you can call this from enemy code:
    public void OnEnemyDeath(GameObject enemy)
    {
        UnregisterEnemy(enemy);
    }
}