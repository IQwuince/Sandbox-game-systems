using UnityEngine;

public class PointManager : MonoBehaviour
{
    public static PointManager Instance { get; private set; }

    private float points = 0f;

    public TMPro.TextMeshPro pointsText;


    private void Awake()
    {
        //ensure there's only one instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        UpdatePointsText();
    }

    public void AddPoints(float amount)
    {
        points += amount;
        UpdatePointsText();
    }

    private void UpdatePointsText()
    {
        if (pointsText != null)
        {
            pointsText.text = "Points: " + Mathf.FloorToInt(points).ToString();
        }
    }
}
