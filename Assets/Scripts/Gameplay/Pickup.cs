using UnityEngine;

namespace KiviGun
{
    /// <summary>Подбираемая киви-долька — валюта игры.</summary>
    public class Pickup : MonoBehaviour
    {
        public int Value = 1;
        public Transform target;

        private float _timer;
        private Transform _model;
        private bool _collected;

        public static Pickup Spawn(Vector3 position, int value)
        {
            var go = new GameObject("KiwiSlice");
            go.transform.position = position;
            Pickup pickup = go.AddComponent<Pickup>();
            pickup.Value = value;
            pickup.BuildModel();
            return pickup;
        }

        private void BuildModel()
        {
            Material flesh = MaterialFactory.Solid(new Color32(140, 198, 32, 255), false, 0.25f);
            Material core = MaterialFactory.Solid(new Color32(238, 243, 214, 255), false, 0.25f);

            _model = new GameObject("SliceModel").transform;
            _model.SetParent(transform, false);

            MaterialFactory.Cylinder("Flesh", Vector3.zero, 0.52f, 0.12f, new Color32(140, 198, 32, 255), _model, false);
            MaterialFactory.Cylinder("Core", new Vector3(0f, 0.07f, 0f), 0.2f, 0.04f, new Color32(238, 243, 214, 255), _model, false);

            // Семечки
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                MaterialFactory.Sphere("Seed", new Vector3(Mathf.Cos(a) * 0.16f, 0.08f, Mathf.Sin(a) * 0.16f), 0.06f,
                    new Color32(30, 28, 26, 255), _model, false);
            }

            if (flesh == null || core == null) return;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_model != null)
            {
                _model.Rotate(Vector3.up, 140f * Time.deltaTime, Space.World);
                _model.localPosition = new Vector3(0f, 0.35f + Mathf.Sin(_timer * 2.6f) * 0.12f, 0f);
            }

            if (_collected || target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < 1.6f)
            {
                // Долька летит к игроку
                transform.position = Vector3.Lerp(transform.position, target.position + Vector3.up, 6f * Time.deltaTime);
            }

            if (distance < 1.0f) Collect();
        }

        private void Collect()
        {
            _collected = true;
            SaveSystem.AddSlices(Value);
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Pickup, 0.8f, UnityEngine.Random.Range(0.95f, 1.1f));
            ToastManager.GetOrCreate().Show("+" + Value + " " + GameConfig.Currency);
            Destroy(gameObject);
        }
    }
}
