using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesertLantern
{
    internal sealed class MotelWorld
    {
        public const int SolidLayer = 8;
        public const int GroundLayer = 9;
        public static readonly Vector3 Spawn = new Vector3(0, 0, -8);
        public static readonly Vector3 OfficeApproach = new Vector3(-9.75f, 0, 3.4f);
        public static readonly Vector3 GuestSpawn = new Vector3(-9.57f, 0, 5.5f);
        public static readonly Vector3 OfficeSign = new Vector3(-9.4f, 3.5f, 4.02f);
        public static readonly float[] RoomX = { -4f, 1f, 6f, 11f };
        public static Vector3 RoomApproach(int room) => new Vector3(RoomX[room], 0, 5.75f);
        public static Vector3 RoomSign(int room) => new Vector3(RoomX[room], 2.9f, 6.48f);

        private readonly Transform root;
        private readonly Shader shader;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Material sand, asphalt, plaster, wood, teal, roof, window, cream, iron;

        public MotelWorld(Transform parent, bool overview)
        {
            root = new GameObject("Desert Lantern Property").transform;
            root.SetParent(parent, false);
            shader = Resources.Load<Shader>("MotelModel");
            sand = Surface(new Color32(172, 130, 86, 255), .65f, 3);
            asphalt = Surface(new Color32(76, 69, 61, 255), 1, 7);
            plaster = Surface(new Color32(183, 156, 118, 255), .76f, 5);
            wood = Surface(new Color32(93, 69, 49, 255), .6f, 7);
            teal = Surface(new Color32(73, 104, 94, 255), .62f, 10);
            roof = Surface(new Color32(102, 72, 49, 255), .7f, 7);
            window = Surface(new Color32(58, 61, 55, 255), .26f, 5);
            cream = Surface(new Color32(232, 185, 111, 255), .38f, 10);
            iron = Surface(new Color32(102, 87, 68, 255), .78f, 8);

            Lighting(overview);
            Box("Desert floor", new Vector3(0, -.32f, 0), new Vector3(760, .6f, 760), sand);
            Box("Parking lot", new Vector3(0, -.14f, -2.4f), new Vector3(34, .28f, 25), asphalt, GroundLayer);
            Box("Highway", new Vector3(0, -.075f, -17), new Vector3(330, .16f, 5.3f), asphalt);
            for (int i = -20; i <= 20; i++)
                Box("Road center stripe", new Vector3(i * 7, .009f, -17), new Vector3(2.8f, .009f, .09f), cream);

            for (int i = 0; i < RoomX.Length; i++)
                BuildRoomWing(RoomX[i], i + 1);
            Box("Continuous canopy", new Vector3(3.5f, 3.10f, 5.70f),
                new Vector3(20.5f, .2f, 2.15f), roof);
            Box("Canopy fascia", new Vector3(3.5f, 3.13f, 4.60f),
                new Vector3(20.5f, .25f, .16f), teal);
            for (int i = 0; i < 5; i++)
            {
                float x = -6.5f + i * 5;
                Box("Porch column", new Vector3(x, 1.48f, 4.78f), new Vector3(.135f, 2.96f, .16f), teal, SolidLayer);
                Box("Parking bay stripe", new Vector3(x, .006f, 1.54f), new Vector3(.065f, .01f, 4.7f), cream);
            }

            BuildOffice();
            Box("Roadside sign post", new Vector3(-14, 2, -1.2f), new Vector3(.20f, 4, .22f), iron, SolidLayer);
            Box("Roadside sign", new Vector3(-14, 4.05f, -1.2f), new Vector3(3, 1.35f, .28f), roof);
            Sign("MOTEL", new Vector3(-14, 4.24f, -1.35f), .45f, new Color32(255, 221, 154, 255));
            Sign("DESERT LANTERN", new Vector3(-14, 3.84f, -1.35f), .145f, Color.white);
            BuildDesert();
        }

        private void BuildRoomWing(float x, int number)
        {
            Box("Room " + number + " structure", new Vector3(x, 1.49f, 8.7f), new Vector3(4.99f, 3, 4.4f), plaster, SolidLayer);
            Box("Flat roof", new Vector3(x, 3.03f, 8.6f), new Vector3(5.05f, .19f, 4.47f), roof);
            Box("Weathered base trim", new Vector3(x, .13f, 6.46f), new Vector3(4.99f, .25f, .15f), wood);
            Box("Door frame", new Vector3(x, 1.07f, 6.43f), new Vector3(1.09f, 2.14f, .15f), wood);
            Box("Room " + number + " door", new Vector3(x, 1.05f, 6.34f), new Vector3(.94f, 2.05f, .10f), teal, SolidLayer);
            for (int row = 0; row < 2; row++)
                Box("Recessed door panel", new Vector3(x, .48f + row * .83f, 6.28f),
                    new Vector3(.64f, .60f, .027f), teal);
            Box("Door handle", new Vector3(x + .34f, .99f, 6.255f), new Vector3(.13f, .048f, .065f), cream);
            Sign(number.ToString("00"), new Vector3(x, 1.75f, 6.265f), .16f, Color.white);
            Box("Window frame", new Vector3(x + 1.45f, 1.57f, 6.41f), new Vector3(1.51f, 1.17f, .15f), wood);
            Box("Window glass", new Vector3(x + 1.45f, 1.57f, 6.31f), new Vector3(1.33f, 1.02f, .07f), window);
            Box("Window mullion", new Vector3(x + 1.45f, 1.57f, 6.255f), new Vector3(.06f, 1.06f, .07f), iron);
            Box("Window air conditioner", new Vector3(x + 1.45f, .63f, 6.39f),
                new Vector3(.81f, .4f, .27f), iron);
            for (int slat = 0; slat < 4; slat++)
                Box("Vent slot", new Vector3(x + 1.45f, .52f + slat * .065f, 6.248f),
                    new Vector3(.63f, .018f, .018f), window);
        }

        private void BuildOffice()
        {
            Box("Office floor", new Vector3(-9.65f, -.038f, 6.42f),
                new Vector3(4.7f, .09f, 4.68f), wood, GroundLayer);
            Box("Office back wall", new Vector3(-9.65f, 1.45f, 8.7f), new Vector3(4.8f, 2.9f, .2f), plaster, SolidLayer);
            Box("Office west wall", new Vector3(-12, 1.45f, 6.41f), new Vector3(.20f, 2.9f, 4.7f), plaster, SolidLayer);
            Box("Office east wall", new Vector3(-7.31f, 1.45f, 6.41f), new Vector3(.20f, 2.9f, 4.7f), plaster, SolidLayer);
            Box("Office front left", new Vector3(-11.27f, 1.45f, 4.10f), new Vector3(1.65f, 2.9f, .20f), plaster, SolidLayer);
            Box("Office front right", new Vector3(-8.14f, 1.45f, 4.10f), new Vector3(1.86f, 2.9f, .20f), plaster, SolidLayer);
            Box("Office doorway header", new Vector3(-9.75f, 2.60f, 4.1f), new Vector3(1.45f, .60f, .22f), teal, SolidLayer);
            Box("Office window", new Vector3(-8.24f, 1.56f, 3.965f), new Vector3(1.15f, .86f, .10f), window);
            Box("Office roof", new Vector3(-9.65f, 2.98f, 6.37f), new Vector3(5.03f, .24f, 5.04f), roof, SolidLayer);
            Box("Office fascia", new Vector3(-9.65f, 2.97f, 3.83f), new Vector3(5.02f, .37f, .16f), teal);
            Sign("FRONT OFFICE", new Vector3(-9.65f, 2.97f, 3.731f), .25f, Color.white);
            Box("Reception counter base", new Vector3(-9.37f, .50f, 7.43f), new Vector3(2.87f, 1, .70f), wood, SolidLayer);
            Box("Reception counter top", new Vector3(-9.37f, 1.04f, 7.43f), new Vector3(3, .1f, .86f), teal, SolidLayer);
            Box("Guest register", new Vector3(-9.31f, 1.105f, 7.22f), new Vector3(.46f, .023f, .34f), cream);
            Box("Office rug", new Vector3(-9.72f, .015f, 5.2f), new Vector3(1.30f, .009f, 1.56f), roof);
        }

        private void BuildDesert()
        {
            Material rock = Surface(new Color32(160, 115, 79, 255), .85f, .45f);
            Material cactus = Surface(new Color32(79, 103, 63, 255), .48f, 4);
            var random = new System.Random(138);
            for (int i = 0; i < 21; i++)
            {
                float degrees = i * 17f + 8f;
                float angle = degrees * Mathf.Deg2Rad;
                float distance = 43 + random.Next(12, 87);
                float height = random.Next(6, 25);
                Ridge(new Vector3(Mathf.Cos(angle) * distance, -.04f, Mathf.Sin(angle) * distance),
                    random.Next(14, 33), height, rock, random.Next());
            }

            for (int i = 0; i < 33; i++)
            {
                float x = random.Next(-59, 60);
                float z = random.Next(-59, 64);
                if (Mathf.Abs(z + 17) < 4 || (Mathf.Abs(x) < 20 && z > -15 && z < 14))
                    continue;

                float h = .65f + (float)random.NextDouble() * 1.75f;
                Box("Desert cactus", new Vector3(x, h / 2, z), new Vector3(.22f, h, .22f), cactus);
                float side = i % 2 == 0 ? -.23f : .23f;
                Box("Cactus arm", new Vector3(x + side, h * .66f, z), new Vector3(.19f, h * .41f, .19f), cactus);
                Box("Cactus elbow", new Vector3(x + side / 2, h * .49f, z), new Vector3(.40f, .18f, .20f), cactus);
            }
        }

        private void Ridge(Vector3 position, float radius, float height, Material material, int seed)
        {
            const int sides = 9;
            var random = new System.Random(seed);
            var vertices = new Vector3[sides * 3 + 1];
            for (int layer = 0; layer < 3; layer++)
            {
                float scale = layer == 0 ? 1f : layer == 1 ? .68f : .32f;
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * (2 * Mathf.PI / sides);
                    float r = radius * scale * (.72f + (float)random.NextDouble() * .52f);
                    float y = height * (.43f * layer + (float)random.NextDouble() * .10f);
                    vertices[layer * sides + i] = new Vector3(Mathf.Cos(angle) * r, y,
                        Mathf.Sin(angle) * r * .68f);
                }
            }
            vertices[sides * 3] = new Vector3(.1f * radius, height, 0);
            var indices = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = layer * sides + i;
                    int b = layer * sides + (i + 1) % sides;
                    indices.AddRange(new[] { a, a + sides, b + sides, a, b + sides, b });
                }
            }
            for (int i = 0; i < sides; i++)
                indices.AddRange(new[] { 2 * sides + i, 3 * sides, 2 * sides + (i + 1) % sides });
            // Duplicate triangle vertices for the faceted faces of the desert ridges.
            var flat = new List<Vector3>(indices.Count);
            for (int i = 0; i < indices.Count; i++)
                flat.Add(vertices[indices[i]]);
            var triangles = new int[flat.Count];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = i;
            var mesh = new Mesh { name = "Desert ridge" };
            mesh.SetVertices(flat);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            meshes.Add(mesh);

            var ridge = new GameObject("Desert ridge");
            ridge.transform.SetParent(root, false);
            ridge.transform.localPosition = position;
            ridge.AddComponent<MeshFilter>().sharedMesh = mesh;
            ridge.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Sign(string text, Vector3 position, float size, Color color)
        {
            var sign = new GameObject(text);
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = position;
            TextMesh mesh = sign.AddComponent<TextMesh>();
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 86;
            mesh.characterSize = size * .115f;
            mesh.color = color;
        }

        private void Lighting(bool overview)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .52f, .48f);
            RenderSettings.fog = !overview;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.69f, .61f, .49f);
            RenderSettings.fogStartDistance = 32;
            RenderSettings.fogEndDistance = 175;
            var sun = new GameObject("Desert sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(32, -32, 0);
            sun.type = LightType.Directional;
            sun.color = new Color(1, .84f, .66f);
            sun.intensity = 1.14f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;
            RenderSettings.sun = sun;
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, int layer = 0)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.layer = layer;
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            piece.GetComponent<BoxCollider>().enabled = layer != 0;
            return piece;
        }

        private Material Surface(Color color, float wear, float grain)
        {
            var material = new Material(shader) { color = color };
            material.SetFloat("_Wear", wear);
            material.SetFloat("_Grain", grain);
            materials.Add(material);
            return material;
        }

        public void Dispose()
        {
            foreach (Material material in materials)
                Object.Destroy(material);
            foreach (Mesh mesh in meshes)
                Object.Destroy(mesh);
        }
    }
}
