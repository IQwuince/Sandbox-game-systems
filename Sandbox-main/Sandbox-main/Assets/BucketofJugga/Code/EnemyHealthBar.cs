using UnityEngine;
using UnityEngine.UI;
using TMPro;

//basically same as player health but local event to enemy to avoid event bus spam
namespace Game.UI
{
    public class EnemyHealthBar : MonoBehaviour
    {   
        [SerializeField] private Slider hpBar;
        [SerializeField] private bool billboardToCamera = true;

        [Header("Player Camera")]
        [SerializeField] private Camera cam;

        private void Awake()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
        }


        private void LateUpdate()
        {
            if (billboardToCamera && cam != null)
            {
                //look at camera while keeping upright
                Vector3 dir = transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f) //if camera position/rotation changed
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        public void OnEnemyHealthChanged(int value, int maxValue)
        {
            if (hpBar != null)
            {
                hpBar.maxValue = maxValue;
                hpBar.value = value;
            }
        }


    }
}