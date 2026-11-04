using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Собирает модели утки-игрока, киви-птиц и шапок из примитивов.
    /// Внешние модели не нужны — всё строится кодом.
    /// </summary>
    public static class CharacterFactory
    {
        public static readonly Color DuckBody = new Color32(246, 243, 231, 255);
        public static readonly Color DuckShade = new Color32(219, 213, 190, 255);
        public static readonly Color DuckBeak = new Color32(242, 160, 54, 255);
        public static readonly Color DuckFoot = new Color32(232, 140, 44, 255);

        public static readonly Color KiwiBody = new Color32(118, 92, 52, 255);
        public static readonly Color KiwiBelly = new Color32(196, 219, 138, 255);
        public static readonly Color KiwiBeak = new Color32(226, 190, 96, 255);
        public static readonly Color KiwiLeg = new Color32(140, 96, 48, 255);

        /// <summary>Создаёт модель утки. Возвращает корневой объект (на нём CharacterController).</summary>
        public static GameObject CreateDuck(Transform parent, string hatId)
        {
            var root = new GameObject("Duck");
            root.transform.SetParent(parent, false);

            // Тело
            Material bodyMat = MaterialFactory.Solid(DuckBody, false, 0.16f);
            Material shadeMat = MaterialFactory.Solid(DuckShade, false, 0.16f);
            Material beakMat = MaterialFactory.Solid(DuckBeak, false, 0.22f);

            GameObject body = MaterialFactory.Sphere("Body", new Vector3(0f, 0.78f, 0f), 0.92f, DuckBody, root.transform, false);
            body.transform.localScale = new Vector3(0.85f, 0.95f, 1.05f);

            // Хвост
            GameObject tail = MaterialFactory.Cone("Tail", new Vector3(0f, 0.86f, -0.5f), 0.26f, 0.36f, DuckShade, root.transform, false);
            tail.transform.localRotation = Quaternion.Euler(72f, 0f, 0f);

            // Шея и голова
            MaterialFactory.Sphere("Neck", new Vector3(0f, 1.15f, 0.02f), 0.42f, DuckBody, root.transform, false);
            GameObject head = MaterialFactory.Sphere("Head", new Vector3(0f, 1.42f, 0.08f), 0.52f, DuckBody, root.transform, false);
            head.transform.localScale = new Vector3(0.95f, 0.95f, 1.05f);

            // Клюв
            GameObject beak = MaterialFactory.Cone("Beak", new Vector3(0f, 1.38f, 0.36f), 0.22f, 0.3f, DuckBeak, root.transform, false);
            beak.transform.localRotation = Quaternion.Euler(92f, 0f, 0f);
            beak.transform.localScale = new Vector3(1f, 1f, 0.65f);

            // Пузо и щёчки — утка перестаёт быть «белым шаром»
            GameObject belly = MaterialFactory.Sphere("Belly", new Vector3(0f, 0.72f, 0.26f), 0.6f,
                new Color32(252, 250, 242, 255), root.transform, false);
            belly.transform.localScale = new Vector3(0.9f, 0.72f, 0.7f);
            MaterialFactory.Sphere("CheekL", new Vector3(-0.2f, 1.34f, 0.16f), 0.11f,
                new Color32(244, 168, 150, 255), root.transform, false);
            MaterialFactory.Sphere("CheekR", new Vector3(0.2f, 1.34f, 0.16f), 0.11f,
                new Color32(244, 168, 150, 255), root.transform, false);

            // Глаза
            Material eyeMat = MaterialFactory.Solid(new Color32(24, 26, 28, 255), false, 0.5f);
            MaterialFactory.Sphere("EyeL", new Vector3(-0.14f, 1.5f, 0.24f), 0.09f, new Color32(24, 26, 28, 255), root.transform, false);
            MaterialFactory.Sphere("EyeR", new Vector3(0.14f, 1.5f, 0.24f), 0.09f, new Color32(24, 26, 28, 255), root.transform, false);
            MaterialFactory.Sphere("EyeHighlightL", new Vector3(-0.155f, 1.515f, 0.275f), 0.032f, Color.white, root.transform, false);
            MaterialFactory.Sphere("EyeHighlightR", new Vector3(0.125f, 1.515f, 0.275f), 0.032f, Color.white, root.transform, false);

            // Крылья
            GameObject wingL = MaterialFactory.Sphere("WingL", new Vector3(-0.38f, 0.86f, -0.02f), 0.5f, DuckShade, root.transform, false);
            wingL.transform.localScale = new Vector3(0.28f, 0.85f, 1.25f);
            wingL.transform.localRotation = Quaternion.Euler(12f, 0f, 8f);
            GameObject wingR = MaterialFactory.Sphere("WingR", new Vector3(0.38f, 0.86f, -0.02f), 0.5f, DuckShade, root.transform, false);
            wingR.transform.localScale = new Vector3(0.28f, 0.85f, 1.25f);
            wingR.transform.localRotation = Quaternion.Euler(12f, 0f, -8f);

            // Лапы
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -0.22f : 0.22f;
                MaterialFactory.Cylinder("Leg" + i, new Vector3(side, 0.26f, 0f), 0.12f, 0.34f, DuckFoot, root.transform, false);
                MaterialFactory.Box("Foot" + i, new Vector3(side, 0.06f, 0.1f), new Vector3(0.22f, 0.08f, 0.34f), DuckFoot, root.transform, false);
            }

            // Якорь для шапки
            var hatAnchor = new GameObject("HatAnchor");
            hatAnchor.transform.SetParent(root.transform, false);
            hatAnchor.transform.localPosition = new Vector3(0f, 1.56f, 0.06f);

            ApplyHat(hatAnchor.transform, hatId);

            // Утица слегка наклоняется вперёд — как в шутерах
            bodyMat.hideFlags = HideFlags.None;
            shadeMat.hideFlags = HideFlags.None;
            beakMat.hideFlags = HideFlags.None;
            eyeMat.hideFlags = HideFlags.None;

            return root;
        }

        /// <summary>Вешает 3D-шапку на указанный якорь.</summary>
        public static void ApplyHat(Transform anchor, string hatId)
        {
            if (anchor == null) return;
            for (int i = anchor.childCount - 1; i >= 0; i--)
                Object.Destroy(anchor.GetChild(i).gameObject);

            if (string.IsNullOrEmpty(hatId) || hatId == "hat_none") return;

            switch (hatId)
            {
                case "hat_kiwi":
                {
                    Material cap = MaterialFactory.Solid(new Color32(112, 168, 40, 255));
                    GameObject dome = MaterialFactory.Sphere("HatKiwi", new Vector3(0f, 0.08f, 0f), 0.42f, new Color32(112, 168, 40, 255), anchor, false);
                    dome.transform.localScale = new Vector3(1f, 0.55f, 1f);
                    GameObject slice = MaterialFactory.Cylinder("Slice", new Vector3(0f, 0.26f, 0f), 0.2f, 0.06f, new Color32(122, 196, 40, 255), anchor, false);
                    slice.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    GameObject core = MaterialFactory.Cylinder("Core", new Vector3(0f, 0.3f, 0f), 0.09f, 0.02f, new Color32(238, 243, 214, 255), anchor, false);
                    core.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    if (cap != null) cap.hideFlags = HideFlags.None;
                    break;
                }
                case "hat_top":
                {
                    MaterialFactory.Cylinder("Brim", new Vector3(0f, 0.02f, 0f), 0.62f, 0.06f, new Color32(30, 34, 40, 255), anchor, false);
                    MaterialFactory.Cylinder("Top", new Vector3(0f, 0.28f, 0f), 0.42f, 0.5f, new Color32(42, 48, 56, 255), anchor, false);
                    MaterialFactory.Cylinder("Band", new Vector3(0f, 0.1f, 0f), 0.44f, 0.09f, new Color32(226, 86, 74, 255), anchor, false);
                    break;
                }
                case "hat_propeller":
                {
                    GameObject cap = MaterialFactory.Sphere("Cap", new Vector3(0f, 0.1f, 0f), 0.44f, new Color32(226, 86, 74, 255), anchor, false);
                    cap.transform.localScale = new Vector3(1f, 0.7f, 1f);
                    MaterialFactory.Box("Visor", new Vector3(0f, 0.04f, 0.26f), new Vector3(0.34f, 0.04f, 0.2f), new Color32(176, 60, 52, 255), anchor, false);
                    MaterialFactory.Cylinder("Stem", new Vector3(0f, 0.32f, 0f), 0.05f, 0.16f, new Color32(58, 66, 74, 255), anchor, false);
                    MaterialFactory.Box("PropA", new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.03f, 0.09f), new Color32(92, 166, 232, 255), anchor, false);
                    MaterialFactory.Box("PropB", new Vector3(0f, 0.42f, 0f), new Vector3(0.09f, 0.03f, 0.42f), new Color32(92, 166, 232, 255), anchor, false);
                    break;
                }
                case "hat_crown":
                {
                    MaterialFactory.Cylinder("CrownBase", new Vector3(0f, 0.06f, 0f), 0.46f, 0.1f, new Color32(196, 140, 26, 255), anchor, false);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * 0.19f, 0.2f, Mathf.Sin(a) * 0.19f);
                        GameObject spike = MaterialFactory.Cone("Spike" + i, p, 0.1f, 0.2f, new Color32(247, 191, 63, 255), anchor, false);
                        spike.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    }
                    break;
                }
                case "hat_party":
                {
                    GameObject cone = MaterialFactory.Cone("Party", new Vector3(0f, 0.24f, 0f), 0.36f, 0.5f, new Color32(168, 120, 224, 255), anchor, false);
                    cone.transform.localScale = new Vector3(1f, 1f, 1f);
                    MaterialFactory.Sphere("Pom", new Vector3(0f, 0.5f, 0f), 0.14f, new Color32(247, 191, 63, 255), anchor, false);
                    break;
                }
            }
        }

        /// <summary>Создаёт модель киви-птицы.</summary>
        public static GameObject CreateKiwi(Transform parent, float scale = 1f)
        {
            var root = new GameObject("Kiwi");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;

            GameObject body = MaterialFactory.Sphere("Body", new Vector3(0f, 0.42f, 0f), 0.5f, KiwiBody, root.transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.95f, 1.15f);

            MaterialFactory.Sphere("Belly", new Vector3(0f, 0.34f, 0.14f), 0.34f, KiwiBelly, root.transform, false);

            MaterialFactory.Sphere("Head", new Vector3(0f, 0.68f, 0.1f), 0.34f, KiwiBody, root.transform, false);

            // Длинный клюв
            GameObject beak = MaterialFactory.Cone("Beak", new Vector3(0f, 0.66f, 0.32f), 0.14f, 0.34f, KiwiBeak, root.transform, false);
            beak.transform.localRotation = Quaternion.Euler(94f, 0f, 0f);
            beak.transform.localScale = new Vector3(1f, 1f, 0.8f);

            MaterialFactory.Sphere("EyeL", new Vector3(-0.09f, 0.73f, 0.2f), 0.07f, new Color32(20, 20, 20, 255), root.transform, false);
            MaterialFactory.Sphere("EyeR", new Vector3(0.09f, 0.73f, 0.2f), 0.07f, new Color32(20, 20, 20, 255), root.transform, false);

            // Крылышки
            GameObject wingL = MaterialFactory.Sphere("WingL", new Vector3(-0.22f, 0.46f, -0.02f), 0.3f, new Color32(102, 78, 42, 255), root.transform, false);
            wingL.transform.localScale = new Vector3(0.3f, 0.9f, 1.3f);
            GameObject wingR = MaterialFactory.Sphere("WingR", new Vector3(0.22f, 0.46f, -0.02f), 0.3f, new Color32(102, 78, 42, 255), root.transform, false);
            wingR.transform.localScale = new Vector3(0.3f, 0.9f, 1.3f);

            // Лапки
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -0.1f : 0.1f;
                MaterialFactory.Cylinder("Leg" + i, new Vector3(side, 0.12f, 0f), 0.07f, 0.2f, KiwiLeg, root.transform, false);
                MaterialFactory.Box("Foot" + i, new Vector3(side, 0.03f, 0.06f), new Vector3(0.14f, 0.05f, 0.2f),
                    KiwiLeg, root.transform, false);
            }

            // Брови — киви выглядит серьёзнее
            MaterialFactory.Box("BrowL", new Vector3(-0.1f, 0.79f, 0.16f), new Vector3(0.11f, 0.03f, 0.05f),
                new Color32(60, 46, 24, 255), root.transform, false);
            MaterialFactory.Box("BrowR", new Vector3(0.1f, 0.79f, 0.16f), new Vector3(0.11f, 0.03f, 0.05f),
                new Color32(60, 46, 24, 255), root.transform, false);

            return root;
        }
    }
}
