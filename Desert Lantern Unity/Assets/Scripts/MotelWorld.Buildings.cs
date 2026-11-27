using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public static readonly Vector3 ComputerApproach = new Vector3(-10.22f, 0, 6.62f);
        public static readonly Vector3 ComputerScreen = new Vector3(-10.28f, 1.61f, 7.24f);
        public Transform ReceptionComputer { get; private set; }
        public static Vector3 RoomDoor(int room) => new Vector3(RoomX[room], 0, 6.43f);
        public static Vector3 BagDrop(int room) => new Vector3(RoomX[room] + .48f, 0, 10.69f);
        public static Vector3 RackPosition(int room) => new Vector3(RoomX[room] + 1.86f, .57f, 10.68f);
        public static int InteriorRoom(Vector3 position)
        {
            if (position.z < 6.50f || position.z > 13.35f)
                return -1;
            for (int room = 0; room < RoomX.Length; room++)
                if (Mathf.Abs(position.x - RoomX[room]) < 2.42f)
                    return room;
            return -1;
        }

        private Material siding, oak, trim, linen, runner, carpet, tile, stone, leaf, lamp, dark;
        private readonly Transform[] roomDoors = new Transform[4];
        private readonly List<Transform> fans = new List<Transform>();

        private void CreateFinishes()
        {
            siding = Surface(new Color32(103, 79, 62, 255), .63f, 11);
            oak = Surface(new Color32(159, 110, 60, 255), .37f, 13);
            trim = Surface(new Color32(219, 210, 188, 255), .25f, 9);
            linen = Surface(new Color32(239, 229, 209, 255), .12f, 14);
            runner = Surface(new Color32(105, 116, 82, 255), .43f, 24);
            carpet = Surface(new Color32(112, 51, 51, 255), .51f, 26);
            tile = Surface(new Color32(161, 146, 119, 255), .25f, 15);
            stone = Surface(new Color32(128, 129, 113, 255), .75f, 5);
            leaf = Surface(new Color32(56, 96, 58, 255), .35f, 9);
            lamp = Surface(new Color32(255, 228, 168, 255), 0, 1);
            lamp.EnableKeyword("_EMISSION");
            lamp.SetColor("_EmissionColor", new Color(.37f, .23f, .10f));
            dark = Surface(new Color32(47, 47, 43, 255), .19f, 8);
        }

        private void BuildRoom(int room, bool overview)
        {
            float x = RoomX[room];
            Box("Room floor", new Vector3(x, -.04f, 9.91f), new Vector3(4.92f, .10f, 6.92f), carpet, GroundLayer);
            Box("Room back wall", new Vector3(x, 1.45f, 13.30f), new Vector3(5, 2.9f, .15f), plaster, SolidLayer);
            Box("Room west wall", new Vector3(x - 2.43f, 1.45f, 9.89f), new Vector3(.15f, 2.9f, 6.8f), plaster, SolidLayer);
            if (room == RoomX.Length - 1)
                Box("Wing east wall", new Vector3(x + 2.43f, 1.45f, 9.89f), new Vector3(.15f, 2.9f, 6.8f), plaster, SolidLayer);
            for (int side = -1; side <= 1; side += 2)
                Box("Room front wall", new Vector3(x + side * 1.54f, 1.45f, 6.52f),
                    new Vector3(1.91f, 2.9f, .19f), plaster, SolidLayer);
            Box("Door header", new Vector3(x, 2.54f, 6.52f), new Vector3(1.22f, .72f, .19f), siding, SolidLayer);
            Box("Room ceiling", new Vector3(x, 2.91f, 9.90f), new Vector3(5.01f, .12f, 6.93f), linen, SolidLayer);
            Box("Crown molding", new Vector3(x, 2.79f, 13.19f), new Vector3(4.8f, .13f, .065f), oak);
            Box("Back baseboard", new Vector3(x, .11f, 13.19f), new Vector3(4.8f, .15f, .065f), oak);

            roomDoors[room] = Node("Door " + (room + 1) + " hinge", new Vector3(x - .55f, 0, 6.43f));
            Piece(roomDoors[room], "Solid door leaf", new Vector3(.55f, 1.08f, 0),
                new Vector3(1.10f, 2.14f, .075f), teal, SolidLayer);
            Piece(roomDoors[room], "Brass handle", new Vector3(.94f, 1.03f, -.067f),
                new Vector3(.12f, .058f, .10f), cream);
            for (int side = -1; side <= 1; side += 2)
                Box("Door trim", new Vector3(x + side * .59f, 1.08f, 6.39f), new Vector3(.10f, 2.17f, .10f), trim);
            Sign((room + 1).ToString("00"), new Vector3(x, 2.45f, 6.396f), .20f, Color.white);

            Glazing(x + 1.54f, 1.63f, 6.395f, 1.44f, 1.10f);
            Vent(x + 1.54f, .67f, 6.41f);
            Box("Stone ground-floor skirting", new Vector3(x + 1.49f, .22f, 6.402f), new Vector3(1.76f, .42f, .10f), stone);
            Box("Stone ground-floor skirting", new Vector3(x - 1.49f, .22f, 6.402f), new Vector3(1.76f, .42f, .10f), stone);
            if (overview)
                return;

            Bed(x, 8.20f);
            Bed(x, 11.34f);
            Box("Shared bedside cabinet", new Vector3(x - 2.00f, .38f, 9.76f),
                new Vector3(.79f, .76f, .79f), oak, SolidLayer);
            Box("Bedside drawer", new Vector3(x - 1.59f, .55f, 9.76f), new Vector3(.050f, .22f, .68f), trim);
            Box("Drawer pull", new Vector3(x - 1.55f, .55f, 9.76f), new Vector3(.052f, .055f, .23f), iron);
            Box("Reading light shade", new Vector3(x - 2.21f, 1.88f, 9.76f),
                new Vector3(.30f, .37f, .30f), lamp);
            WarmLight(new Vector3(x - 1.97f, 1.63f, 9.76f), 3.3f, .80f);

            Box("Credenza", new Vector3(x + 1.26f, .43f, 12.85f), new Vector3(1.70f, .86f, .68f), oak, SolidLayer);
            Box("Microwave", new Vector3(x + 1.46f, 1.05f, 12.78f), new Vector3(.66f, .37f, .38f), dark);
            Box("Microwave window", new Vector3(x + 1.41f, 1.05f, 12.58f), new Vector3(.45f, .22f, .028f), window);
            Box("Mini refrigerator", new Vector3(x + .03f, .38f, 12.87f), new Vector3(.60f, .76f, .62f), trim, SolidLayer);
            Box("Refrigerator handle", new Vector3(x + .20f, .65f, 12.542f), new Vector3(.08f, .16f, .05f), dark);
            Box("Window AC housing", new Vector3(x + 1.62f, 2.03f, 13.18f), new Vector3(.87f, .48f, .12f), trim);
            for (int i = 0; i < 4; i++)
                Box("AC grille", new Vector3(x + 1.62f, 1.93f + i * .064f, 13.109f),
                    new Vector3(.66f, .018f, .025f), dark);
            Box("Wall television", new Vector3(x + 2.30f, 1.70f, 9.13f),
                new Vector3(.10f, .60f, 1.05f), dark);
            Box("TV panel", new Vector3(x + 2.235f, 1.70f, 9.13f),
                new Vector3(.026f, .51f, .94f), window);
            Cylinder("Round table top", new Vector3(x + 1.71f, .73f, 7.61f), new Vector3(.48f, .044f, .48f), siding, SolidLayer);
            Cylinder("Table pedestal", new Vector3(x + 1.71f, .35f, 7.61f), new Vector3(.08f, .35f, .08f), iron);
            Cylinder("Table foot", new Vector3(x + 1.71f, .035f, 7.61f), new Vector3(.30f, .035f, .30f), iron);

            Vector3 rack = RackPosition(room);
            Box("Luggage rack top", rack + Vector3.down * .08f, new Vector3(.68f, .095f, 1.03f), oak, SolidLayer);
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                    Box("Luggage rack leg", rack + new Vector3(a * .27f, -.32f, b * .43f),
                        new Vector3(.07f, .48f, .07f), iron);

            Transform fan = Node("Ceiling fan", new Vector3(x + .02f, 2.61f, 9.90f));
            fans.Add(fan);
            Cylinder("Fan motor", fan.position + Vector3.up * .10f, new Vector3(.21f, .105f, .21f), iron);
            Cylinder("Ceiling globe", fan.position + Vector3.down * .05f, new Vector3(.13f, .066f, .13f), lamp);
            for (int i = 0; i < 4; i++)
            {
                Transform arm = Node("Fan arm", fan.position);
                arm.SetParent(fan, true);
                arm.localRotation = Quaternion.Euler(0, i * 90f, 0);
                Piece(arm, "Timber fan blade", new Vector3(.50f, 0, 0), new Vector3(.77f, .037f, .157f), oak);
            }
            WarmLight(new Vector3(x + .2f, 2.50f, 9.83f), 5.3f, 1.65f);
        }

        private void Bed(float x, float z)
        {
            Box("Twin upholstered bed base", new Vector3(x - 1.29f, .23f, z),
                new Vector3(2.00f, .46f, 1.25f), runner, SolidLayer);
            Box("Covered mattress", new Vector3(x - 1.26f, .54f, z),
                new Vector3(2.07f, .24f, 1.28f), linen);
            Box("Bed runner", new Vector3(x - .57f, .674f, z), new Vector3(.67f, .029f, 1.29f), runner);
            Box("Linen pillow", new Vector3(x - 1.99f, .704f, z), new Vector3(.42f, .108f, .79f), linen);
            Box("Oak headboard", new Vector3(x - 2.29f, .75f, z), new Vector3(.095f, .83f, 1.33f), oak);
        }

        private void BuildUpperWalkway()
        {
            Box("Upper motel wing", new Vector3(3.5f, 4.23f, 9.9f),
                new Vector3(20.05f, 2.63f, 6.86f), siding);
            Box("Upper balcony floor", new Vector3(3.5f, 2.97f, 5.70f), new Vector3(20.45f, .22f, 2.18f), trim, SolidLayer);
            Box("Balcony top rail", new Vector3(3.5f, 4.12f, 4.70f), new Vector3(20.45f, .105f, .13f), trim);
            Box("Balcony lower rail", new Vector3(3.5f, 3.32f, 4.70f), new Vector3(20.45f, .095f, .12f), trim);
            for (int i = 0; i < 53; i++)
                Box("Balcony baluster", new Vector3(-6.65f + i * .39f, 3.72f, 4.70f),
                    new Vector3(.055f, .77f, .06f), trim);
            Box("Ground-floor walkway", new Vector3(3.5f, -.035f, 5.66f),
                new Vector3(20.3f, .09f, 2.22f), tile, GroundLayer);
            foreach (float x in RoomX)
            {
                Glazing(x + 1.44f, 4.26f, 6.37f, 1.43f, 1.08f);
                Box("Upper door surround", new Vector3(x, 4.15f, 6.365f), new Vector3(1.04f, 2.16f, .11f), trim);
                Box("Upper motel door", new Vector3(x, 4.16f, 6.297f), new Vector3(.90f, 2.00f, .061f), teal);
            }
            for (int i = 0; i <= 4; i++)
            {
                float x = -6.5f + 5f * i;
                Box("Walkway timber post", new Vector3(x, 2.69f, 4.71f), new Vector3(.17f, 5.38f, .17f), siding, SolidLayer);
                Box("Stone post base", new Vector3(x, .36f, 4.71f), new Vector3(.31f, .72f, .34f), stone, SolidLayer);
                Box("Parking stall stripe", new Vector3(x, .008f, 1.36f), new Vector3(.070f, .01f, 5.3f), trim);
            }
            Box("Upper walkway soffit", new Vector3(3.5f, 5.47f, 5.67f), new Vector3(20.7f, .15f, 2.20f), siding);
            GableRoof("Wing gable", 3.5f, 5.60f, 9.36f, 20.80f, 9.50f, 1.55f, false);
        }

        private void BuildReception(bool overview)
        {
            const float x = -9.65f;
            Box("Office floor", new Vector3(x, -.04f, 6.43f), new Vector3(4.65f, .095f, 4.65f), tile, GroundLayer);
            Box("Rear office wall", new Vector3(x, 1.48f, 8.68f), new Vector3(4.79f, 2.96f, .2f), oak, SolidLayer);
            Box("Office west wall", new Vector3(-12, 1.48f, 6.42f), new Vector3(.20f, 2.96f, 4.69f), oak, SolidLayer);
            Box("Office east wall", new Vector3(-7.31f, 1.48f, 6.42f), new Vector3(.20f, 2.96f, 4.69f), oak, SolidLayer);
            Box("Entrance left jamb wall", new Vector3(-11.27f, 1.48f, 4.10f), new Vector3(1.65f, 2.96f, .20f), plaster, SolidLayer);
            Box("Entrance right jamb wall", new Vector3(-8.14f, 1.48f, 4.10f), new Vector3(1.86f, 2.96f, .20f), plaster, SolidLayer);
            Box("Entrance header", new Vector3(-9.75f, 2.64f, 4.10f), new Vector3(1.45f, .64f, .22f), siding, SolidLayer);
            Box("Office ceiling", new Vector3(x, 2.96f, 6.36f), new Vector3(4.95f, .15f, 4.98f), linen, SolidLayer);
            Glazing(-8.18f, 1.55f, 3.96f, 1.35f, .91f);
            GableRoof("Office main roof", x, 3.03f, 6.31f, 5.25f, 5.50f, 1.39f, true);
            GableRoof("Office entry porch", x, 2.62f, 3.62f, 5.28f, 2.13f, 1.03f, true);
            for (int side = -1; side <= 1; side += 2)
            {
                float postX = x + side * 2.30f;
                Box("Porch support", new Vector3(postX, 1.35f, 2.75f), new Vector3(.19f, 2.70f, .19f), siding, SolidLayer);
                Box("Porch stone pedestal", new Vector3(postX, .39f, 2.75f), new Vector3(.42f, .78f, .42f), stone, SolidLayer);
            }
            Box("Reception fascia sign", new Vector3(x, 2.96f, 2.49f), new Vector3(2.76f, .45f, .08f), siding);
            Sign("RECEPTION", new Vector3(x, 2.96f, 2.445f), .35f, Color.white);
            Box("Office entrance landing", new Vector3(-9.75f, -.045f, 3.3f), new Vector3(3.99f, .105f, 1.60f), tile, GroundLayer);
            if (overview)
                return;

            for (int board = 0; board < 20; board++)
            {
                float offset = board * .232f;
                Box("Rear wood panel seam", new Vector3(-11.88f + offset, 1.46f, 8.573f),
                    new Vector3(.012f, 2.86f, .014f), siding);
                Box("Office wall panel seam", new Vector3(-11.892f, 1.46f, 4.12f + offset),
                    new Vector3(.014f, 2.86f, .012f), siding);
            }
            Box("Laminated counter base", new Vector3(-9.37f, .50f, 7.43f), new Vector3(2.87f, 1f, .70f), oak, SolidLayer);
            Box("Ivory counter top", new Vector3(-9.38f, 1.04f, 7.43f), new Vector3(3.03f, .10f, .90f), linen, SolidLayer);
            Box("Counter return", new Vector3(-7.90f, .51f, 7.87f), new Vector3(.55f, 1.02f, 1.54f), oak, SolidLayer);
            Box("Return countertop", new Vector3(-7.90f, 1.04f, 7.87f), new Vector3(.63f, .10f, 1.59f), linen);
            for (int strip = 0; strip < 2; strip++)
                Box("Counter rail inlay", new Vector3(-9.37f, .24f + strip * .30f, 7.066f),
                    new Vector3(2.74f, .048f, .041f), trim);
            BuildReceptionComputer();
            Box("Registration ledger", new Vector3(-9.47f, 1.104f, 7.33f), new Vector3(.54f, .035f, .39f), trim);
            Box("Desk telephone", new Vector3(-8.67f, 1.126f, 7.19f), new Vector3(.32f, .076f, .23f), linen);
            Box("Handset", new Vector3(-8.67f, 1.185f, 7.19f), new Vector3(.29f, .065f, .081f), trim);
            Box("Key cabinet", new Vector3(-10.35f, 1.77f, 8.50f), new Vector3(1.28f, .96f, .145f), siding);
            Box("Key cabinet backing", new Vector3(-10.35f, 1.77f, 8.416f), new Vector3(1.18f, .86f, .036f), dark);
            for (int row = 0; row < 4; row++)
                Box("Key cubby shelf", new Vector3(-10.35f, 1.42f + row * .23f, 8.381f), new Vector3(1.19f, .029f, .081f), oak);
            for (int column = 0; column < 5; column++)
                Box("Key cubby divider", new Vector3(-10.83f + column * .24f, 1.77f, 8.384f), new Vector3(.024f, .86f, .077f), oak);
            Box("Reception rate board", new Vector3(-8.33f, 1.81f, 8.533f), new Vector3(1.09f, .88f, .069f), dark);
            Sign("ROOMS", new Vector3(-8.33f, 2.05f, 8.487f), .20f, Color.white);
            Sign("$65 PER NIGHT", new Vector3(-8.33f, 1.79f, 8.487f), .112f, Color.white);
            GameObject clock = Cylinder("Wall clock face", new Vector3(-9.35f, 2.39f, 8.526f),
                new Vector3(.23f, .034f, .23f), linen);
            clock.transform.rotation = Quaternion.Euler(90, 0, 0);
            Box("Clock minute hand", new Vector3(-9.35f, 2.46f, 8.485f), new Vector3(.021f, .15f, .015f), dark);
            Box("Clock hour hand", new Vector3(-9.31f, 2.39f, 8.484f), new Vector3(.10f, .023f, .015f), dark);
            Cylinder("Reception ceiling globe", new Vector3(-9.65f, 2.84f, 6.32f), new Vector3(.28f, .071f, .28f), lamp);
            WarmLight(new Vector3(-9.65f, 2.68f, 6.39f), 5.8f, 2.1f);
            Box("Desk sconce", new Vector3(-8.08f, 1.70f, 8.48f), new Vector3(.22f, .20f, .12f), lamp);
            WarmLight(new Vector3(-8.12f, 1.60f, 8.28f), 2.7f, .85f);
        }

        private void BuildReceptionComputer()
        {
            ReceptionComputer = Node("Reception supply computer", new Vector3(-10.28f, 1.09f, 7.47f));
            Piece(ReceptionComputer, "Monitor pedestal", new Vector3(0, .06f, -.01f),
                new Vector3(.40f, .102f, .36f), dark);
            Piece(ReceptionComputer, "CRT casing", new Vector3(0, .35f, .045f),
                new Vector3(.59f, .46f, .43f), trim, SolidLayer);
            Piece(ReceptionComputer, "CRT bezel", new Vector3(0, .363f, -.18f),
                new Vector3(.498f, .351f, .047f), dark);
            Material screen = Surface(new Color32(32, 69, 60, 255), .14f, 7);
            screen.SetColor("_EmissionColor", new Color(.07f, .28f, .17f));
            Piece(ReceptionComputer, "Glowing monitor glass", new Vector3(0, .382f, -.205f),
                new Vector3(.429f, .265f, .021f), screen);
            Sign("SUPPLIES", ReceptionComputer.position + new Vector3(0, .385f, -.219f), .075f,
                new Color32(195, 255, 208, 255));
            Piece(ReceptionComputer, "Keyboard", new Vector3(0, .023f, -.31f),
                new Vector3(.505f, .046f, .243f), dark);
            for (int row = 0; row < 4; row++)
                Piece(ReceptionComputer, "Keyboard key row", new Vector3(0, .054f, -.393f + row * .047f),
                    new Vector3(.434f, .014f, .023f), trim);
        }

        private void GableRoof(string name, float x, float y, float z, float width, float depth, float rise, bool acrossFront)
        {
            float run = (acrossFront ? width : depth) / 2;
            float ridge = acrossFront ? depth : width;
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + rise * rise) + .25f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = acrossFront ? new Vector3(x + side * run / 2, y + rise / 2, z)
                    : new Vector3(x, y + rise / 2, z + side * run / 2);
                Vector3 size = acrossFront ? new Vector3(length, .18f, ridge) : new Vector3(ridge, .18f, length);
                var roofHalf = Box(name + " roof slope", center, size, roof);
                roofHalf.transform.rotation = acrossFront ? Quaternion.Euler(0, 0, -side * slope)
                    : Quaternion.Euler(side * slope, 0, 0);
                float face = side * (ridge / 2 - .09f);
                var vertices = acrossFront
                    ? new[] { new Vector3(x - run, y, z + face), new Vector3(x, y + rise, z + face), new Vector3(x + run, y, z + face) }
                    : new[] { new Vector3(x + face, y, z - run), new Vector3(x + face, y + rise, z), new Vector3(x + face, y, z + run) };
                ShapeTwoSided(name + " timber gable", vertices, new[] { 0, 1, 2 }, siding);
            }
        }

        private void Glazing(float x, float y, float z, float width, float height)
        {
            Box("Window casing", new Vector3(x, y, z), new Vector3(width, height, .13f), trim);
            Box("Tinted window panes", new Vector3(x, y, z - .075f), new Vector3(width - .13f, height - .13f, .049f), window);
            Box("Vertical window mullion", new Vector3(x, y, z - .106f), new Vector3(.044f, height - .1f, .049f), trim);
            Box("Horizontal window mullion", new Vector3(x, y, z - .106f), new Vector3(width - .1f, .043f, .048f), trim);
            Box("Window sill", new Vector3(x, y - height / 2, z - .08f), new Vector3(width + .10f, .065f, .23f), trim);
        }

        private void Vent(float x, float y, float z)
        {
            Box("External AC housing", new Vector3(x, y, z), new Vector3(.70f, .36f, .26f), trim);
            for (int slat = 0; slat < 4; slat++)
                Box("AC vent slot", new Vector3(x, y - .10f + slat * .060f, z - .138f),
                    new Vector3(.55f, .020f, .018f), dark);
        }

        private void WarmLight(Vector3 p, float range, float intensity)
        {
            Light light = Node("Warm interior light", p).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .79f, .51f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private Transform Node(string name, Vector3 p)
        {
            var node = new GameObject(name).transform;
            node.SetParent(root, false);
            node.localPosition = p;
            return node;
        }

        private GameObject Piece(Transform parent, string name, Vector3 local, Vector3 size, Material material, int layer = 0)
        {
            GameObject piece = Box(name, Vector3.zero, size, material, layer);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = local;
            return piece;
        }

        private GameObject Cylinder(string name, Vector3 position, Vector3 size, Material material, int layer = 0)
            => Primitive(PrimitiveType.Cylinder, name, position, size, material, layer);

        private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 size,
            Material material, int layer = 0)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.layer = layer;
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            // Refer to the collider types explicitly so Web builds keep the
            // components used by Unity's runtime primitive factory.
            if (type == PrimitiveType.Cylinder && obj.TryGetComponent(out CapsuleCollider capsule))
                capsule.enabled = layer != 0;
            if (type == PrimitiveType.Sphere && obj.TryGetComponent(out SphereCollider sphere))
                sphere.enabled = layer != 0;
            return obj;
        }

        private void Shape(string name, Vector3[] vertices, int[] indices, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            GameObject obj = Node(name, Vector3.zero).gameObject;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
