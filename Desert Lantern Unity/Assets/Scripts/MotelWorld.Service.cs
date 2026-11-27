using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        private static readonly float[] ParkingX = { -9.20f, -4.60f, 4.40f, 10.50f };
        private static readonly float[] ParkingLanes = { -11.4f, -6.9f, 0, 7.35f, 12.5f };
        public static Vector3 CarApproach(int room) => new Vector3(ParkingX[room], 0, -7.25f);
        private static Vector3 TrunkBagPosition(int room) => new Vector3(ParkingX[room], .86f, -6.38f);
        private readonly Rigidbody[] bags = new Rigidbody[4];
        private readonly bool[] bagDropped = new bool[4];
        private readonly Transform[] trunks = new Transform[4];
        public static readonly Vector3 DeliveryBay = new Vector3(-13.80f, 0, 1.20f);
        public static readonly Vector3 SupplyStorage = new Vector3(-8.46f, 0, 5.55f);
        private static readonly Vector3 CrateAtBay = new Vector3(-13.80f, .55f, 2.03f);
        private Transform supplyCrate;
        private BoxCollider supplyCollider;

        public static readonly Vector3 PhoneApproach = new Vector3(-8.98f, 0, 6.72f);
        public static readonly Vector3 PhonePosition = new Vector3(-8.92f, 1.30f, 7.17f);
        private readonly List<Light> outdoorLights = new List<Light>();
        private readonly TextMesh[] supplyCounts = new TextMesh[3];
        private readonly GameObject[,] stockProps = new GameObject[3, 4];
        private AudioSource deskPhone;
        private AudioClip ringClip;

        private readonly GameObject[] tvIndicator = new GameObject[4];
        private readonly GameObject[] roomServiceTray = new GameObject[4];

        public void ShowPurchasedServices(MotelService service)
        {
            for (int room = 0; room < tvIndicator.Length; room++)
            {
                int bit = 1 << room;
                tvIndicator[room].SetActive((service.ActiveRooms & service.TvPackages & bit) != 0);
                roomServiceTray[room].SetActive((service.ActiveRooms & service.MealOrders & bit) != 0);
            }
        }

        private void BuildOutdoorLights()
        {
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            OutdoorLamp(new Vector3(-9.75f, 2.23f, 3.45f), 6.1f);
            foreach (float x in RoomX)
                OutdoorLamp(new Vector3(x + .82f, 2.56f, 5.98f), 6.2f);
            foreach (float x in new[] { -13.85f, 13.10f })
            {
                Box("Parking lamp pole", new Vector3(x, 2.18f, -.85f),
                    new Vector3(.16f, 4.36f, .16f), iron, SolidLayer);
                OutdoorLamp(new Vector3(x, 4.30f, -.85f), 10f);
            }
        }

        private void OutdoorLamp(Vector3 position, float range)
        {
            Box("Outdoor lamp shade", position + Vector3.up * .115f,
                new Vector3(.43f, .14f, .33f), iron);
            Box("Outdoor lamp glass", position, new Vector3(.33f, .10f, .25f), cream);
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
            Vector3 basePosition = PhonePosition - Vector3.up * .16f;
            Box("Telephone base", basePosition, new Vector3(.34f, .10f, .24f), dark);
            Box("Telephone handset", basePosition + new Vector3(0, .085f, .015f),
                new Vector3(.37f, .08f, .085f), dark);
            for (int key = 0; key < 9; key++)
                Box("Phone keypad", basePosition + new Vector3(.055f * (key % 3 - 1), .058f, -.027f - .022f * (key / 3)),
                    new Vector3(.033f, .012f, .012f), cream);
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
            Box("Supply storage back", new Vector3(-7.48f, .92f, 5.55f),
                new Vector3(.16f, 1.84f, 1.48f), iron, SolidLayer);
            string[] labels = { "Kits", "Towels", "Linens" };
            Material[] contents = { carton, linen, runner };
            for (int level = 0; level < 3; level++)
            {
                float height = .25f + level * .66f;
                Box("Supply shelf", new Vector3(-7.78f, height, 5.55f),
                    new Vector3(.65f, .095f, 1.50f), oak, SolidLayer);
                supplyCounts[level] = Sign(labels[level] + " 0", new Vector3(-7.81f, height + .055f, 4.773f),
                    .125f, Color.white);
                for (int unit = 0; unit < 4; unit++)
                {
                    stockProps[level, unit] = Box(labels[level] + " stock", new Vector3(-7.82f, height + .14f, 4.98f + unit * .34f),
                        new Vector3(.48f, level == 0 ? .18f : .11f, .27f), contents[level]);
                    stockProps[level, unit].SetActive(false);
                }
            }
            Box("Storage sign board", new Vector3(-7.80f, 1.90f, 4.82f),
                new Vector3(1.10f, .25f, .08f), dark);
            Sign("SUPPLIES", new Vector3(-7.80f, 1.90f, 4.77f), .17f, Color.white);
            supplyCrate = Node("Supply crate", CrateAtBay);
            Piece(supplyCrate, "Cardboard parcel", Vector3.zero, new Vector3(.68f, .49f, .48f), carton);
            Piece(supplyCrate, "Packing tape", new Vector3(0, .247f, 0),
                new Vector3(.078f, .009f, .484f), cream);
            Piece(supplyCrate, "Shipping label", new Vector3(.15f, .248f, -.07f),
                new Vector3(.18f, .010f, .13f), linen);
            supplyCollider = supplyCrate.gameObject.AddComponent<BoxCollider>();
            supplyCollider.size = new Vector3(.68f, .49f, .48f);
            supplyCrate.gameObject.SetActive(false);
        }

        public void ShowSupplyDelivery(MotelService service, MotelWalker carrier)
        {
            for (int kind = 0; kind < supplyCounts.Length; kind++)
            {
                int count = service.Stock(kind);
                string name = kind == 0 ? "Kits" : kind == 1 ? "Towels" : "Linens";
                string text = name + " " + count;
                if (supplyCounts[kind].text != text)
                    supplyCounts[kind].text = text;
                for (int unit = 0; unit < 4; unit++)
                    stockProps[kind, unit].SetActive(unit < count);
            }
            bool onPallet = service.Delivery == SupplyDelivery.AtBay;
            bool visible = onPallet || carrier != null;
            supplyCrate.gameObject.SetActive(visible);
            if (!visible)
                return;
            supplyCollider.enabled = onPallet;
            supplyCrate.SetPositionAndRotation(onPallet ? CrateAtBay : carrier.CarryPosition,
                onPallet ? Quaternion.identity : carrier.Orientation);
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
            Palm(new Vector3(14.3f, 0, 3.88f), 5.41f);
            Palm(new Vector3(15.34f, 0, -6.90f), 4.68f);
            var rng = new System.Random(167);
            for (int i = 0; i < 24; i++)
            {
                float x = -33f + i * 3f + (float)rng.NextDouble() * 2;
                float z = 16.8f + (float)rng.NextDouble() * 10f;
                float height = 2.3f + (float)rng.NextDouble() * 4f;
                Cylinder("Woodland trunk", new Vector3(x, height * .42f, z),
                    new Vector3(.13f, height * .42f, .13f), bark);
                for (int branch = 0; branch < 3; branch++)
                {
                    float dx = (branch - 1) * .70f;
                    float stretch = 1f + (float)rng.NextDouble() * .32f;
                    Primitive(PrimitiveType.Sphere, "Woodland crown", new Vector3(x + dx, height + (branch % 2) * .72f, z),
                        new Vector3(2.4f * stretch, 2.7f * stretch, 2.5f * stretch), branch == 1 ? shrub : leaf);
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
            for (int car = 0; car < ParkingX.Length; car++)
                trunks[car] = Sedan("Guest " + (car + 1) + " parked sedan",
                    new Vector3(ParkingX[car], 0, -4.63f), palette[car]);
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
                    Cylinder("Tire", tireAt, new Vector3(.33f, .117f, .33f), rubber).transform.rotation = Quaternion.Euler(0, 0, 90);
                    Cylinder("Hubcap", tireAt + new Vector3(side * .120f, 0, 0),
                        new Vector3(.179f, .017f, .179f), chrome).transform.rotation = Quaternion.Euler(0, 0, 90);
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
            bool northbound = from.z < -6.88f && to.z > -2.12f;
            bool southbound = from.z > -2.12f && to.z < -6.88f;
            if (!northbound && !southbound)
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

            route.Enqueue(new Vector3(lane, 0, northbound ? -7.14f : -2.12f));
            route.Enqueue(new Vector3(lane, 0, northbound ? -2.12f : -7.14f));
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
