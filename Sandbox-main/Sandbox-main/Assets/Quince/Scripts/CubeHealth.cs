using UnityEngine;

public class CubeHealth : MonoBehaviour
{
    public int health = 100;
    public ResetWaves resetWaves;

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}
