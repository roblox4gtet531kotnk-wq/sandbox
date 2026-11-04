using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Процедурный генератор карт. Три темы: город, посёлок и офис.
    /// Все объекты ставятся из примитивов, с коллайдерами, тенями и освещением.
    /// </summary>
    public static class LevelBuilder
    {
        public class LevelData
        {
            public Transform Root;
            public Vector3 PlayerSpawn;
            public List<Vector3> EnemySpawns = new List<Vector3>();
            public Transform[] CoverPoints;
        }

        public static LevelData Build(LevelDefinition def, Transform parent)
        {
            var data = new LevelData();
            var rootGo = new GameObject("Level_" + def.Title);
            rootGo.transform.SetParent(parent, false);
            data.Root = rootGo.transform;

            switch (def.Theme)
            {
                case LevelTheme.City: BuildCity(def, data); break;
                case LevelTheme.Settlement: BuildSettlement(def, data); break;
                case LevelTheme.Wheatfield: BuildWheatfield(def, data); break;
                default: BuildOffice(def, data); break;
            }

            return data;
        }

        // =====================================================================
        //  Киви-Сити
        // =====================================================================
        private static void BuildCity(LevelDefinition def, LevelData data)
        {
            var rnd = new System.Random(1001 + def.Index * 77);
            Transform root = data.Root;

            SetupAmbient(new Color32(150, 196, 232, 255), new Color32(120, 140, 150, 255), 1.15f);
            SetupSun(new Color32(255, 244, 214, 255), 48f, 52f, true);
            SetupFog(new Color32(168, 196, 216, 255), 0.006f);

            // Земля и тротуары
            MaterialFactory.Box("Ground", new Vector3(0f, -0.3f, 0f), new Vector3(96f, 0.6f, 96f), new Color32(72, 76, 82, 255), root);
            MaterialFactory.Box("RoadX", new Vector3(0f, 0.02f, 0f), new Vector3(84f, 0.1f, 15f), new Color32(58, 62, 68, 255), root);
            MaterialFactory.Box("RoadZ", new Vector3(0f, 0.03f, 0f), new Vector3(15f, 0.1f, 84f), new Color32(58, 62, 68, 255), root);

            // Разметка
            for (int i = -38; i <= 38; i += 6)
            {
                MaterialFactory.Box("MarkX" + i, new Vector3(i, 0.09f, 0f), new Vector3(2.6f, 0.04f, 0.28f), new Color32(236, 214, 96, 255), root, false);
                MaterialFactory.Box("MarkZ" + i, new Vector3(0f, 0.1f, i), new Vector3(0.28f, 0.04f, 2.6f), new Color32(236, 214, 96, 255), root, false);
            }

            // Тротуары
            MaterialFactory.Box("WalkN", new Vector3(0f, 0.11f, 9.2f), new Vector3(84f, 0.22f, 3.4f), new Color32(178, 180, 184, 255), root);
            MaterialFactory.Box("WalkS", new Vector3(0f, 0.11f, -9.2f), new Vector3(84f, 0.22f, 3.4f), new Color32(178, 180, 184, 255), root);
            MaterialFactory.Box("WalkE", new Vector3(9.2f, 0.12f, 0f), new Vector3(3.4f, 0.22f, 84f), new Color32(178, 180, 184, 255), root);
            MaterialFactory.Box("WalkW", new Vector3(-9.2f, 0.12f, 0f), new Vector3(3.4f, 0.22f, 84f), new Color32(178, 180, 184, 255), root);

            // Здания
            int seed = 7;
            float[] positions = { -26f, -13.5f, 13.5f, 26f };
            for (int xi = 0; xi < positions.Length; xi++)
            {
                for (int zi = 0; zi < positions.Length; zi++)
                {
                    float x = positions[xi];
                    float z = positions[zi];
                    // Парк занимает один квартал
                    if (xi == 2 && zi == 2) continue;
                    if (rnd.NextDouble() < 0.12) continue;

                    float w = 8f + (float)rnd.NextDouble() * 3f;
                    float d = 8f + (float)rnd.NextDouble() * 3f;
                    float h = 9f + (float)rnd.NextDouble() * 16f;
                    var wallColor = PaletteFor(rnd, xi * 4 + zi);
                    Material mat = WindowMaterial(wallColor, new Color32(96, 128, 150, 255), 0.45f, seed++);

                    GameObject building = MaterialFactory.Box("Building", new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), Color.white, root);
                    building.GetComponent<MeshRenderer>().sharedMaterial = mat;

                    // Крыша
                    MaterialFactory.Box("Roof", new Vector3(x, h + 0.25f, z), new Vector3(w + 0.7f, 0.5f, d + 0.7f), new Color32(70, 74, 82, 255), root);
                    // Вход
                    MaterialFactory.Box("Door", new Vector3(x, 1.3f, z - d * 0.5f - 0.06f), new Vector3(1.6f, 2.6f, 0.2f), new Color32(62, 66, 74, 255), root, false);
                    // Крыша-кондиционер
                    if (rnd.NextDouble() < 0.5)
                        MaterialFactory.Box("AC", new Vector3(x + 1f, h + 0.9f, z), new Vector3(1.4f, 0.8f, 1.4f), new Color32(120, 124, 132, 255), root);
                }
            }

            // Парк
            BuildPark(new Vector3(13.5f, 0f, 13.5f), root, rnd);

            // Машины
            for (int i = 0; i < 9; i++)
            {
                float along = -36f + i * 9f;
                bool onX = i % 2 == 0;
                Vector3 pos = onX ? new Vector3(along, 0f, (i % 4 == 0 ? 3.4f : -3.4f)) : new Vector3((i % 4 == 1 ? 3.4f : -3.4f), 0f, along);
                BuildCar(pos, root, rnd, i);
            }

            // Фонари
            for (int i = -30; i <= 30; i += 15)
            {
                BuildStreetLamp(new Vector3(i, 0f, 8f), root, false);
                BuildStreetLamp(new Vector3(i, 0f, -8f), root, true);
                BuildStreetLamp(new Vector3(8f, 0f, i), root, false);
                BuildStreetLamp(new Vector3(-8f, 0f, i), root, true);
            }

            // Мусорные баки, ящики, остановка
            for (int i = 0; i < 10; i++)
            {
                float x = -34f + (float)rnd.NextDouble() * 68f;
                float z = -34f + (float)rnd.NextDouble() * 68f;
                if (Mathf.Abs(x) < 9f || Mathf.Abs(z) < 9f) continue;
                if (rnd.NextDouble() < 0.5)
                    MaterialFactory.Cylinder("Bin", new Vector3(x, 0.6f, z), 0.8f, 1.2f, new Color32(52, 92, 62, 255), root);
                else
                    MaterialFactory.Box("Crate", new Vector3(x, 0.6f, z), new Vector3(1.2f, 1.2f, 1.2f), new Color32(152, 118, 70, 255), root);
            }

            // Билборд с логотипом
            BuildBillboard(new Vector3(-11f, 0f, -26f), root);

            data.PlayerSpawn = new Vector3(0f, 1.2f, -34f);
            FillSpawns(data, 44f, 13f, def.EnemyCount, rnd);
        }

        // =====================================================================
        //  Киви-Посёлок
        // =====================================================================
        private static void BuildSettlement(LevelDefinition def, LevelData data)
        {
            var rnd = new System.Random(2002 + def.Index * 91);
            Transform root = data.Root;

            SetupAmbient(new Color32(168, 206, 236, 255), new Color32(126, 132, 110, 255), 1.05f);
            SetupSun(new Color32(255, 226, 178, 255), 42f, 62f, true);
            SetupFog(new Color32(206, 198, 168, 255), 0.007f);

            // Земля
            MaterialFactory.Box("Ground", new Vector3(0f, -0.3f, 0f), new Vector3(96f, 0.6f, 96f), new Color32(94, 142, 70, 255), root);

            // Грунтовые дороги
            MaterialFactory.Box("RoadX", new Vector3(0f, 0.03f, 0f), new Vector3(84f, 0.12f, 7f), new Color32(158, 132, 92, 255), root);
            MaterialFactory.Box("RoadZ", new Vector3(0f, 0.04f, 0f), new Vector3(7f, 0.12f, 84f), new Color32(158, 132, 92, 255), root);

            // Поля
            MaterialFactory.Box("FieldWheat", new Vector3(-26f, 0.02f, 26f), new Vector3(22f, 0.1f, 22f), new Color32(214, 186, 92, 255), root, false);
            MaterialFactory.Box("FieldSoil", new Vector3(26f, 0.02f, -26f), new Vector3(22f, 0.1f, 22f), new Color32(112, 84, 62, 255), root, false);
            MaterialFactory.Box("FieldGreen", new Vector3(26f, 0.02f, 26f), new Vector3(22f, 0.1f, 22f), new Color32(120, 168, 78, 255), root, false);

            // Дома
            for (int i = 0; i < 9; i++)
            {
                float x = -33f + (float)rnd.NextDouble() * 66f;
                float z = -33f + (float)rnd.NextDouble() * 66f;
                if (Mathf.Abs(x) < 6f || Mathf.Abs(z) < 6f) continue;
                BuildHouse(new Vector3(x, 0f, z), root, rnd, i);
            }

            // Сарай
            BuildBarn(new Vector3(-12f, 0f, 14f), root);

            // Мельница
            BuildWindmill(new Vector3(14f, 0f, 12f), root);

            // Заборы
            BuildFenceLine(new Vector3(-26f, 0f, 14f), new Vector3(22f, 0f, 0f), root);
            BuildFenceLine(new Vector3(26f, 0f, -14f), new Vector3(22f, 0f, 0f), root);
            BuildFenceLine(new Vector3(-13f, 0f, -26f), new Vector3(0f, 0f, 22f), root);

            // Сеновалы, колодец, бочка, поленница
            for (int i = 0; i < 5; i++)
            {
                float x = -30f + (float)rnd.NextDouble() * 60f;
                float z = -30f + (float)rnd.NextDouble() * 60f;
                if (Mathf.Abs(x) < 6f || Mathf.Abs(z) < 6f) continue;
                MaterialFactory.Cylinder("Hay", new Vector3(x, 0.9f, z), 2.2f, 1.8f, new Color32(222, 196, 104, 255), root);
            }

            BuildWell(new Vector3(-8f, 0f, -12f), root);
            for (int i = 0; i < 4; i++)
                MaterialFactory.Cylinder("Barrel", new Vector3(18f + i * 1.4f, 0.6f, -10f), 1.1f, 1.2f, new Color32(142, 96, 58, 255), root);

            // Деревья
            for (int i = 0; i < 16; i++)
            {
                float x = -36f + (float)rnd.NextDouble() * 72f;
                float z = -36f + (float)rnd.NextDouble() * 72f;
                if (Mathf.Abs(x) < 5f || Mathf.Abs(z) < 5f) continue;
                BuildTree(new Vector3(x, 0f, z), root, rnd);
            }

            // Пруд
            MaterialFactory.Box("Pond", new Vector3(-30f, 0.08f, -30f), new Vector3(14f, 0.12f, 10f), new Color32(72, 128, 168, 255), root, false);
            BuildTree(new Vector3(-34f, 0f, -26f), root, rnd);
            BuildTree(new Vector3(-26f, 0f, -34f), root, rnd);

            data.PlayerSpawn = new Vector3(0f, 1.2f, -32f);
            FillSpawns(data, 38f, 11f, def.EnemyCount, rnd);
        }

        // =====================================================================
        //  Пшеничное поле (4-й уровень, сложность «Нормальное»)
        // =====================================================================
        private static void BuildWheatfield(LevelDefinition def, LevelData data)
        {
            var rnd = new System.Random(4004 + def.Index * 37);
            Transform root = data.Root;

            // Тёплый вечерний свет, лёгкая золотая дымка — «хлебная» атмосфера.
            SetupAmbient(new Color32(236, 206, 150, 255), new Color32(150, 126, 84, 255), 1.1f);
            SetupSun(new Color32(255, 216, 140, 255), 50f, 70f, true);
            SetupFog(new Color32(238, 202, 142, 255), 0.009f);

            // Земля — с текстурой, чтобы не была однотонной плитой.
            Texture2D soil = MaterialFactory.NoiseTexture(64,
                new Color32(104, 84, 48, 255), new Color32(148, 122, 74, 255), 0.18f, 4111);
            Material soilMat = MaterialFactory.Textured(new Color32(126, 104, 62, 255), soil,
                new Vector2(24f, 24f), 0.04f);
            MaterialFactory.Box("Ground", new Vector3(0f, -0.3f, 0f), new Vector3(110f, 0.6f, 110f),
                soilMat, root);

            // --- Пшеница: ряды «пятен» (без коллайдеров — иначе тормоза) ---
            var wheat = new Color32(226, 190, 92, 255);
            var wheatDark = new Color32(198, 160, 70, 255);
            var covers = new System.Collections.Generic.List<Transform>();

            // Два материала пшеницы с текстурой — колосья не выглядят плоской заливкой.
            Texture2D wheatTex = MaterialFactory.NoiseTexture(32,
                new Color32(196, 158, 66, 255), new Color32(248, 214, 122, 255), 0.35f, 4222);
            Material wheatMat = MaterialFactory.Textured(wheat, wheatTex, new Vector2(2f, 2f), 0.03f);
            Material wheatDarkMat = MaterialFactory.Textured(wheatDark, wheatTex, new Vector2(2.4f, 2.4f), 0.03f);

            for (int row = -6; row <= 6; row++)
            {
                for (int col = -6; col <= 6; col++)
                {
                    // Клетки рядом с дорогой оставляем под тропы
                    if (Mathf.Abs(col) <= 1 || Mathf.Abs(row) <= 1) continue;

                    float x = col * 7f + (float)rnd.NextDouble() * 1.2f;
                    float z = row * 7f + (float)rnd.NextDouble() * 1.2f;
                    Material patchMat = (row + col) % 2 == 0 ? wheatMat : wheatDarkMat;

                    GameObject patch = MaterialFactory.Box("Wheat_" + row + "_" + col,
                        new Vector3(x, 0.35f, z), new Vector3(6.2f, 0.7f, 6.2f), patchMat, root, false);

                    // Колосья — тонкие «ёлочки», чтобы поле не было плоским
                    for (int i = 0; i < 2; i++)
                    {
                        float ox = x + (float)rnd.NextDouble() * 5f - 2.5f;
                        float oz = z + (float)rnd.NextDouble() * 5f - 2.5f;
                        GameObject ear = MaterialFactory.Cone("Ear", new Vector3(ox, 0.95f, oz), 0.34f, 0.9f,
                            wheat, root, false);
                        ear.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 12f - 6f, 0f,
                            (float)rnd.NextDouble() * 12f - 6f);
                    }

                    // Каждая 4-я клетка — укрытие для умных киви
                    if ((row + col) % 4 == 0) covers.Add(patch.transform);
                }
            }

            // Тропы — утоптанная земля
            Texture2D dirt = MaterialFactory.NoiseTexture(48,
                new Color32(136, 114, 74, 255), new Color32(186, 162, 112, 255), 0.25f, 4333);
            Material pathMat = MaterialFactory.Textured(new Color32(158, 136, 92, 255), dirt,
                new Vector2(12f, 2f), 0.03f);
            MaterialFactory.Box("PathX", new Vector3(0f, 0.06f, 0f), new Vector3(96f, 0.12f, 5.5f),
                pathMat, root, false);
            MaterialFactory.Box("PathZ", new Vector3(0f, 0.07f, 0f), new Vector3(5.5f, 0.12f, 96f),
                pathMat, root, false);

            // Сарай, мельница, заборы
            BuildBarn(new Vector3(-22f, 0f, 20f), root);
            BuildWindmill(new Vector3(24f, 0f, 18f), root);
            BuildFenceLine(new Vector3(-34f, 0f, -18f), new Vector3(28f, 0f, 0f), root);
            BuildFenceLine(new Vector3(18f, 0f, -26f), new Vector3(0f, 0f, 24f), root);

            // Сарай и мельница тоже укрытия
            covers.Add(root.Find("Barn") != null ? root.Find("Barn").transform : null);
            covers.Add(root.Find("Tower") != null ? root.Find("Tower").transform : null);

            // Снопы и мешки
            for (int i = 0; i < 6; i++)
            {
                float x = -28f + (float)rnd.NextDouble() * 56f;
                float z = -28f + (float)rnd.NextDouble() * 56f;
                if (Mathf.Abs(x) < 5f || Mathf.Abs(z) < 5f) continue;
                GameObject hay = MaterialFactory.Cylinder("Haystack", new Vector3(x, 1f, z), 2.4f, 2f,
                    new Color32(230, 200, 110, 255), root);
                covers.Add(hay.transform);
            }

            // Пугало — украшение и укрытие
            GameObject scarecrow = BuildScarecrow(new Vector3(-10f, 0f, -16f), root);
            if (scarecrow != null) covers.Add(scarecrow.transform);

            // Деревья по краям поля
            for (int i = 0; i < 14; i++)
            {
                float x = -46f + (float)rnd.NextDouble() * 92f;
                float z = -46f + (float)rnd.NextDouble() * 92f;
                if (Mathf.Abs(x) < 40f && Mathf.Abs(z) < 40f) continue;
                BuildTree(new Vector3(x, 0f, z), root, rnd);
            }

            // Укрытия для ИИ киви (без null-элементов)
            var coverList = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < covers.Count; i++)
                if (covers[i] != null) coverList.Add(covers[i]);
            data.CoverPoints = coverList.ToArray();

            data.PlayerSpawn = new Vector3(0f, 1.2f, -34f);
            FillSpawns(data, 40f, 12f, def.EnemyCount, rnd);
        }

        /// <summary>Пугало: столб, шляпа и соломенное тело. Возвращает корневой объект.</summary>
        private static GameObject BuildScarecrow(Vector3 pos, Transform root)
        {
            GameObject go = new GameObject("Scarecrow");
            go.transform.SetParent(root, false);
            go.transform.position = pos;

            MaterialFactory.Cylinder("Post", new Vector3(0f, 1.1f, 0f), 0.16f, 2.2f,
                new Color32(122, 92, 56, 255), go.transform, false);
            MaterialFactory.Box("Arms", new Vector3(0f, 1.65f, 0f), new Vector3(1.9f, 0.14f, 0.14f),
                new Color32(122, 92, 56, 255), go.transform, false);
            MaterialFactory.Box("Shirt", new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.85f, 0.4f),
                new Color32(186, 72, 60, 255), go.transform, false);
            MaterialFactory.Sphere("Head", new Vector3(0f, 2.1f, 0f), 0.5f,
                new Color32(226, 196, 120, 255), go.transform, false);

            // Шляпа
            MaterialFactory.Cylinder("HatBrim", new Vector3(0f, 2.3f, 0f), 0.8f, 0.05f,
                new Color32(140, 104, 56, 255), go.transform, false);
            MaterialFactory.Cylinder("HatTop", new Vector3(0f, 2.44f, 0f), 0.42f, 0.32f,
                new Color32(140, 104, 56, 255), go.transform, false);

            return go;
        }

        // =====================================================================
        //  Офис Kivi-Microsoft
        // =====================================================================
        private static void BuildOffice(LevelDefinition def, LevelData data)
        {
            var rnd = new System.Random(3003 + def.Index * 53);
            Transform root = data.Root;

            SetupAmbient(new Color32(120, 132, 150, 255), new Color32(88, 96, 112, 255), 0.75f);

            // Пол, потолок, стены
            MaterialFactory.Box("Floor", new Vector3(0f, -0.2f, 0f), new Vector3(46f, 0.4f, 34f), new Color32(64, 72, 82, 255), root);
            MaterialFactory.Box("Ceiling", new Vector3(0f, 5f, 0f), new Vector3(46f, 0.4f, 34f), new Color32(48, 54, 62, 255), root);
            MaterialFactory.Box("WallN", new Vector3(0f, 2.5f, -17f), new Vector3(46f, 5f, 0.5f), new Color32(196, 202, 210, 255), root);
            MaterialFactory.Box("WallS", new Vector3(0f, 2.5f, 17f), new Vector3(46f, 5f, 0.5f), new Color32(196, 202, 210, 255), root);
            MaterialFactory.Box("WallW", new Vector3(-23f, 2.5f, 0f), new Vector3(0.5f, 5f, 34f), new Color32(196, 202, 210, 255), root);
            MaterialFactory.Box("WallE", new Vector3(23f, 2.5f, 0f), new Vector3(0.5f, 5f, 34f), new Color32(196, 202, 210, 255), root);

            // Стеклянные перегородки (переговорная)
            Material glass = MaterialFactory.Transparent(new Color32(150, 210, 235, 255), 0.3f);
            GameObject glassWall = MaterialFactory.Box("GlassWall", new Vector3(9f, 2.4f, -8f), new Vector3(0.2f, 4.8f, 16f), Color.white, root);
            glassWall.GetComponent<MeshRenderer>().sharedMaterial = glass;
            GameObject glassWall2 = MaterialFactory.Box("GlassWall2", new Vector3(17f, 2.4f, -8f), new Vector3(16f, 4.8f, 0.2f), Color.white, root);
            glassWall2.GetComponent<MeshRenderer>().sharedMaterial = glass;

            // Стол в переговорной
            MaterialFactory.Box("MeetingTable", new Vector3(13f, 0.85f, -8f), new Vector3(7f, 0.2f, 3.4f), new Color32(122, 92, 68, 255), root);
            for (int i = 0; i < 4; i++)
                MaterialFactory.Box("MeetingLeg", new Vector3(10f + i * 2f, 0.42f, -8f), new Vector3(0.16f, 0.84f, 3f), new Color32(90, 68, 52, 255), root);
            for (int i = 0; i < 6; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float z = -9.6f + (i / 2) * 2.2f;
                MaterialFactory.Box("Chair", new Vector3(13f + side * 2.4f, 0.5f, z), new Vector3(0.8f, 0.12f, 0.8f), new Color32(58, 64, 74, 255), root);
                MaterialFactory.Box("ChairBack", new Vector3(13f + side * 2.85f, 0.95f, z), new Vector3(0.12f, 0.9f, 0.8f), new Color32(58, 64, 74, 255), root);
            }

            // Кубиклы
            for (int ix = 0; ix < 4; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    float x = -18f + ix * 9f;
                    float z = -12f + iz * 9f;
                    BuildCubicle(new Vector3(x, 0f, z), root, rnd);
                }
            }

            // Серверная
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-20f + i * 1.6f, 1.1f, 14f);
                GameObject rack = MaterialFactory.Box("Rack", p, new Vector3(1.2f, 2.2f, 0.9f), new Color32(38, 42, 50, 255), root);
                Material led = MaterialFactory.Emissive(new Color32(40, 46, 54, 255), new Color32(90, 240, 140, 255), 2.2f);
                GameObject panel = MaterialFactory.Box("RackLed", new Vector3(p.x, p.y + 0.3f, p.z - 0.48f), new Vector3(1.0f, 0.5f, 0.06f), Color.white, root, false);
                panel.GetComponent<MeshRenderer>().sharedMaterial = led;
                if (rack == null) continue;
            }
            MaterialFactory.Box("ServerRoomWall", new Vector3(-14f, 2.4f, 12.5f), new Vector3(0.2f, 4.8f, 6f), new Color32(168, 176, 186, 255), root);

            // Кабинет директора
            MaterialFactory.Box("CeoDesk", new Vector3(18f, 0.9f, 8f), new Vector3(6f, 0.2f, 2.4f), new Color32(72, 52, 40, 255), root);
            MaterialFactory.Box("CeoMonitor", new Vector3(18f, 1.5f, 8f), new Vector3(1.8f, 1.1f, 0.12f), new Color32(30, 34, 40, 255), root);
            MaterialFactory.Box("CeoChair", new Vector3(18f, 0.6f, 10f), new Vector3(1.1f, 0.16f, 1.1f), new Color32(42, 48, 58, 255), root);
            BuildPlant(new Vector3(21f, 0f, 13f), root);
            BuildPlant(new Vector3(15f, 0f, 13f), root);

            // Зона отдыха
            MaterialFactory.Box("Vending", new Vector3(-20f, 1.1f, -14f), new Vector3(1.6f, 2.2f, 1.1f), new Color32(190, 64, 56, 255), root);
            MaterialFactory.Box("Cooler", new Vector3(-17f, 0.6f, -14f), new Vector3(0.8f, 1.2f, 0.8f), new Color32(120, 180, 220, 255), root);
            MaterialFactory.Box("BreakTable", new Vector3(-14f, 0.85f, -14f), new Vector3(3f, 0.16f, 1.4f), new Color32(150, 150, 158, 255), root);
            MaterialFactory.Box("BreakLeg", new Vector3(-14f, 0.42f, -14f), new Vector3(0.4f, 0.84f, 1.2f), new Color32(120, 120, 128, 255), root);

            // Лифты и доска
            MaterialFactory.Box("Elevator", new Vector3(0f, 1.3f, 16.6f), new Vector3(4.4f, 2.6f, 0.3f), new Color32(140, 146, 156, 255), root);
            MaterialFactory.Box("Whiteboard", new Vector3(-6f, 2f, 16.6f), new Vector3(4f, 2f, 0.15f), new Color32(240, 244, 246, 255), root);

            // Свет
            for (int ix = 0; ix < 4; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    Vector3 p = new Vector3(-15f + ix * 10f, 4.7f, -11f + iz * 10f);
                    Material panel = MaterialFactory.Emissive(new Color32(240, 244, 250, 255), new Color32(230, 240, 255, 255), 1.4f);
                    GameObject lamp = MaterialFactory.Box("CeilingLight", p, new Vector3(4.4f, 0.14f, 1.2f), Color.white, root, false);
                    lamp.GetComponent<MeshRenderer>().sharedMaterial = panel;

                    var lightGo = new GameObject("PointLight");
                    lightGo.transform.SetParent(root, false);
                    lightGo.transform.localPosition = p - new Vector3(0f, 0.4f, 0f);
                    var lt = lightGo.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.color = new Color32(236, 242, 255, 255);
                    lt.range = 16f;
                    lt.intensity = 1.5f;
                    lt.shadows = LightShadows.None;
                }
            }

            data.PlayerSpawn = new Vector3(0f, 1.2f, -15f);
            FillSpawns(data, 18f, 8f, def.EnemyCount, rnd);
        }

        // =====================================================================
        //  Общие объекты
        // =====================================================================

        private static void BuildPark(Vector3 center, Transform root, System.Random rnd)
        {
            MaterialFactory.Box("ParkGrass", center + new Vector3(0f, 0.08f, 0f), new Vector3(18f, 0.16f, 18f), new Color32(108, 162, 74, 255), root, false);
            MaterialFactory.Box("Pond", center + new Vector3(4f, 0.2f, 4f), new Vector3(7f, 0.2f, 5f), new Color32(76, 140, 176, 255), root, false);

            for (int i = 0; i < 6; i++)
            {
                Vector3 p = center + new Vector3(-7f + i * 2.8f, 0f, -6f);
                BuildTree(p, root, rnd);
            }

            // Фонтан
            Vector3 f = center + new Vector3(0f, 0f, 0f);
            MaterialFactory.Cylinder("FountainBase", f + new Vector3(0f, 0.3f, 0f), 5.4f, 0.6f, new Color32(190, 194, 200, 255), root);
            MaterialFactory.Cylinder("FountainWater", f + new Vector3(0f, 0.62f, 0f), 4.8f, 0.1f, new Color32(96, 168, 200, 255), root, false);
            MaterialFactory.Cylinder("FountainColumn", f + new Vector3(0f, 1.1f, 0f), 0.8f, 1.6f, new Color32(190, 194, 200, 255), root);

            // Скамейки
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = center + new Vector3(-6f + i * 4f, 0f, 6.5f);
                MaterialFactory.Box("Bench", p + new Vector3(0f, 0.55f, 0f), new Vector3(2.6f, 0.14f, 0.7f), new Color32(138, 96, 58, 255), root);
                MaterialFactory.Box("BenchBack", p + new Vector3(0f, 0.95f, 0.3f), new Vector3(2.6f, 0.8f, 0.12f), new Color32(138, 96, 58, 255), root);
            }
        }

        private static void BuildHouse(Vector3 pos, Transform root, System.Random rnd, int index)
        {
            float w = 7f + (float)rnd.NextDouble() * 3f;
            float d = 6f + (float)rnd.NextDouble() * 3f;
            float h = 4f;
            Color wall = PaletteFor(rnd, index + 30);
            MaterialFactory.Box("House", pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall, root);
            // Крыша — две наклонные плоскости
            GameObject roofA = MaterialFactory.Box("RoofA", pos + new Vector3(0f, h + 1.1f, -d * 0.25f), new Vector3(w + 1f, 0.3f, d * 0.62f), new Color32(150, 74, 56, 255), root);
            roofA.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
            GameObject roofB = MaterialFactory.Box("RoofB", pos + new Vector3(0f, h + 1.1f, d * 0.25f), new Vector3(w + 1f, 0.3f, d * 0.62f), new Color32(150, 74, 56, 255), root);
            roofB.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            // Дверь и окна
            MaterialFactory.Box("Door", pos + new Vector3(0f, 1.1f, -d * 0.5f - 0.05f), new Vector3(1.2f, 2.2f, 0.16f), new Color32(96, 64, 44, 255), root, false);
            MaterialFactory.Box("Window", pos + new Vector3(w * 0.3f, 2.3f, -d * 0.5f - 0.05f), new Vector3(1.3f, 1.2f, 0.12f), new Color32(160, 200, 220, 255), root, false);
            // Труба
            MaterialFactory.Cylinder("Chimney", pos + new Vector3(w * 0.3f, h + 2f, 0f), 0.6f, 1.6f, new Color32(120, 92, 80, 255), root);
        }

        private static void BuildBarn(Vector3 pos, Transform root)
        {
            // Доски — процедурная текстура, чтобы стена не была однотонной заливкой.
            Texture2D planks = MaterialFactory.StripeTexture(32,
                new Color32(148, 56, 48, 255), new Color32(172, 72, 58, 255), 7);
            Material wallMat = MaterialFactory.Textured(new Color32(158, 62, 52, 255), planks,
                new Vector2(4f, 2f), 0.05f);

            MaterialFactory.Box("Barn", pos + new Vector3(0f, 2.2f, 0f), new Vector3(11f, 4.4f, 8f), wallMat, root);
            GameObject roof = MaterialFactory.Box("BarnRoof", pos + new Vector3(0f, 5.3f, 0f), new Vector3(11.6f, 0.35f, 8.6f), new Color32(110, 48, 42, 255), root);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            MaterialFactory.Box("BarnDoor", pos + new Vector3(0f, 1.6f, -4.05f), new Vector3(3.6f, 3.2f, 0.2f), new Color32(238, 234, 220, 255), root, false);
            MaterialFactory.Box("BarnCross1", pos + new Vector3(0f, 1.6f, -4.16f), new Vector3(3.7f, 0.2f, 0.1f), new Color32(158, 62, 52, 255), root, false);
            MaterialFactory.Box("BarnCross2", pos + new Vector3(0f, 1.6f, -4.16f), new Vector3(0.2f, 3.3f, 0.1f), new Color32(158, 62, 52, 255), root, false);
        }

        private static void BuildWindmill(Vector3 pos, Transform root)
        {
            MaterialFactory.Cylinder("Tower", pos + new Vector3(0f, 4f, 0f), 3.2f, 8f, new Color32(206, 196, 176, 255), root);
            GameObject top = MaterialFactory.Cone("Cap", pos + new Vector3(0f, 8.6f, 0f), 3.4f, 1.6f, new Color32(140, 74, 56, 255), root);
            top.transform.localRotation = Quaternion.identity;

            var blades = new GameObject("Blades");
            blades.transform.SetParent(root, false);
            blades.transform.localPosition = pos + new Vector3(0f, 7.4f, -1.8f);

            for (int i = 0; i < 4; i++)
            {
                GameObject blade = MaterialFactory.Box("Blade", Vector3.zero, new Vector3(0.6f, 5.4f, 0.12f), new Color32(238, 232, 216, 255), blades.transform);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                blade.transform.localPosition = Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0f, 2.7f, 0f);
            }
            var spin = blades.AddComponent<Spin>();
            spin.axis = Vector3.forward;
            spin.speed = 22f;
        }

        private static void BuildFenceLine(Vector3 start, Vector3 dir, Transform root)
        {
            float len = dir.magnitude;
            if (len < 0.1f) return;
            Vector3 d = dir.normalized;

            // Дерево забора — с текстурой досок.
            Texture2D wood = MaterialFactory.NoiseTexture(32,
                new Color32(112, 84, 52, 255), new Color32(152, 118, 74, 255), 0.4f, 4555);
            Material postMat = MaterialFactory.Textured(new Color32(128, 96, 60, 255), wood, new Vector2(1f, 2f), 0.05f);
            Material railMat = MaterialFactory.Textured(new Color32(140, 108, 68, 255), wood, new Vector2(3f, 1f), 0.05f);

            int posts = Mathf.Max(2, Mathf.RoundToInt(len / 2.4f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = start + d * (len * i / posts);
                MaterialFactory.Box("FencePost", p + new Vector3(0f, 0.7f, 0f), new Vector3(0.18f, 1.4f, 0.18f), postMat, root);
            }
            for (int i = 0; i < posts; i++)
            {
                Vector3 mid = start + d * (len * (i + 0.5f) / posts);
                Vector3 size = Mathf.Abs(d.x) > Mathf.Abs(d.z)
                    ? new Vector3(len / posts, 0.12f, 0.1f)
                    : new Vector3(0.1f, 0.12f, len / posts);
                MaterialFactory.Box("FenceRail", mid + new Vector3(0f, 1.05f, 0f), size, railMat, root);
            }
        }

        private static void BuildWell(Vector3 pos, Transform root)
        {
            MaterialFactory.Cylinder("WellBase", pos + new Vector3(0f, 0.5f, 0f), 2f, 1f, new Color32(150, 146, 140, 255), root);
            MaterialFactory.Cylinder("WellWater", pos + new Vector3(0f, 1.0f, 0f), 1.6f, 0.1f, new Color32(70, 120, 160, 255), root, false);
            MaterialFactory.Box("PostL", pos + new Vector3(-0.8f, 1.6f, 0f), new Vector3(0.16f, 1.4f, 0.16f), new Color32(120, 88, 56, 255), root);
            MaterialFactory.Box("PostR", pos + new Vector3(0.8f, 1.6f, 0f), new Vector3(0.16f, 1.4f, 0.16f), new Color32(120, 88, 56, 255), root);
            MaterialFactory.Box("Roof", pos + new Vector3(0f, 2.5f, 0f), new Vector3(2.6f, 0.2f, 2.2f), new Color32(146, 76, 56, 255), root);
        }

        private static void BuildTree(Vector3 pos, Transform root, System.Random rnd)
        {
            float h = 2.4f + (float)rnd.NextDouble() * 2.2f;
            MaterialFactory.Cylinder("Trunk", pos + new Vector3(0f, h * 0.5f, 0f), 0.55f, h, new Color32(112, 80, 52, 255), root);
            GameObject foliage = MaterialFactory.Sphere("Foliage", pos + new Vector3(0f, h + 0.7f, 0f), 3.2f + (float)rnd.NextDouble(), new Color32(74, 132, 56, 255), root);
            foliage.transform.localScale = new Vector3(1f, 0.85f, 1f);
            GameObject foliage2 = MaterialFactory.Sphere("Foliage2", pos + new Vector3(0.7f, h + 0.2f, 0.3f), 2.2f, new Color32(92, 152, 66, 255), root);
            foliage2.transform.localScale = new Vector3(1f, 0.8f, 1f);
        }

        private static void BuildCar(Vector3 pos, Transform root, System.Random rnd, int index)
        {
            Color[] colors =
            {
                new Color32(206, 74, 66, 255), new Color32(64, 118, 198, 255), new Color32(236, 220, 120, 255),
                new Color32(64, 168, 122, 255), new Color32(226, 226, 232, 255), new Color32(74, 78, 88, 255)
            };
            Color c = colors[index % colors.Length];
            var car = new GameObject("Car");
            car.transform.SetParent(root, false);
            car.transform.localPosition = pos;

            MaterialFactory.Box("Body", pos + new Vector3(0f, 0.55f, 0f), new Vector3(2f, 0.75f, 4.2f), c, car.transform);
            MaterialFactory.Box("Cabin", pos + new Vector3(0f, 1.15f, -0.2f), new Vector3(1.8f, 0.6f, 2.1f), new Color32(58, 72, 88, 255), car.transform, false);
            MaterialFactory.Box("BumperF", pos + new Vector3(0f, 0.42f, 2.15f), new Vector3(1.9f, 0.2f, 0.2f), new Color32(220, 220, 226, 255), car.transform, false);
            MaterialFactory.Box("BumperB", pos + new Vector3(0f, 0.42f, -2.15f), new Vector3(1.9f, 0.2f, 0.2f), new Color32(220, 220, 226, 255), car.transform, false);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1f : 1f;
                float z = (i < 2) ? 1.3f : -1.3f;
                MaterialFactory.Cylinder("Wheel", pos + new Vector3(x, 0.42f, z), 0.8f, 0.3f, new Color32(26, 26, 30, 255), car.transform)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void BuildStreetLamp(Vector3 pos, Transform root, bool flip)
        {
            MaterialFactory.Cylinder("Pole", pos + new Vector3(0f, 2.6f, 0f), 0.18f, 5.2f, new Color32(62, 68, 76, 255), root);
            GameObject arm = MaterialFactory.Box("Arm", pos + new Vector3(flip ? 0.7f : -0.7f, 5.1f, 0f), new Vector3(1.5f, 0.12f, 0.12f), new Color32(62, 68, 76, 255), root);
            arm.transform.localRotation = Quaternion.identity;
            Material lamp = MaterialFactory.Emissive(new Color32(250, 244, 210, 255), new Color32(255, 240, 190, 255), 1.8f);
            GameObject head = MaterialFactory.Box("LampHead", pos + new Vector3(flip ? 1.3f : -1.3f, 5f, 0f), new Vector3(0.7f, 0.22f, 0.36f), Color.white, root, false);
            head.GetComponent<MeshRenderer>().sharedMaterial = lamp;
        }

        private static void BuildBillboard(Vector3 pos, Transform root)
        {
            MaterialFactory.Cylinder("Post", pos + new Vector3(0f, 1.8f, 0f), 0.3f, 3.6f, new Color32(80, 86, 94, 255), root);
            GameObject board = MaterialFactory.Box("Board", pos + new Vector3(0f, 4f, 0f), new Vector3(7f, 3.2f, 0.25f), new Color32(38, 132, 56, 255), root);
            GameObject logo = MaterialFactory.Sphere("Logo", pos + new Vector3(0f, 4f, -0.2f), 2.2f, new Color32(122, 196, 40, 255), root, false);
            logo.transform.localScale = new Vector3(1f, 1f, 0.16f);
            if (board == null) return;
        }

        private static void BuildCubicle(Vector3 pos, Transform root, System.Random rnd)
        {
            // Стол
            MaterialFactory.Box("Desk", pos + new Vector3(0f, 0.78f, 0f), new Vector3(3.6f, 0.12f, 1.7f), new Color32(166, 138, 102, 255), root);
            MaterialFactory.Box("DeskLeg", pos + new Vector3(0f, 0.38f, 0f), new Vector3(3.2f, 0.76f, 1.3f), new Color32(120, 96, 70, 255), root);
            // Монитор
            MaterialFactory.Box("Monitor", pos + new Vector3(0f, 1.35f, 0.3f), new Vector3(1.5f, 0.9f, 0.1f), new Color32(26, 28, 34, 255), root);
            MaterialMaterialScreen(pos, root);
            // Кресло
            MaterialFactory.Box("Chair", pos + new Vector3(0f, 0.5f, -1.2f), new Vector3(0.9f, 0.14f, 0.9f), new Color32(46, 52, 62, 255), root);
            MaterialFactory.Box("ChairBack", pos + new Vector3(0f, 0.95f, -1.6f), new Vector3(0.9f, 0.9f, 0.12f), new Color32(46, 52, 62, 255), root);
            // Перегородки
            MaterialFactory.Box("PartitionL", pos + new Vector3(-2.1f, 0.9f, -0.2f), new Vector3(0.1f, 1.8f, 3.2f), new Color32(126, 148, 156, 255), root);
            MaterialFactory.Box("PartitionR", pos + new Vector3(2.1f, 0.9f, -0.2f), new Vector3(0.1f, 1.8f, 3.2f), new Color32(126, 148, 156, 255), root);
            // Кулер / стойка
            if (rnd.NextDouble() < 0.4)
                MaterialFactory.Box("PC", pos + new Vector3(1.3f, 0.5f, 0.4f), new Vector3(0.5f, 0.9f, 0.7f), new Color32(34, 38, 44, 255), root);
        }

        private static void MaterialMaterialScreen(Vector3 pos, Transform root)
        {
            Material screen = MaterialFactory.Emissive(new Color32(90, 170, 220, 255), new Color32(120, 200, 255, 255), 1.5f);
            GameObject s = MaterialFactory.Box("Screen", pos + new Vector3(0f, 1.35f, 0.23f), new Vector3(1.35f, 0.75f, 0.04f), Color.white, root, false);
            s.GetComponent<MeshRenderer>().sharedMaterial = screen;
        }

        private static void BuildPlant(Vector3 pos, Transform root)
        {
            MaterialFactory.Cylinder("Pot", pos + new Vector3(0f, 0.3f, 0f), 0.8f, 0.6f, new Color32(178, 118, 92, 255), root);
            GameObject leaf = MaterialFactory.Sphere("Leaf", pos + new Vector3(0f, 1.1f, 0f), 1.5f, new Color32(64, 132, 72, 255), root);
            leaf.transform.localScale = new Vector3(1f, 1.2f, 1f);
        }

        // =====================================================================
        //  Свет и окружение
        // =====================================================================

        private static void SetupSun(Color color, float intensity, float shadowDistance, bool shadows)
        {
            var lightGo = new GameObject("Sun");
            lightGo.transform.rotation = Quaternion.Euler(48f, 32f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.72f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = color * 0.55f;
            RenderSettings.ambientEquatorColor = new Color32(110, 116, 120, 255);
            RenderSettings.ambientGroundColor = new Color32(70, 66, 60, 255);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color32(160, 186, 200, 255);
        }

        /// <summary>
        /// Туман под тему уровня. Раньше цвет дымки задавался внутри SetupSun
        /// для всех уровней одинаковым (холодный серо-голубой) — из-за чего
        /// пшеничное поле выглядело как город в дымке.
        /// </summary>
        private static void SetupFog(Color color, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = Mathf.Max(0f, density);
        }

        private static void SetupAmbient(Color sky, Color ground, float intensity)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = sky * intensity;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0.4f;
        }

        private static void FillSpawns(LevelData data, float radius, float minDistance, int count, System.Random rnd)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = minDistance + (float)rnd.NextDouble() * Mathf.Max(1f, radius - minDistance);
                Vector3 p = data.PlayerSpawn + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                data.EnemySpawns.Add(p);
            }
        }

        private static Color PaletteFor(System.Random rnd, int index)
        {
            Color[] palette =
            {
                new Color32(176, 172, 166, 255),
                new Color32(150, 158, 168, 255),
                new Color32(186, 162, 138, 255),
                new Color32(140, 156, 150, 255),
                new Color32(198, 186, 168, 255),
                new Color32(162, 146, 168, 255),
            };
            return palette[index % palette.Length];
        }

        /// <summary>Материал стены с окнами (часть окон светится).</summary>
        private static Material WindowMaterial(Color wall, Color glass, float litRatio, int seed)
        {
            const int size = 128;
            var rnd = new System.Random(seed * 977 + 13);
            var baseCanvas = new PixelCanvas(size, size);
            var emitCanvas = new PixelCanvas(size, size);

            baseCanvas.FillRect(0, 0, size, size, wall);
            emitCanvas.FillRect(0, 0, size, size, Color.black);

            int cols = 4, rows = 4;
            int cw = size / cols, ch = size / rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    bool lit = rnd.NextDouble() < litRatio;
                    int px = x * cw + cw / 4 + 2;
                    int py = y * ch + ch / 4 + 2;
                    int pw = cw / 2 - 4;
                    int ph = ch / 2 - 4;
                    Color c = lit ? new Color32(255, 226, 150, 255) : glass;
                    baseCanvas.FillRect(px, py, pw, ph, c);
                    baseCanvas.StrokeRect(px, py, pw, ph, 2, wall * 0.85f);
                    if (lit) emitCanvas.FillRect(px, py, pw, ph, new Color32(255, 226, 150, 255));
                }
            }

            Texture2D baseTex = baseCanvas.ToTexture();
            Texture2D emitTex = emitCanvas.ToTexture();

            var mat = new Material(MaterialFactory.LitShader) { name = "BuildingMat" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emitTex);
                mat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.62f));
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeIndirectEmission;
            }
            return mat;
        }
    }
}
