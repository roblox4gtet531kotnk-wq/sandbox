using UnityEngine;

namespace KiviGun
{
    /// <summary>Простое вращение объекта (мельница, лопасти, декорации).</summary>
    public class Spin : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float speed = 20f;

        private void Update()
        {
            transform.Rotate(axis.normalized * speed * Time.deltaTime, Space.Self);
        }
    }
}
