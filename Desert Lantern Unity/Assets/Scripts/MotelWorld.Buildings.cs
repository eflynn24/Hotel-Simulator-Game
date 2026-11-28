using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public const float ReceptionExtension = .9f;
        public const float ReceptionBackWall = 8.58f + ReceptionExtension;
        public static readonly Vector3 ComputerApproach = new Vector3(-10.22f, 0, 8.24f);
        public static readonly Vector3 DeskFrontCorner = new Vector3(-11.30f, 0, 6.72f);
        public static readonly Vector3 DeskBackCorner = new Vector3(-11.30f, 0, 8.24f);
        public static readonly Vector3 ComputerScreen = new Vector3(-10.28f, 1.61f, 7.70f);
        public Transform ReceptionComputer { get; private set; }
        public static Vector3 RoomDoor(int room) => new Vector3(RoomX[room], FloorHeight(room), 6.43f);
        public static Vector3 BagDrop(int room) => new Vector3(RoomX[room] + .48f, FloorHeight(room), 10.69f);
        public static Vector3 RackPosition(int room) => new Vector3(RoomX[room] + 1.86f, FloorHeight(room) + .57f, 10.68f);
        public static int InteriorRoom(Vector3 position)
        {
            if (position.z < 6.50f || position.z > 13.35f)
                return -1;
            for (int room = 0; room < RoomX.Length; room++)
                if (Mathf.Abs(position.x - RoomX[room]) < 2.42f && NearFloor(position, RoomDoor(room)))
                    return room;
            return -1;
        }

        private Material siding, oak, trim, linen, runner, carpet, tile, stone, leaf, lamp, dark;
        private readonly Light[] roomLighting = new Light[MotelService.RoomCount];
        private readonly Transform[] roomDoors = new Transform[MotelService.RoomCount];
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
            if (room == 3 || room == 7 || room == 9 || room == 11)
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
            BuildRoomMess(room);
            BuildFurniture(room);
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
            tvIndicator[room] = Box("Activated television screen", new Vector3(x + 2.211f, 1.70f, 9.13f),
                new Vector3(.011f, .29f, .47f), teal);
            roomServiceTray[room] = Node("Room service tray", new Vector3(x + 1.71f, .778f, 7.61f)).gameObject;
            Piece(roomServiceTray[room].transform, "Meal tray", new Vector3(0, .035f, 0),
                new Vector3(.47f, .055f, .33f), iron);
            Piece(roomServiceTray[room].transform, "Covered plate", new Vector3(-.08f, .14f, 0),
                new Vector3(.28f, .17f, .23f), cream);
            Piece(roomServiceTray[room].transform, "Drink", new Vector3(.14f, .14f, .01f),
                new Vector3(.09f, .16f, .09f), teal);
            tvIndicator[room].SetActive(false);
            roomServiceTray[room].SetActive(false);
            Cylinder("Round table top", new Vector3(x + 1.71f, .73f, 7.61f), new Vector3(.48f, .044f, .48f), siding, SolidLayer);
            Cylinder("Table pedestal", new Vector3(x + 1.71f, .35f, 7.61f), new Vector3(.08f, .35f, .08f), iron);
            Cylinder("Table foot", new Vector3(x + 1.71f, .035f, 7.61f), new Vector3(.30f, .035f, .30f), iron);

            Vector3 rack = RackPosition(room) - roomBuildOffset;
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
            roomLighting[room] = WarmLight(new Vector3(x + .2f, 2.50f, 9.83f), 5.3f, 1.65f);
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
            Box("Upper balcony floor", new Vector3(3.5f, 2.97f, 5.70f), new Vector3(20.45f, .22f, 2.18f), trim, GroundLayer);
            Box("Balcony top rail", new Vector3(3.5f, 4.12f, 4.70f), new Vector3(20.45f, .105f, .13f), trim, SolidLayer);
            Box("Balcony lower rail", new Vector3(3.5f, 3.32f, 4.70f), new Vector3(20.45f, .095f, .12f), trim);
            for (int i = 0; i < 53; i++)
                Box("Balcony baluster", new Vector3(-6.65f + i * .39f, 3.72f, 4.70f),
                    new Vector3(.055f, .77f, .06f), trim);
            Box("Ground-floor walkway", new Vector3(3.5f, -.035f, 5.66f),
                new Vector3(20.3f, .09f, 2.22f), tile, GroundLayer);
            for (int i = 0; i <= 4; i++)
            {
                float x = -6.5f + 5f * i;
                Box("Walkway timber post", new Vector3(x, 2.69f, 4.71f), new Vector3(.17f, 5.38f, .17f), siding, SolidLayer);
                Box("Stone post base", new Vector3(x, .36f, 4.71f), new Vector3(.31f, .72f, .34f), stone, SolidLayer);
                Box("Parking stall stripe", new Vector3(x, .008f, 1.36f), new Vector3(.070f, .01f, 5.3f), trim);
            }
            Box("Upper walkway soffit", new Vector3(3.5f, 6.08f, 5.67f), new Vector3(20.7f, .15f, 2.20f), siding);
            GableRoof("Wing gable", 3.5f, 6.18f, 9.36f, 20.80f, 9.50f, 1.55f, false);
        }

        private void BuildExternalStairs()
        {
            const int steps = 18;
            Vector3 span = StairTop - StairFoot;
            float run = span.z;
            float rise = span.y;
            float length = span.magnitude;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var collision = Box("Stair walking surface", (StairFoot + StairTop) * .5f - Vector3.up * .10f,
                new Vector3(1.85f, .18f, length), trim, GroundLayer);
            collision.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            collision.GetComponent<MeshRenderer>().enabled = false;
            for (int step = 0; step < steps; step++)
            {
                float t = (step + .5f) / steps;
                Box("Stair tread", StairFoot + span * t - Vector3.up * .062f,
                    new Vector3(1.87f, .12f, run / steps + .035f), oak);
            }
            Box("Upper stair landing", new Vector3(14.13f, UpperFloor - .10f, 5.97f),
                new Vector3(3.29f, .20f, 1.66f), trim, GroundLayer);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 railSide = Vector3.right * (side * .99f);
                var handrail = Box("Stair handrail", (StairFoot + StairTop) * .5f + railSide + Vector3.up * 1.08f,
                    new Vector3(.12f, .12f, length), iron, SolidLayer);
                handrail.transform.rotation = Quaternion.Euler(-angle, 0, 0);
                for (int post = 0; post <= 6; post++)
                    Box("Stair rail post", StairFoot + span * (post / 6f) + railSide + Vector3.up * .50f,
                        new Vector3(.09f, 1f, .09f), iron, SolidLayer);
            }
            Box("Landing guard", new Vector3(14.16f, UpperFloor + .92f, 6.79f),
                new Vector3(3.14f, .12f, .12f), iron, SolidLayer);
            Sign("UPPER ROOMS", new Vector3(14.84f, 1.35f, -.27f), .20f, Color.white);
        }

        private void BuildReception(bool overview)
        {
            const float x = -9.65f;
            Box("Office floor", new Vector3(x, -.04f, 6.43f + ReceptionExtension / 2), new Vector3(4.65f, .095f, 4.65f + ReceptionExtension), tile, GroundLayer);
            Box("Rear office wall", new Vector3(x, 1.48f, 8.68f + ReceptionExtension), new Vector3(4.79f, 2.96f, .2f), oak, SolidLayer);
            Box("West wall window base", new Vector3(-12, .38f, 6.42f + ReceptionExtension / 2), new Vector3(.20f, .76f, 4.69f + ReceptionExtension), oak, SolidLayer);
            Box("West wall window header", new Vector3(-12, 2.68f, 6.42f + ReceptionExtension / 2), new Vector3(.20f, .56f, 4.69f + ReceptionExtension), oak, SolidLayer);
            Box("West wall south jamb", new Vector3(-12, 1.58f, 4.65f + ReceptionExtension), new Vector3(.20f, 1.65f, 1.15f), oak, SolidLayer);
            Box("West wall north jamb", new Vector3(-12, 1.58f, 8.0f + ReceptionExtension / 2), new Vector3(.20f, 1.65f, 1.40f + ReceptionExtension), oak, SolidLayer);
            GameObject window = Box("Picture window collision", new Vector3(-12, 1.58f, 6.275f),
                new Vector3(.16f, 1.65f, 2.10f), dark, SolidLayer);
            window.GetComponent<MeshRenderer>().enabled = false;
            Box("Office east wall", new Vector3(-7.31f, 1.48f, 6.42f + ReceptionExtension / 2), new Vector3(.20f, 2.96f, 4.69f + ReceptionExtension), oak, SolidLayer);
            Box("Entrance left jamb wall", new Vector3(-11.27f, 1.48f, 4.10f), new Vector3(1.65f, 2.96f, .20f), plaster, SolidLayer);
            Box("Entrance right jamb wall", new Vector3(-8.14f, 1.48f, 4.10f), new Vector3(1.86f, 2.96f, .20f), plaster, SolidLayer);
            Box("Entrance header", new Vector3(-9.75f, 2.64f, 4.10f), new Vector3(1.45f, .64f, .22f), siding, SolidLayer);
            Box("Office ceiling", new Vector3(x, 2.96f, 6.36f + ReceptionExtension / 2), new Vector3(4.95f, .15f, 4.98f + ReceptionExtension), linen, SolidLayer);
            Glazing(-8.18f, 1.55f, 3.96f, 1.35f, .91f);
            GableRoof("Office main roof", x, 3.03f, 6.31f + ReceptionExtension / 2, 5.25f, 5.50f + ReceptionExtension, 1.39f, true);
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

            Material walnut = Surface(new Color32(83, 45, 26, 255), .28f, 12);
            Material honey = Surface(new Color32(144, 83, 42, 255), .33f, 15);
            Material middleWood = Surface(new Color32(112, 62, 35, 255), .30f, 15);
            Material granite = Surface(new Color32(174, 143, 121, 255), .27f, 36);
            Material brass = Surface(new Color32(195, 142, 52, 255), .10f, 0);
            Material green = Surface(new Color32(39, 106, 39, 255), .10f, 0);
            Material[] wallStains = { middleWood, middleWood, honey, middleWood, walnut, middleWood };
            for (int board = 0; board < 32; board++)
                Box("Walnut wall panel", new Vector3(-11.90f + board * .145f, 1.46f, 8.573f + ReceptionExtension),
                    new Vector3(.144f, 2.91f, .011f), wallStains[board % wallStains.Length]);
            Box("Laminated counter base", new Vector3(-9.37f, .50f, 7.43f), new Vector3(2.87f, 1f, .70f), oak, SolidLayer);
            Box("Stone counter top", new Vector3(-9.38f, 1.04f, 7.43f), new Vector3(3.03f, .10f, .90f), granite, SolidLayer);
            Box("Counter return", new Vector3(-7.90f, .51f, 7.87f), new Vector3(.55f, 1.02f, 1.54f), oak, SolidLayer);
            Box("Return countertop", new Vector3(-7.90f, 1.04f, 7.87f), new Vector3(.63f, .10f, 1.59f), granite);
            for (int strip = 0; strip < 26; strip++)
                Box("Counter walnut slat", new Vector3(-10.754f + strip * .111f, .51f, 7.065f),
                    new Vector3(.107f, .86f, .028f), strip % 3 == 0 ? honey : middleWood);
            Box("Counter toe kick", new Vector3(-9.37f, .055f, 7.052f), new Vector3(2.9f, .11f, .06f), dark);
            Box("Counter apron", new Vector3(-9.37f, .98f, 7.046f), new Vector3(2.94f, .11f, .09f), walnut);
            Box("Under-counter light strip", new Vector3(-9.37f, .943f, 7.008f), new Vector3(2.7f, .017f, .036f), lamp);
            for (int segment = 0; segment < 3; segment++)
                WarmLight(new Vector3(-10.25f + segment * .87f, .81f, 6.96f), 1.05f, .25f);
            BuildReceptionComputer();
            Box("Desk blotter", new Vector3(-9.42f, 1.098f, 7.29f), new Vector3(.64f, .016f, .37f), dark);
            Box("Guest ledger", new Vector3(-8.99f, 1.111f, 7.43f), new Vector3(.23f, .035f, .29f), walnut);
            Box("Ledger pages", new Vector3(-8.99f, 1.133f, 7.43f), new Vector3(.20f, .012f, .25f), cream);
            Cylinder("Service bell base", new Vector3(-9.83f, 1.109f, 7.23f), new Vector3(.077f, .017f, .077f), dark);
            Cylinder("Brass service bell", new Vector3(-9.83f, 1.143f, 7.23f), new Vector3(.052f, .021f, .052f), brass);
            Cylinder("Banker lamp base", new Vector3(-8.25f, 1.107f, 7.27f), new Vector3(.122f, .02f, .122f), brass);
            Cylinder("Banker lamp stem", new Vector3(-8.25f, 1.27f, 7.27f), new Vector3(.019f, .16f, .019f), brass);
            Primitive(PrimitiveType.Sphere, "Green banker shade", new Vector3(-8.25f, 1.45f, 7.25f),
                new Vector3(.35f, .15f, .18f), green);
            Box("Banker shade lower rim", new Vector3(-8.25f, 1.422f, 7.25f), new Vector3(.32f, .019f, .135f), brass);
            WarmLight(new Vector3(-8.25f, 1.39f, 7.18f), 1.5f, .75f);
            heldReceptionKey = Node("Held room key", Vector3.zero);
            Piece(heldReceptionKey, "Held key bow", new Vector3(0, .045f, 0), new Vector3(.057f, .052f, .023f), brass);
            Piece(heldReceptionKey, "Held key shaft", new Vector3(0, -.024f, 0), new Vector3(.019f, .094f, .019f), brass);
            Piece(heldReceptionKey, "Held key tooth", new Vector3(.015f, -.06f, 0), new Vector3(.021f, .023f, .018f), brass);
            heldReceptionKey.gameObject.SetActive(false);
            Box("Walnut key rack", new Vector3(-9.43f, 1.65f, 8.50f + ReceptionExtension), new Vector3(1.13f, .70f, .15f), walnut);
            for (int room = 0; room < MotelService.RoomCount; room++)
            {
                Vector3 key = ReceptionKey(room);
                Box("Room number plate", key + Vector3.up * .09f, new Vector3(.18f, .072f, .025f), brass);
                Sign((room + 1).ToString("00"), key + new Vector3(0, .09f, -.026f), .068f, Color.black);
                Transform hook = Node("Room key " + (room + 1), key);
                Piece(hook, "Hanging brass key", Vector3.down * .044f, new Vector3(.019f, .11f, .021f), brass);
                Piece(hook, "Key bow", Vector3.up * .015f, new Vector3(.053f, .049f, .024f), brass);
                receptionKeys[room] = hook.gameObject;
            }
            Box("Daily checklist frame", new Vector3(-8.16f, 1.86f, 8.53f + ReceptionExtension), new Vector3(.75f, .83f, .08f), walnut);
            Box("Daily checklist paper", new Vector3(-8.16f, 1.86f, 8.48f + ReceptionExtension), new Vector3(.66f, .74f, .02f), cream);
            receptionChecklist = Sign("Today", new Vector3(-8.16f, 1.86f, 8.46f + ReceptionExtension), .102f, Color.black);
            receptionChecklist.lineSpacing = 1.13f;
            GameObject signFrame = Cylinder("Oval motel sign frame", new Vector3(-9.64f, 2.36f, 8.511f + ReceptionExtension),
                new Vector3(1.64f, .034f, .55f), walnut);
            signFrame.transform.rotation = Quaternion.Euler(90, 0, 0);
            GameObject signFace = Cylinder("Oval motel sign face", new Vector3(-9.64f, 2.36f, 8.471f + ReceptionExtension),
                new Vector3(1.58f, .017f, .50f), honey);
            signFace.transform.rotation = signFrame.transform.rotation;
            Sign("Desert Lantern", new Vector3(-9.64f, 2.41f, 8.444f + ReceptionExtension), .164f, Color.black).fontStyle = FontStyle.BoldAndItalic;
            Sign("M O T E L", new Vector3(-9.64f, 2.24f, 8.444f + ReceptionExtension), .089f, Color.black);
            Box("Motel license frame", new Vector3(-8.16f, 2.48f, 8.53f + ReceptionExtension), new Vector3(.46f, .25f, .065f), walnut);
            Box("License paper", new Vector3(-8.16f, 2.48f, 8.49f + ReceptionExtension), new Vector3(.37f, .18f, .021f), cream);
            Sign("Motel License", new Vector3(-8.16f, 2.49f, 8.473f + ReceptionExtension), .059f, Color.black);
            Box("Reception shelving", new Vector3(-10.97f, 1.75f, 8.50f + ReceptionExtension), new Vector3(.71f, 1.18f, .14f), walnut);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 1.21f + shelf * .38f;
                Box("Walnut shelf", new Vector3(-10.97f, y, 8.35f + ReceptionExtension), new Vector3(.72f, .045f, .32f), honey);
                for (int binder = 0; binder < 4; binder++)
                    Box("Office binder", new Vector3(-11.21f + binder * .14f, y + .13f, 8.36f + ReceptionExtension),
                        new Vector3(.102f, .22f - .03f * (binder % 2), .21f), binder % 2 == 0 ? dark : cream);
            }
            FloorRug(-9.66f, 5.64f, walnut, honey);
            BuildFurniture(MotelService.ReceptionFurniture);
            Box("Picture window sill", new Vector3(-11.91f, .77f, 6.27f), new Vector3(.23f, .10f, 2.23f), walnut);
            Box("Window mullion", new Vector3(-11.89f, 1.55f, 6.27f), new Vector3(.09f, 1.58f, .059f), walnut);
            for (int slat = 0; slat < 8; slat++)
                Box("Venetian blind slat", new Vector3(-11.86f, 2.34f - slat * .052f, 6.27f),
                    new Vector3(.088f, .034f, 2.12f), honey);
            Box("Vacancy sign casing", new Vector3(-11.83f, 1.76f, 5.89f), new Vector3(.068f, .21f, .87f), dark);
            TextMesh vacancy = Sign("VACANCY", new Vector3(-11.788f, 1.76f, 5.89f), .144f,
                new Color32(255, 102, 54, 255));
            vacancy.transform.rotation = Quaternion.Euler(0, -90, 0);
            Box("Staff door casing", new Vector3(-7.53f, 1.05f, 8.518f), new Vector3(.40f, 2.10f, .079f), honey);
            Box("Staff door", new Vector3(-7.53f, 1.04f, 8.463f), new Vector3(.35f, 2f, .035f), walnut);
            Sign("STAFF", new Vector3(-7.53f, 1.64f, 8.437f), .058f, cream.color);
            Box("Lobby chair seat", new Vector3(-11.46f, .40f, 5.73f), new Vector3(.55f, .20f, .65f), linen, SolidLayer);
            Box("Lobby chair back", new Vector3(-11.68f, .71f, 5.73f), new Vector3(.16f, .48f, .65f), linen);
            for (int arm = -1; arm <= 1; arm += 2)
                Box("Chair arm", new Vector3(-11.45f, .59f, 5.73f + arm * .32f), new Vector3(.57f, .06f, .07f), walnut);
            BuildLobbyPlant(new Vector3(-10.98f, 2.11f, 8.29f), .62f);
            BuildLobbyPlant(new Vector3(-7.73f, .24f, 6.37f), .92f);
            BuildLobbyPlant(new Vector3(-11.44f, .21f, 6.68f), .74f);
            for (int fixture = 0; fixture < 2; fixture++)
            {
                float lightX = -10.80f + fixture * 2.26f;
                Cylinder("Recessed counter light", new Vector3(lightX, 2.866f, 7.75f + ReceptionExtension / 2), new Vector3(.27f, .026f, .27f), lamp);
                WarmLight(new Vector3(lightX, 2.70f, 7.74f), 2.9f, .40f);
            }
            GameObject clock = Cylinder("Wall clock face", new Vector3(-7.56f, 2.46f, 8.526f),
                new Vector3(.30f, .031f, .30f), granite);
            clock.transform.rotation = Quaternion.Euler(90, 0, 0);
            clockMinuteHand = Node("Clock minute pivot", new Vector3(-7.56f, 2.46f, 8.482f));
            clockHour = Node("Clock hour pivot", new Vector3(-7.56f, 2.46f, 8.470f));
            Piece(clockMinuteHand, "Clock minute hand", new Vector3(0, .079f, 0),
                new Vector3(.021f, .169f, .012f), dark);
            Piece(clockHour, "Clock hour hand", new Vector3(0, .05f, 0),
                new Vector3(.028f, .112f, .012f), dark);
            Cylinder("Reception ceiling globe", new Vector3(-9.65f, 2.84f, 6.32f), new Vector3(.28f, .071f, .28f), lamp);
            WarmLight(new Vector3(-9.65f, 2.68f, 6.39f), 5.8f, 1.25f);
            Box("Motel sign lamp", new Vector3(-9.64f, 2.72f, 8.40f), new Vector3(.39f, .075f, .18f), dark);
            WarmLight(new Vector3(-9.64f, 2.67f, 8.31f), 1.2f, .40f);
        }

        private void BuildLobbyPlant(Vector3 potTop, float scale)
        {
            Cylinder("Ceramic planter", potTop + Vector3.down * .10f * scale,
                new Vector3(.32f, .13f, .32f) * scale, stone);
            for (int stem = 0; stem < 9; stem++)
            {
                float angle = stem * 2.39996f;
                float radius = (.12f + stem % 3 * .08f) * scale;
                Vector3 center = potTop + new Vector3(Mathf.Cos(angle) * radius, .16f + stem % 4 * .09f,
                    Mathf.Sin(angle) * radius);
                var blade = Primitive(PrimitiveType.Sphere, "Planter leaf", center,
                    new Vector3(.11f, .036f, .30f) * scale, leaf);
                blade.transform.rotation = Quaternion.Euler(40 - stem * 5, angle * Mathf.Rad2Deg + 90, 0);
            }
        }

        private TextMesh receptionChecklist;
        private Transform heldReceptionKey;
        private readonly GameObject[] receptionKeys = new GameObject[MotelService.RoomCount];

        public void ShowReceptionKeys(int unavailable, MotelWalker carrier)
        {
            heldReceptionKey.gameObject.SetActive(carrier != null);
            if (carrier != null)
                heldReceptionKey.SetPositionAndRotation(carrier.CarryPosition, carrier.Orientation);
            for (int room = 0; room < receptionKeys.Length; room++)
                if (receptionKeys[room] != null)
                    receptionKeys[room].SetActive((unavailable & (1 << room)) == 0);
        }

        public static Vector3 ReceptionKey(int room) =>
            new Vector3(-9.84f + room % 4 * .27f, 1.85f - room / 4 * .205f, 8.392f + ReceptionExtension);

        public void ShowReceptionChecklist(int arrivals, int departures, int requests, int deliveries)
        {
            if (receptionChecklist == null)
                return;
            string text = $"Today\nCheck-ins  {arrivals}\nCheck-outs  {departures}\nRequests  {requests}\nDeliveries  {deliveries}";
            if (receptionChecklist.text != text)
                receptionChecklist.text = text;
        }

        private void FloorRug(float x, float z, Material border, Material inset)
        {
            Box("Lobby rug border", new Vector3(x, .015f, z), new Vector3(2.12f, .014f, 1.38f), border);
            Box("Lobby rug field", new Vector3(x, .024f, z), new Vector3(1.98f, .008f, 1.24f), inset);
        }

        private void BuildReceptionComputer()
        {
            ReceptionComputer = Node("Motel Manager computer", new Vector3(-10.28f, 1.09f, 7.47f));
            Piece(ReceptionComputer, "Monitor pedestal", new Vector3(0, .06f, -.01f),
                new Vector3(.40f, .102f, .36f), dark);
            Piece(ReceptionComputer, "Flat monitor casing", new Vector3(0, .35f, -.145f),
                new Vector3(.59f, .46f, .055f), dark, SolidLayer);
            Piece(ReceptionComputer, "CRT bezel", new Vector3(0, .363f, -.18f),
                new Vector3(.498f, .351f, .047f), dark);
            Material screen = Surface(new Color32(41, 46, 83, 255), .14f, 7);
            screen.SetColor("_EmissionColor", new Color(.09f, .10f, .22f));
            Piece(ReceptionComputer, "Glowing monitor glass", new Vector3(0, .382f, -.205f),
                new Vector3(.429f, .265f, .021f), screen);
            TextMesh display = Sign("Motel Manager\nReservations\nGuests · Supplies\nStaff", ReceptionComputer.position + new Vector3(0, .385f, -.219f), .062f,
                new Color32(195, 255, 208, 255));
            display.transform.SetParent(ReceptionComputer, true);
            Piece(ReceptionComputer, "Keyboard", new Vector3(0, .023f, -.31f),
                new Vector3(.505f, .046f, .243f), dark);
            for (int row = 0; row < 4; row++)
                Piece(ReceptionComputer, "Keyboard key row", new Vector3(0, .054f, -.393f + row * .047f),
                    new Vector3(.434f, .014f, .023f), trim);
            ReceptionComputer.Rotate(0, 180, 0);
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

        private Light WarmLight(Vector3 p, float range, float intensity)
        {
            Light light = Node("Warm interior light", p).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .79f, .51f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private Transform Node(string name, Vector3 p)
        {
            var node = new GameObject(name).transform;
            node.SetParent(root, false);
            node.localPosition = p + roomBuildOffset;
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
            obj.transform.localPosition = position + roomBuildOffset;
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
