using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;


public class Enemyspawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public List<BoxCollider> spawnBoxes = new List<BoxCollider>();
    public AnimationCurve enemiesPerWaveCurve; // X: waveIndex, Y: enemyCount
    public AnimationCurve waveDurationCurve;   // X: waveIndex, Y: seconds

    [Header("UI")]
    public TMP_Text roundText;
    public TMP_Text waveStatusText;

    private int currentWave = 0;
    private bool waveActive = false;
    private int enemiesToSpawn = 0;
    private int enemiesSpawned = 0;
    private int enemiesAlive = 0;

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        if (!waveActive && Input.GetKeyDown(KeyCode.P))
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (waveActive) return;

        currentWave++;
        waveActive = true;
        UpdateUI();

        // Use animation curve directly for enemy count
        enemiesToSpawn = Mathf.RoundToInt(enemiesPerWaveCurve.Evaluate(currentWave));
        float waveDuration = waveDurationCurve.Evaluate(currentWave);

        // Distribute enemies randomly among spawn boxes
        int[] boxEnemyCounts = RandomlyDistribute(enemiesToSpawn, spawnBoxes.Count);

        StartCoroutine(SpawnWave(boxEnemyCounts, waveDuration));
    }

    private IEnumerator SpawnWave(int[] boxEnemyCounts, float duration)
    {
        enemiesSpawned = 0;
        enemiesAlive = 0;
        int totalToSpawn = 0;
        foreach (int count in boxEnemyCounts) totalToSpawn += count;

        float spawnInterval = duration / Mathf.Max(totalToSpawn, 1);

        for (int boxIdx = 0; boxIdx < spawnBoxes.Count; boxIdx++)
        {
            for (int i = 0; i < boxEnemyCounts[boxIdx]; i++)
            {
                SpawnEnemyInBox(spawnBoxes[boxIdx]);
                enemiesSpawned++;
                enemiesAlive++;
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // Wait until all enemies are dead (implement your own enemy death tracking)
        while (enemiesAlive > 0)
        {
            yield return null;
        }

        waveActive = false;
        UpdateUI();
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
        // Optionally, subscribe to enemy death event to decrement enemiesAlive
        // Example: enemy.GetComponent<Enemy>().OnDeath += () => enemiesAlive--;
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
                int val = Random.Range(0, remaining + 1);
                result[i] = val;
                remaining -= val;
            }
        }
        return result;
    }

    private void UpdateUI()
    {
        roundText.text = $"Round: {currentWave}";
        waveStatusText.text = waveActive ? "Wave Active" : "Wave Inactive";
    }

    // Call this from your enemy's death logic
    public void OnEnemyDeath()
    {
        enemiesAlive--;
    }
}
