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
        public const float UpperFloor = 3.08f;
        public static readonly float[] RoomX = { -4f, 1f, 6f, 11f, -4f, 1f, 6f, 11f, 19.5f, 24.5f, 19.5f, 24.5f };
        public static readonly Vector3 StairFoot = new Vector3(14.83f, 0, .08f);
        public static readonly Vector3 StairTop = new Vector3(14.83f, UpperFloor, 5.75f);
        public static float FloorHeight(int room) => (room >= 4 && room < 8) || room >= 10 ? UpperFloor : 0f;
        public static bool NearFloor(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < .75f;
        public static bool OnStairs(Vector3 p) => p.x > 13.8f && p.x < 15.9f && p.z > .08f && p.z < 5.69f;
        public static Vector3 RoomApproach(int room) => new Vector3(RoomX[room], FloorHeight(room), 5.75f);
        public static Vector3 RoomSign(int room) => new Vector3(RoomX[room], FloorHeight(room) + 2.9f, 6.48f);
        private Vector3 roomBuildOffset;

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
            sand = Surface(new Color32(151, 136, 112, 255), .58f, 3);
            asphalt = Surface(new Color32(91, 81, 75, 255), .44f, 43, .08f);
            plaster = Surface(new Color32(178, 164, 141, 255), .36f, 22, .16f);
            wood = Surface(new Color32(97, 74, 55, 255), .43f, 15, .24f, grainStretch: .18f);
            teal = Surface(new Color32(49, 91, 85, 255), .49f, 20, .21f);
            roof = Surface(new Color32(75, 64, 55, 255), .57f, 15);
            roof.SetFloat("_RoofCourses", 1);
            window = Surface(new Color32(52, 61, 63, 255), .14f, 9, .75f);
            cream = Surface(new Color32(232, 185, 111, 255), .38f, 10);
            iron = Surface(new Color32(102, 87, 68, 255), .78f, 8);

            ApplySurfaceDetail(asphalt, "Asphalt", .46f, .90f);
            ApplySurfaceDetail(plaster, "Plaster", .34f, .62f);
            ApplySurfaceDetail(wood, "Wood", 1.20f, .76f);
            CreateFinishes();
            Lighting(overview);
            Box("Desert floor", new Vector3(0, -.32f, 0), new Vector3(760, .6f, 760), sand);
            Box("Parking lot", new Vector3(0, -.14f, -2.4f), new Vector3(34, .28f, 25), asphalt, GroundLayer);
            Box("Highway", new Vector3(0, -.075f, -17), new Vector3(330, .16f, 5.3f), asphalt);
            for (int i = -20; i <= 20; i++)
                Box("Road center stripe", new Vector3(i * 7, .009f, -17), new Vector3(2.8f, .009f, .09f), cream);

            BuildRoomWing(overview);
            BuildExpansionGrounds();
            BuildUpperWalkway();
            BuildExternalStairs();
            BuildReception(overview);
            BuildUtilityRoom(overview);
            BuildRoadsideFeatures();
            BuildDesert();
            BuildPlanting();
            if (!overview)
                BuildForecourtDetails();
            BuildParking(overview);
            if (!overview)
            {
                BuildSupplyDelivery();
                BuildLaundry();
                BuildOutdoorLights();
                BuildDeskPhone();
            }
        }

        private void BuildDesert()
        {
            Material rock = Surface(new Color32(137, 111, 93, 255), .85f, .45f);
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
            const int sides = 15;
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
            var mesh = new Mesh { name = "Desert ridge" };
            mesh.vertices = vertices;
            mesh.SetTriangles(indices, 0);
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
            sign.transform.localPosition = position + roomBuildOffset;
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
        private Material motelSky;
        private Light receptionWindowFill;
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
            daylightSun.transform.rotation = Quaternion.Euler(Mathf.Max(4f, elevation * 45f), hour * 11f - 85f, 0);
            daylightSun.intensity = 1.12f * daylight;
            if (receptionWindowFill != null)
                receptionWindowFill.intensity = .64f * daylight;
            daylightSun.color = Color.Lerp(new Color(1f, .95f, .84f), new Color(1f, .63f, .38f), warm);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.19f, .22f, .34f),
                new Color(.57f, .63f, .69f), daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.18f, .20f, .27f),
                new Color(.47f, .49f, .49f), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.13f, .12f, .14f),
                new Color(.42f, .37f, .30f), daylight);
            Color sky = Color.Lerp(new Color(.075f, .105f, .19f), new Color(.76f, .82f, .87f), daylight);
            camera.backgroundColor = Color.Lerp(sky, new Color(.81f, .49f, .36f), warm * .55f);
            RenderSettings.fogColor = camera.backgroundColor;
            if (motelSky != null)
            {
                if (camera.clearFlags != CameraClearFlags.Skybox)
                {
                    camera.gameObject.AddComponent<Skybox>().material = motelSky;
                    camera.clearFlags = CameraClearFlags.Skybox;
                }
                motelSky.SetFloat("_Daylight", daylight);
                motelSky.SetColor("_Horizon", camera.backgroundColor);
            }
            foreach (Light light in outdoorLights)
                light.intensity = Mathf.Lerp(2.6f, light.range < 8f ? .39f : 0f, daylight);
            clockHour.localRotation = Quaternion.Euler(0, 0, -hour * 30f);
            clockMinuteHand.localRotation = Quaternion.Euler(0, 0, -(absoluteMinute % 60) * 6f);
        }

        private void Lighting(bool overview)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .63f, .69f);
            RenderSettings.ambientEquatorColor = new Color(.47f, .49f, .49f);
            RenderSettings.ambientGroundColor = new Color(.42f, .37f, .30f);
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
            if (!overview && Resources.Load<Shader>("MotelSky") is Shader skyShader && skyShader.isSupported)
            {
                motelSky = new Material(skyShader);
                materials.Add(motelSky);
            }
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, int layer = 0)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.layer = layer;
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = position + roomBuildOffset;
            piece.transform.localScale = scale;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            piece.GetComponent<BoxCollider>().enabled = layer != 0;
            return piece;
        }

        private Material Surface(Color color, float wear, float grain, float smoothness = .18f,
            float metallic = 0f, float grainStretch = 1f)
        {
            var material = new Material(shader) { color = color };
            material.SetFloat("_Wear", wear);
            material.SetFloat("_Grain", grain);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_GrainStretch", grainStretch);
            materials.Add(material);
            return material;
        }

        private static void ApplySurfaceDetail(Material material, string name, float repeatsPerMetre, float strength)
        {
            Texture2D detail = Resources.Load<Texture2D>("SurfaceDetail/" + name);
            if (detail == null)
                return;
            material.SetTexture("_DetailMap", detail);
            material.SetFloat("_DetailScale", repeatsPerMetre);
            material.SetFloat("_DetailStrength", strength);
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
