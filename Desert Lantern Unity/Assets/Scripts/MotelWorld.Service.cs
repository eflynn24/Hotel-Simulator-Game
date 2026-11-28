using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        private static readonly float[] ParkingX = { -9.20f, -4.60f, 4.40f, 10.50f, -9.20f, -4.60f, 4.40f, 10.50f, 19.5f, 24.5f, 19.5f, 24.5f };
        private static readonly float[] ParkingLanes = { -11.4f, -6.9f, 0, 7.35f, 12.5f, 17f, 22f, 27f };
        private static float ParkingRow(int room) => FloorHeight(room) > 0 ? -5.60f : 0f;
        public static Vector3 CarApproach(int room) => new Vector3(ParkingX[room], 0, -7.25f + ParkingRow(room));
        private static Vector3 TrunkBagPosition(int room) => new Vector3(ParkingX[room], .86f, -6.38f + ParkingRow(room));
        private readonly Rigidbody[] bags = new Rigidbody[MotelService.RoomCount];
        private readonly bool[] bagDropped = new bool[MotelService.RoomCount];
        private readonly BoxCollider[] carColliders = new BoxCollider[MotelService.RoomCount];
        private readonly Transform[] trunks = new Transform[MotelService.RoomCount];
        public static readonly Vector3 DeliveryBay = new Vector3(-13.80f, 0, 1.20f);
        public static readonly Vector3 SupplyStorage = new Vector3(-14.32f, 0, 7.22f);
        private static readonly Vector3 CrateAtBay = new Vector3(-13.80f, .55f, 2.03f);
        private Transform supplyCrate;
        private readonly GameObject[] waitingParcels = new GameObject[8];
        private BoxCollider supplyCollider;
        private readonly Transform[] carriedCrates = new Transform[LanSession.Capacity];

        public static readonly Vector3 PhoneApproach = new Vector3(-8.98f, 0, 6.72f);
        public static readonly Vector3 PhonePosition = new Vector3(-8.92f, 1.30f, 7.17f);
        private readonly List<Light> outdoorLights = new List<Light>();
        private readonly TextMesh[] supplyCounts = new TextMesh[3];
        private readonly GameObject[,] stockProps = new GameObject[3, 4];
        private AudioSource deskPhone;
        private AudioClip ringClip;

        private readonly GameObject[] tvIndicator = new GameObject[MotelService.RoomCount];
        private readonly GameObject[] roomServiceTray = new GameObject[MotelService.RoomCount];

        public void ShowPurchasedServices(MotelService service)
        {
            for (int room = 0; room < tvIndicator.Length; room++)
            {
                int bit = 1 << room;
                tvIndicator[room].SetActive((service.ActiveRooms & service.TvPackages & bit) != 0 &&
                    !service.IsBroken(room, Fixture.Television));
                roomLighting[room].enabled = !service.IsBroken(room, Fixture.Lights);
                roomServiceTray[room].SetActive((service.ActiveRooms & service.MealOrders & bit) != 0);
            }
        }

        private void BuildOutdoorLights()
        {
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            OutdoorLamp(new Vector3(-9.75f, 2.23f, 3.45f), 6.1f);
            OutdoorLamp(new Vector3(-14.55f, 2.31f, 3.95f), 4.1f);
            for (int room = 0; room < RoomX.Length; room++)
                OutdoorLamp(new Vector3(RoomX[room] + .82f, FloorHeight(room) + 2.56f, 5.98f), 6.2f);
            foreach (float x in new[] { -13.85f, 13.10f })
            {
                Box("Parking lamp pole", new Vector3(x, 2.18f, -.85f),
                    new Vector3(.16f, 4.36f, .16f), iron, SolidLayer);
                OutdoorLamp(new Vector3(x, 4.30f, -.85f), 10f);
            }
        }

        private void OutdoorLamp(Vector3 position, float range)
        {
            if (range < 8f)
            {
                Box("Lantern cap", position + Vector3.up * .10f, new Vector3(.23f, .071f, .20f), dark);
                Box("Lantern diffuser", position, new Vector3(.16f, .17f, .14f), lamp);
                Box("Lantern base", position + Vector3.down * .105f, new Vector3(.21f, .047f, .182f), dark);
                Box("Lantern wall arm", position + new Vector3(0, .093f, .18f), new Vector3(.051f, .056f, .37f), iron);
                for (int side = -1; side <= 1; side += 2)
                    Box("Lantern metal stile", position + new Vector3(side * .085f, 0, -.067f),
                        new Vector3(.017f, .18f, .019f), dark);
            }
            else
            {
                Box("Parking lamp shade", position + Vector3.up * .115f, new Vector3(.43f, .14f, .33f), iron);
                Box("Parking lamp glass", position, new Vector3(.33f, .10f, .25f), cream);
            }
            Light light = Node("Outdoor light", position + Vector3.down * .12f).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .79f, .54f);
            light.range = range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            outdoorLights.Add(light);
        }

        private void BuildDeskPhone()
        {
            Vector3 basePosition = PhonePosition - Vector3.up * .15f;
            Box("Telephone base", basePosition, new Vector3(.34f, .12f, .26f), cream);
            Primitive(PrimitiveType.Sphere, "Telephone handset bridge", basePosition + new Vector3(0, .108f, .057f),
                new Vector3(.36f, .071f, .088f), linen);
            for (int end = -1; end <= 1; end += 2)
                Primitive(PrimitiveType.Sphere, "Telephone receiver cup", basePosition + new Vector3(end * .126f, .087f, .047f),
                    new Vector3(.133f, .092f, .124f), cream);
            for (int key = 0; key < 9; key++)
                Box("Phone keypad", basePosition + new Vector3(.055f * (key % 3 - 1), .065f, -.027f - .022f * (key / 3)),
                    new Vector3(.033f, .012f, .012f), trim);
        }

        public void ShowDeskPhone(bool ringing)
        {
            if (!ringing)
            {
                if (deskPhone != null && deskPhone.isPlaying)
                    deskPhone.Stop();
                return;
            }
            if (deskPhone == null)
            {
                deskPhone = Node("Telephone ringer", PhonePosition).gameObject.AddComponent<AudioSource>();
                deskPhone.spatialBlend = .6f;
                deskPhone.maxDistance = 20f;
                deskPhone.volume = .34f;
                deskPhone.loop = true;
                const int sampleRate = 22050;
                var waveform = new float[sampleRate * 3];
                for (int i = 0; i < waveform.Length; i++)
                {
                    float t = (float)i / sampleRate;
                    float pulse = t < .23f ? t : t - .37f;
                    if (pulse >= 0 && pulse < .23f)
                    {
                        float envelope = Mathf.Clamp01(Mathf.Min(pulse, .23f - pulse) * 140f);
                        waveform[i] = envelope * .28f *
                            (Mathf.Sin(t * 440f * 2f * Mathf.PI) + Mathf.Sin(t * 620f * 2f * Mathf.PI));
                    }
                }
                ringClip = AudioClip.Create("Desk telephone ring", waveform.Length, 1, sampleRate, false);
                ringClip.SetData(waveform, 0);
                deskPhone.clip = ringClip;
            }
            if (!deskPhone.isPlaying)
                deskPhone.Play();
        }

        public void DisposeDeskPhone()
        {
            if (ringClip != null)
                Object.Destroy(ringClip);
        }

        private void BuildSupplyDelivery()
        {
            Material carton = Surface(new Color32(170, 121, 71, 255), .75f, 14);
            Box("Delivery bay marking", new Vector3(-13.80f, .018f, 1.95f),
                new Vector3(2.65f, .026f, 2.40f), cream);
            Box("Delivery pallet", new Vector3(-13.80f, .16f, 2.03f),
                new Vector3(1.45f, .29f, .98f), oak, SolidLayer);
            Sign("DELIVERIES", new Vector3(-13.80f, .31f, 1.51f), .15f, Color.white);
            const float shelfX = -14.32f;
            Material steel = Surface(new Color32(77, 78, 73, 255), .43f, 29, .27f, .21f);
            Box("Rack rear liner", new Vector3(shelfX, 1.06f, 8.18f),
                new Vector3(2.18f, 2.06f, .045f), steel, SolidLayer);
            for (int side = -1; side <= 1; side += 2)
                for (int edge = 0; edge < 2; edge++)
                    Box("Powder-coated rack upright", new Vector3(shelfX + side * 1.08f, 1.05f, 7.64f + edge * .51f),
                        new Vector3(.054f, 2.10f, .052f), steel);
            string[] labels = { "KITS", "TOWELS", "LINENS" };
            for (int level = 0; level < 4; level++)
            {
                float height = .26f + level * .50f;
                Box("Supply rack shelf", new Vector3(shelfX, height, 7.91f),
                    new Vector3(2.15f, .058f, .55f), oak, SolidLayer);
                Box("Supply shelf steel lip", new Vector3(shelfX, height, 7.616f),
                    new Vector3(2.19f, .064f, .044f), steel);
                if (level == 3)
                    continue;
                supplyCounts[level] = Sign(labels[level] + " 0", new Vector3(shelfX + .66f, height, 7.591f),
                    .079f, new Color32(254, 223, 172, 255));
                for (int unit = 0; unit < 4; unit++)
                {
                    Transform stack = Node("Stock " + labels[level], new Vector3(shelfX - .79f + unit * .48f, height + .035f, 7.94f));
                    if (level == 0)
                    {
                        Piece(stack, "Cleaning carton", new Vector3(0, .104f, 0), new Vector3(.37f, .20f, .33f), carton);
                        Piece(stack, "Carton label", new Vector3(.04f, .104f, -.168f), new Vector3(.16f, .10f, .012f), linen);
                    }
                    else
                    {
                        for (int fold = 0; fold < 3; fold++)
                            Piece(stack, "Folded fabric", new Vector3((fold % 2) * .012f, .028f + fold * .051f, 0),
                                new Vector3(.34f - fold * .012f, .048f, level == 1 ? .27f : .34f), linen);
                    }
                    stockProps[level, unit] = stack.gameObject;
                    stack.gameObject.SetActive(false);
                }
            }
            for (int parcel = 0; parcel < 3; parcel++)
            {
                Vector3 box = new Vector3(shelfX - .72f + parcel * .67f, 1.93f, 8.01f);
                Box("Reusable storage bin", box, new Vector3(.44f, .22f, .35f), parcel == 1 ? teal : carton);
                Box("Bin pull", box + new Vector3(0, .047f, -.181f), new Vector3(.13f, .034f, .012f), steel);
            }
            Box("Storage sign board", new Vector3(shelfX, 2.25f, 8.11f),
                new Vector3(1.22f, .24f, .07f), siding);
            Sign("SUPPLIES", new Vector3(shelfX, 2.25f, 8.067f), .18f, new Color32(250, 212, 152, 255));
            supplyCrate = Node("Supply crate", CrateAtBay);
            Piece(supplyCrate, "Cardboard parcel", Vector3.zero, new Vector3(.68f, .49f, .48f), carton);
            Piece(supplyCrate, "Packing tape", new Vector3(0, .247f, 0),
                new Vector3(.078f, .009f, .484f), cream);
            Piece(supplyCrate, "Shipping label", new Vector3(.15f, .248f, -.07f),
                new Vector3(.18f, .010f, .13f), linen);
            for (int index = 0; index < carriedCrates.Length; index++)
            {
                carriedCrates[index] = Object.Instantiate(supplyCrate, root);
                carriedCrates[index].name = "Carried supply crate";
                carriedCrates[index].gameObject.SetActive(false);
            }
            supplyCollider = supplyCrate.gameObject.AddComponent<BoxCollider>();
            supplyCollider.size = new Vector3(.68f, .49f, .48f);
            supplyCrate.gameObject.SetActive(false);
            for (int index = 0; index < waitingParcels.Length; index++)
            {
                Vector3 offset = new Vector3((index % 2 == 0 ? 1 : -1) * .49f, -.09f + index / 2 * .19f, .15f);
                waitingParcels[index] = Box("Waiting delivery parcel", CrateAtBay + offset,
                    new Vector3(.31f, .19f, .40f), carton);
                waitingParcels[index].SetActive(false);
            }
        }

        public void ShowSupplyDelivery(MotelService service, System.Func<ulong, MotelWalker> findCarrier)
        {
            for (int kind = 0; kind < supplyCounts.Length; kind++)
            {
                int count = service.Stock(kind);
                string name = kind == 0 ? "KITS" : kind == 1 ? "TOWELS" : "LINENS";
                string text = name + " " + count;
                if (supplyCounts[kind].text != text)
                    supplyCounts[kind].text = text;
                for (int unit = 0; unit < 4; unit++)
                    stockProps[kind, unit].SetActive(unit < count);
            }
            int ready = service.ReadyOrderCount;
            for (int index = 0; index < waitingParcels.Length; index++)
                waitingParcels[index].SetActive(index < ready - 1);
            supplyCrate.gameObject.SetActive(ready > 0);
            supplyCollider.enabled = ready > 0;

            int shown = 0;
            for (int index = 0; index < service.PendingOrderCount && shown < carriedCrates.Length; index++)
            {
                SupplyOrder order = service.SupplyOrderAt(index);
                if (order.Status != SupplyDelivery.Carried)
                    continue;
                MotelWalker carrier = findCarrier(order.Carrier);
                if (carrier == null)
                    continue;
                Transform crate = carriedCrates[shown++];
                crate.SetPositionAndRotation(carrier.CarryPosition, carrier.Orientation);
                crate.gameObject.SetActive(true);
            }
            for (int index = shown; index < carriedCrates.Length; index++)
                carriedCrates[index].gameObject.SetActive(false);
        }

        private Material rubber, chrome, bark, shrub;

        private void BuildRoadsideFeatures()
        {
            Box("Roadside sign timber", new Vector3(-14, 2.48f, -2.2f), new Vector3(.36f, 4.96f, .33f), siding, SolidLayer);
            Box("Roadside sign board", new Vector3(-14, 4.87f, -2.2f), new Vector3(3.30f, 1.44f, .30f), dark);
            Box("Sign lower border", new Vector3(-14, 4.14f, -2.36f), new Vector3(3.31f, .054f, .045f), cream);
            Sign("MOTEL", new Vector3(-14, 4.68f, -2.36f), .57f, new Color32(255, 219, 139, 255));
            Sign("DESERT LANTERN", new Vector3(-14, 5.15f, -2.36f), .19f, Color.white);
            Box("Roadside planter curb", new Vector3(-13.35f, .11f, -9.9f),
                new Vector3(3.71f, .22f, 2.65f), stone, SolidLayer);
            Box("Planter topsoil", new Vector3(-13.35f, .205f, -9.9f),
                new Vector3(3.52f, .10f, 2.47f), sand);
            Box("Driveway shoulder", new Vector3(9.28f, -.05f, -12.91f), new Vector3(7.78f, .12f, 4.05f), asphalt);
            for (int i = 0; i < 4; i++)
            {
                var bollard = Cylinder("Yellow driveway bollard", new Vector3(5.0f + 2.9f * (i % 2), .42f, -11.9f - (i / 2) * 1.3f),
                    new Vector3(.057f, .42f, .057f), cream, SolidLayer);
                bollard.transform.rotation = Quaternion.Euler(0, 0, (i - 1) * 2f);
            }
        }

        private void BuildPlanting()
        {
            bark = Surface(new Color32(106, 93, 73, 255), .71f, 14);
            shrub = Surface(new Color32(81, 107, 70, 255), .53f, 9);
            Palm(new Vector3(-13.64f, .24f, -10.12f), 4.8f);
            Palm(new Vector3(-15.6f, 0, -1.23f), 5.72f);
            Palm(new Vector3(19.2f, 0, 2.80f), 5.41f);
            Palm(new Vector3(15.34f, 0, -6.90f), 4.68f);
            var rng = new System.Random(167);
            Mesh[] crowns = { BuildCanopyMesh(rng), BuildCanopyMesh(rng), BuildCanopyMesh(rng) };
            Material shadedLeaves = Surface(new Color32(75, 87, 56, 255), .65f, 24, .14f);
            Material sunlitLeaves = Surface(new Color32(101, 110, 67, 255), .53f, 18, .16f);
            for (int i = 0; i < 20; i++)
            {
                var foot = new Vector3(-34f + i * 3.8f + (float)rng.NextDouble() * 2.4f,
                    0, 17.5f + (float)rng.NextDouble() * 10f);
                float height = 2.7f + (float)rng.NextDouble() * 3.3f;
                Vector3 fork = foot + new Vector3(.21f, height * .55f, -.11f);
                BuildTreeLimb(foot, fork, height * .032f);

                for (int branch = 0; branch < 4; branch++)
                {
                    float angle = (i * .78f + branch * 1.81f);
                    var lateral = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    float spread = height * (.20f + .13f * (float)rng.NextDouble());
                    Vector3 tip = foot + lateral * spread + Vector3.up * (height * (.75f + .22f * (float)rng.NextDouble()));
                    BuildTreeLimb(fork, tip, height * .016f);
                    Transform cluster = Node("Mesquite foliage", tip + Vector3.up * .21f);
                    cluster.localRotation = Quaternion.Euler(6f * (branch - 1), angle * Mathf.Rad2Deg, 0);
                    cluster.localScale = new Vector3(height * .54f, height * (.34f + .04f * branch), height * .47f);
                    cluster.gameObject.AddComponent<MeshFilter>().sharedMesh = crowns[branch % crowns.Length];
                    cluster.gameObject.AddComponent<MeshRenderer>().sharedMaterial = branch < 2 ? shadedLeaves : sunlitLeaves;
                }
            }
            for (int i = 0; i < 19; i++)
            {
                float x = -12f - (float)rng.NextDouble() * 2.90f;
                float z = -10.98f + (float)rng.NextDouble() * 2.10f;
                float size = .18f + (float)rng.NextDouble() * .54f;
                Primitive(PrimitiveType.Sphere, "Roadside shrub", new Vector3(x, .31f, z),
                    new Vector3(1.2f * size, .67f * size, size), i % 3 == 0 ? leaf : shrub);
            }
        }

        private void BuildTreeLimb(Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = end - start;
            GameObject limb = Cylinder("Mesquite branch", (start + end) * .5f,
                new Vector3(radius * 2, axis.magnitude * .5f, radius * 2), bark);
            limb.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
        }

        private Mesh BuildCanopyMesh(System.Random random)
        {
            const int ringCount = 7, sides = 15;
            var vertices = new List<Vector3>(ringCount * sides + 2) { Vector3.down * .5f };
            var triangles = new List<int>();
            for (int ring = 0; ring < ringCount; ring++)
            {
                float phi = (ring + 1f) / (ringCount + 1f) * Mathf.PI;
                float y = -.5f * Mathf.Cos(phi);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float waviness = 1f + .13f * Mathf.Sin(angle * 5 + phi * 2) + .10f * (float)random.NextDouble();
                    float radius = .5f * Mathf.Sin(phi) * waviness;
                    vertices.Add(new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle)));
                }
            }
            int top = vertices.Count;
            vertices.Add(Vector3.up * .5f);
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles.AddRange(new[] { 0, 1 + side, 1 + next });
                for (int ring = 0; ring < ringCount - 1; ring++)
                {
                    int start = 1 + ring * sides;
                    int a = start + side, b = start + next;
                    triangles.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
                }
                int last = 1 + (ringCount - 1) * sides;
                triangles.AddRange(new[] { last + side, top, last + next });
            }
            var mesh = new Mesh { name = "Irregular mesquite canopy" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            return mesh;
        }

        private void Palm(Vector3 foot, float height)
        {
            const int sections = 11;
            for (int i = 0; i < sections; i++)
            {
                float t = (i + .5f) / sections;
                float radius = Mathf.Lerp(.20f, .12f, t);
                var trunkPiece = Cylinder("Palm ring", foot + new Vector3(t * t * .38f, height * t, 0),
                    new Vector3(radius, height / (sections * 2f) + .018f, radius), bark);
                trunkPiece.transform.rotation = Quaternion.Euler(0, 0, -t * 6);
            }
            Vector3 crown = foot + new Vector3(.38f, height, 0);
            for (int leafIndex = 0; leafIndex < 9; leafIndex++)
            {
                float angle = (leafIndex * 40f + 7f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 sideways = Vector3.Cross(direction, Vector3.up);
                var vertices = new List<Vector3>();
                var faces = new List<int>();
                const int steps = 7;
                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    float extent = (2.11f + (leafIndex % 3) * .20f) * t;
                    float curve = Mathf.Sin(t * Mathf.PI) * .52f - t * t * .88f;
                    float width = .038f + Mathf.Sin(Mathf.PI * t) * .28f;
                    Vector3 stem = crown + direction * extent + Vector3.up * curve;
                    vertices.Add(stem - sideways * width - Vector3.up * .06f);
                    vertices.Add(stem + Vector3.up * .055f);
                    vertices.Add(stem + sideways * width - Vector3.up * .06f);
                    if (step == 0)
                        continue;
                    int a = (step - 1) * 3;
                    faces.AddRange(new[] {a,a+3,a+1, a+1,a+3,a+4, a+1,a+4,a+2, a+2,a+4,a+5});
                }
                ShapeTwoSided("Curved palm frond", vertices.ToArray(), faces.ToArray(), leaf);
            }
        }

        private void ShapeTwoSided(string name, Vector3[] vertices, int[] indices, Material material)
        {
            var doubled = new Vector3[vertices.Length * 2];
            vertices.CopyTo(doubled, 0);
            vertices.CopyTo(doubled, vertices.Length);
            var triangles = new int[indices.Length * 2];
            indices.CopyTo(triangles, 0);
            for (int i = 0; i < indices.Length; i += 3)
            {
                triangles[indices.Length + i] = indices[i + 2] + vertices.Length;
                triangles[indices.Length + i + 1] = indices[i + 1] + vertices.Length;
                triangles[indices.Length + i + 2] = indices[i] + vertices.Length;
            }
            Shape(name, doubled, triangles, material);
        }

        private void BuildParking(bool overview)
        {
            rubber = Surface(new Color32(33, 35, 34, 255), .31f, 8);
            chrome = Surface(new Color32(154, 160, 154, 255), .33f, 7);
            var vehicleBlue = Surface(new Color32(62, 101, 110, 255), .28f, 9);
            var vehicleTan = Surface(new Color32(155, 129, 98, 255), .30f, 9);
            var vehicleWine = Surface(new Color32(104, 54, 51, 255), .29f, 9);
            var vehicleSage = Surface(new Color32(79, 103, 76, 255), .33f, 9);
            Material[] palette = { vehicleBlue, vehicleTan, vehicleWine, vehicleSage };
            for (int car = 0; car < (overview ? MotelService.BaseRoomCount : ParkingX.Length); car++)
            {
                trunks[car] = Sedan("Guest " + (car + 1) + " parked sedan",
                    new Vector3(ParkingX[car], 0, -4.63f + ParkingRow(car)), palette[car % palette.Length]);
                if (!overview)
                {
                    Transform sedan = trunks[car].parent;
                    carColliders[car] = sedan.Find("Lower chassis").GetComponent<BoxCollider>();
                    sedan.gameObject.SetActive(false);
                }
            }
            if (overview)
                return;

            Material leather = Surface(new Color32(114, 76, 51, 255), .33f, 22);
            Material piping = Surface(new Color32(63, 48, 40, 255), .30f, 18);
            for (int room = 0; room < bags.Length; room++)
            {
                Transform shell = Node("Room " + (room + 1) + " suitcase", TrunkBagPosition(room));
                Piece(shell, "Canvas luggage body", Vector3.zero, new Vector3(.30f, .47f, .60f), leather);
                for (int side = -1; side <= 1; side += 2)
                    Piece(shell, "Case binding", new Vector3(0, 0, side * .228f),
                        new Vector3(.313f, .485f, .054f), piping);
                Piece(shell, "Luggage handle", new Vector3(0, .29f, 0), new Vector3(.072f, .044f, .26f), piping);
                for (int side = -1; side <= 1; side += 2)
                    Piece(shell, "Handle post", new Vector3(0, .248f, side * .107f),
                        new Vector3(.040f, .067f, .050f), piping);
                BoxCollider collision = shell.gameObject.AddComponent<BoxCollider>();
                collision.size = new Vector3(.31f, .48f, .61f);
                Rigidbody body = shell.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.detectCollisions = false;
                body.mass = 9;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                bags[room] = body;
                shell.gameObject.SetActive(false);
            }
        }

        private Transform Sedan(string name, Vector3 center, Material paint)
        {
            Transform model = Node(name, center);
            Piece(model, "Lower chassis", new Vector3(0, .52f, 0), new Vector3(1.69f, .46f, 4.19f), paint, SolidLayer);
            Piece(model, "Belt line", new Vector3(0, .78f, .29f), new Vector3(1.66f, .24f, 3.60f), paint);
            Piece(model, "Cabin roof", new Vector3(0, 1.39f, .14f), new Vector3(1.30f, .14f, 1.65f), paint);
            Piece(model, "Cabin side glazing", new Vector3(0, 1.135f, .14f), new Vector3(1.33f, .42f, 1.48f), window);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject glass = Piece(model, "Sloped cabin glass", new Vector3(0, 1.07f, .14f + side * .893f),
                    new Vector3(1.29f, .057f, .67f), window);
                glass.transform.localRotation = Quaternion.Euler(side * 36, 0, 0);
                Piece(model, "B-pillar", new Vector3(side * .68f, 1.14f, .12f), new Vector3(.042f, .42f, .091f), paint);
                Piece(model, "Handle inset", new Vector3(side * .856f, .875f, .45f), new Vector3(.041f, .032f, .14f), chrome);
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 tireAt = center + new Vector3(side * .83f, .34f, end * 1.28f);
                    Transform tire = Cylinder("Tire", tireAt, new Vector3(.33f, .117f, .33f), rubber).transform;
                    tire.rotation = Quaternion.Euler(0, 0, 90);
                    tire.SetParent(model, true);
                    Transform hubcap = Cylinder("Hubcap", tireAt + new Vector3(side * .120f, 0, 0),
                        new Vector3(.179f, .017f, .179f), chrome).transform;
                    hubcap.rotation = Quaternion.Euler(0, 0, 90);
                    hubcap.SetParent(model, true);
                    Piece(model, end > 0 ? "Headlamp" : "Tail lamp",
                        new Vector3(side * .58f, .69f, end * 2.10f),
                        new Vector3(.37f, .13f, .037f), end > 0 ? lamp : carpet);
                }
            }
            Piece(model, "Rear bumper", new Vector3(0, .43f, -2.105f), new Vector3(1.74f, .14f, .14f), chrome);
            Piece(model, "Front bumper", new Vector3(0, .43f, 2.105f), new Vector3(1.74f, .14f, .14f), chrome);
            Transform lid = Node("Trunk hinge", center + new Vector3(0, .82f, -1.13f));
            lid.SetParent(model, true);
            Piece(lid, "Trunk lid", new Vector3(0, 0, -.46f), new Vector3(1.51f, .082f, .91f), paint);
            return lid;
        }

        public static void RouteAcrossParking(Queue<Vector3> route, Vector3 from, Vector3 to)
        {
            int fromZone = ParkingZone(from.z);
            int toZone = ParkingZone(to.z);
            if (fromZone == toZone)
                return;

            float lane = 0;
            float cost = float.PositiveInfinity;
            foreach (float candidate in ParkingLanes)
            {
                float distance = Mathf.Abs(from.x - candidate) + Mathf.Abs(to.x - candidate) +
                    Mathf.Abs((from.x + to.x) / 2 - candidate) * .08f;
                if (distance < cost)
                {
                    lane = candidate;
                    cost = distance;
                }
            }

            route.Enqueue(new Vector3(lane, 0, ParkingCorridor(fromZone)));
            route.Enqueue(new Vector3(lane, 0, ParkingCorridor(toZone)));
        }

        private static int ParkingZone(float z) => z > -2.2f ? 0 : z >= -8.12f ? 1 : 2;

        private static float ParkingCorridor(int zone) => zone == 0 ? -2.12f : zone == 1 ? -7.19f : -12.84f;

        internal static LanPose GuestCarPose(int room, float progress)
        {
            Vector3 parked = new Vector3(ParkingX[room], 0, -4.63f + ParkingRow(room));
            progress = Mathf.Clamp01(progress);
            if (progress >= 1)
                return new LanPose { Position = parked, Rotation = Quaternion.identity };

            const float turnStart = .72f;
            Vector3 corner = parked + new Vector3(-2.5f, 0, -3.15f);
            Vector3 entrance = new Vector3(-17f, 0, corner.z);
            if (progress < turnStart)
            {
                return new LanPose
                {
                    Position = Vector3.Lerp(entrance, corner, progress / turnStart),
                    Rotation = Quaternion.LookRotation(Vector3.right)
                };
            }

            float turn = (progress - turnStart) / (1 - turnStart);
            Vector3 bend = new Vector3(parked.x, 0, corner.z);
            Vector3 first = Vector3.Lerp(corner, bend, turn);
            Vector3 second = Vector3.Lerp(bend, parked, turn);
            return new LanPose
            {
                Position = Vector3.Lerp(first, second, turn),
                Rotation = Quaternion.LookRotation(second - first)
            };
        }

        public void ShowGuestCars(MotelService service, int arrivingRoom, float progress)
        {
            for (int room = 0; room < trunks.Length; room++)
            {
                Transform sedan = trunks[room].parent;
                bool arriving = room == arrivingRoom;
                bool visible = arriving || (service.ActiveRooms & (1 << room)) != 0;
                if (sedan.gameObject.activeSelf != visible)
                    sedan.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                float carProgress = arriving ? progress : 1;
                LanPose pose = GuestCarPose(room, carProgress);
                sedan.SetPositionAndRotation(pose.Position, pose.Rotation);
                carColliders[room].enabled = carProgress >= 1;
            }
        }

        public void AnimateService(MotelService service, MotelWalker carrier, float delta)
        {
            foreach (Transform fan in fans)
                fan.Rotate(0, delta * 126, 0, Space.Self);
            for (int index = 0; index < roomDoors.Length; index++)
            {
                float degrees = (service.OpenDoors & (1 << index)) != 0 ? -99 : 0;
                Transform hinge = roomDoors[index];
                hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation,
                    Quaternion.Euler(0, degrees, 0), 154f * delta);
            }
            for (int room = 0; room < trunks.Length; room++)
            {
                Transform trunk = trunks[room];
                bool open = service.Bag == LuggageStatus.InCar && service.BagRoom == room;
                trunk.localRotation = Quaternion.RotateTowards(trunk.localRotation,
                    Quaternion.Euler(open ? 64 : 0, 0, 0), 123f * delta);
            }

            for (int room = 0; room < bags.Length; room++)
            {
                Rigidbody body = bags[room];
                int bit = 1 << room;
                bool delivered = (service.DeliveredBags & service.ActiveRooms & bit) != 0;
                if (!delivered)
                    bagDropped[room] = false;
                bool active = service.Bag != LuggageStatus.None && service.BagRoom == room;
                body.gameObject.SetActive(delivered || active);
                if (delivered)
                {
                    if (bagDropped[room])
                        continue;
                    body.position = RackPosition(room) + Vector3.up * .32f;
                    body.rotation = Quaternion.Euler(0, 4, 0);
                    body.detectCollisions = true;
                    body.isKinematic = false;
                    body.linearVelocity = Vector3.zero;
                    bagDropped[room] = true;
                }
                else if (active)
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                    if (service.Bag == LuggageStatus.Carried && carrier != null)
                    {
                        body.position = carrier.CarryPosition;
                        body.rotation = carrier.Orientation;
                    }
                    else if (service.Bag == LuggageStatus.InCar)
                    {
                        body.position = TrunkBagPosition(room);
                        body.rotation = Quaternion.Euler(0, 90, 90);
                    }
                }
            }
        }
    }
}
