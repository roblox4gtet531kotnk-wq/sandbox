using UnityEngine;

namespace KiviGun
{
    /// <summary>Собирает 3D-модель пистолета из примитивов под конкретный стиль оружия.</summary>
    public static class WeaponFactory
    {
        public static GameObject Build(WeaponSpec spec, Transform parent)
        {
            if (spec == null) spec = ShopCatalog.WeaponFor("pistol_base");

            var root = new GameObject("Gun_" + spec.Id);
            root.transform.SetParent(parent, false);

            Material body = MaterialFactory.Solid(spec.BodyColor, false, 0.42f, 0.35f);
            Material accent = MaterialFactory.Solid(spec.AccentColor, false, 0.5f, 0.2f);
            Material dark = MaterialFactory.Solid(new Color32(32, 36, 42, 255), false, 0.3f);

            if (spec.Revolver)
            {
                // Револьвер: ствол, барабан, рукоять
                MaterialFactory.Box("Barrel", new Vector3(0f, 0f, 0.36f), new Vector3(0.11f, 0.12f, 0.62f), spec.BodyColor, root.transform, false);
                MaterialFactory.Cylinder("Cylinder", new Vector3(0f, -0.02f, 0.06f), 0.26f, 0.2f, spec.BodyColor, root.transform, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                MaterialFactory.Box("Grip", new Vector3(0f, -0.19f, -0.12f), new Vector3(0.1f, 0.32f, 0.16f), dark, root.transform, false);
                MaterialFactory.Box("Trigger", new Vector3(0f, -0.1f, 0.02f), new Vector3(0.05f, 0.1f, 0.06f), accent, root.transform, false);
                MaterialFactory.Box("Sight", new Vector3(0f, 0.08f, 0.58f), new Vector3(0.03f, 0.05f, 0.04f), accent, root.transform, false);
            }
            else
            {
                // Пистолет: рамка, ствол, рукоять, скоба
                MaterialFactory.Box("Slide", new Vector3(0f, 0.02f, 0.2f), new Vector3(0.12f, 0.13f, 0.62f), spec.BodyColor, root.transform, false);
                MaterialFactory.Box("Barrel", new Vector3(0f, 0.0f, 0.54f), new Vector3(0.07f, 0.07f, 0.2f), dark, root.transform, false);
                MaterialFactory.Box("Grip", new Vector3(0f, -0.17f, -0.1f), new Vector3(0.1f, 0.28f, 0.15f), dark, root.transform, false);
                MaterialFactory.Box("TriggerGuard", new Vector3(0f, -0.08f, 0.03f), new Vector3(0.07f, 0.05f, 0.14f), dark, root.transform, false);
                MaterialFactory.Box("Sight", new Vector3(0f, 0.1f, 0.46f), new Vector3(0.025f, 0.045f, 0.04f), accent, root.transform, false);
                MaterialFactory.Box("AccentStrip", new Vector3(0f, 0.09f, 0.05f), new Vector3(0.13f, 0.02f, 0.2f), accent, root.transform, false);
            }

            // Ствол-«дуло» для эффектов
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = spec.Revolver ? new Vector3(0f, 0f, 0.68f) : new Vector3(0f, 0f, 0.66f);

            if (body == null || accent == null) Debug.LogWarning("[KiviGun] Материал оружия не создан.");
            return root;
        }
    }
}
