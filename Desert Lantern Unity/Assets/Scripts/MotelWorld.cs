using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public const int SolidLayer = 8;
        public const int GroundLayer = 9;
        public static readonly Vector3 Spawn = new Vector3(0, 0, -8);
        public static readonly Vector3 OfficeApproach = new Vector3(-9.75f, 0, 3.4f);
        public static readonly Vector3 GuestSpawn = new Vector3(-9.57f, 0, 5.5f);
        public static readonly Vector3 OfficeSign = new Vector3(-9.55f, 4.1f, 3.60f);
        public static readonly float[] RoomX = { -4f, 1f, 6f, 11f };
        public static Vector3 RoomApproach(int room) => new Vector3(RoomX[room], 0, 5.75f);
        public static Vector3 RoomSign(int room) => new Vector3(RoomX[room], 2.9f, 6.48f);

        private readonly Transform root;
        private readonly Shader shader;
        private readonly Font signFont;
        private readonly Material signMaterial;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Material sand, asphalt, plaster, wood, teal, roof, window, cream, iron;

        public MotelWorld(Transform parent, bool overview)
        {
            root = new GameObject("Desert Lantern Property").transform;
            root.SetParent(parent, false);
            shader = Resources.Load<Shader>("MotelModel");
            signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signMaterial = new Material(Resources.Load<Shader>("MotelSign"));
            materials.Add(signMaterial);
            RefreshSignAtlas(signFont);
            Font.textureRebuilt += RefreshSignAtlas;
            sand = Surface(new Color32(139, 136, 107, 255), .58f, 3);
            asphalt = Surface(new Color32(70, 74, 75, 255), .83f, 7);
            plaster = Surface(new Color32(174, 158, 132, 255), .57f, 5);
            wood = Surface(new Color32(93, 69, 49, 255), .6f, 7);
            teal = Surface(new Color32(73, 104, 94, 255), .62f, 10);
            roof = Surface(new Color32(60, 61, 57, 255), .44f, 7);
            window = Surface(new Color32(58, 61, 55, 255), .26f, 5);
            cream = Surface(new Color32(232, 185, 111, 255), .38f, 10);
            iron = Surface(new Color32(102, 87, 68, 255), .78f, 8);

            CreateFinishes();
            Lighting(overview);
            Box("Desert floor", new Vector3(0, -.32f, 0), new Vector3(760, .6f, 760), sand);
            Box("Parking lot", new Vector3(0, -.14f, -2.4f), new Vector3(34, .28f, 25), asphalt, GroundLayer);
            Box("Highway", new Vector3(0, -.075f, -17), new Vector3(330, .16f, 5.3f), asphalt);
            for (int i = -20; i <= 20; i++)
                Box("Road center stripe", new Vector3(i * 7, .009f, -17), new Vector3(2.8f, .009f, .09f), cream);

            for (int i = 0; i < RoomX.Length; i++)
                BuildRoom(i, overview);
            BuildUpperWalkway();
            BuildReception(overview);
            BuildRoadsideFeatures();
            BuildDesert();
            BuildPlanting();
            BuildParking(overview);
            if (!overview)
            {
                BuildSupplyDelivery();
                BuildOutdoorLights();
                BuildDeskPhone();
            }
        }

        private void BuildDesert()
        {
            Material rock = Surface(new Color32(128, 133, 111, 255), .85f, .45f);
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

        private TextMesh Sign(string text, Vector3 position, float size, Color color)
        {
            var sign = new GameObject(text);
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = position;
            TextMesh mesh = sign.AddComponent<TextMesh>();
            mesh.font = signFont;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = signMaterial;
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 86;
            mesh.characterSize = size * .115f;
            mesh.color = color;
            return mesh;
        }

        private void RefreshSignAtlas(Font rebuilt)
        {
            if (rebuilt == signFont)
                signMaterial.mainTexture = rebuilt.material.mainTexture;
        }

        private Light daylightSun;
        private Transform clockHour;
        private Transform clockMinuteHand;
        private int lastShownMinute = -1;

        public void ShowClock(int absoluteMinute, Camera camera)
        {
            if (absoluteMinute == lastShownMinute)
                return;
            lastShownMinute = absoluteMinute;
            float hour = absoluteMinute % MotelService.MinutesPerDay / 60f;
            float elevation = Mathf.Sin((hour - 6f) * Mathf.PI / 12f);
            float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.14f, .36f, elevation));
            float warm = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 3f) * daylight;
            daylightSun.transform.rotation = Quaternion.Euler(Mathf.Max(4f, elevation * 58f), hour * 15f - 210f, 0);
            daylightSun.intensity = 1.12f * daylight;
            daylightSun.color = Color.Lerp(new Color(1f, .95f, .84f), new Color(1f, .63f, .38f), warm);
            RenderSettings.ambientLight = Color.Lerp(new Color(.20f, .23f, .34f),
                new Color(.56f, .58f, .57f), daylight);
            Color sky = Color.Lerp(new Color(.075f, .105f, .19f), new Color(.66f, .71f, .72f), daylight);
            camera.backgroundColor = Color.Lerp(sky, new Color(.81f, .49f, .36f), warm * .55f);
            RenderSettings.fogColor = camera.backgroundColor;
            foreach (Light light in outdoorLights)
                light.intensity = (1f - daylight) * 2.9f;
            clockHour.localRotation = Quaternion.Euler(0, 0, -hour * 30f);
            clockMinuteHand.localRotation = Quaternion.Euler(0, 0, -(absoluteMinute % 60) * 6f);
        }

        private void Lighting(bool overview)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.56f, .58f, .57f);
            RenderSettings.fog = !overview;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.63f, .70f, .71f);
            RenderSettings.fogStartDistance = 32;
            RenderSettings.fogEndDistance = 175;
            var sun = new GameObject("Desert sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(45, -31, 0);
            sun.type = LightType.Directional;
            sun.color = new Color(1, .92f, .78f);
            sun.intensity = 1.12f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;
            RenderSettings.sun = daylightSun = sun;
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
            Font.textureRebuilt -= RefreshSignAtlas;
            foreach (Material material in materials)
                Object.Destroy(material);
            foreach (Mesh mesh in meshes)
                Object.Destroy(mesh);
        }
    }
}
