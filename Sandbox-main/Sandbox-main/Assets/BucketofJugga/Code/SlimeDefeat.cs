using UnityEngine;

public class SlimeDefeat : MonoBehaviour
{
    public void AwardPoints(float amount)
    {
        PointManager.Instance.AddPoints(amount);
    }

    public void RemoveSlime()
    {
        if (gameObject != null) 
            Destroy(gameObject);
    }
}
