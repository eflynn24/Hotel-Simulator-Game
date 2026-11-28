using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public static readonly Vector3 LaundryApproach = new Vector3(-15.37f, 0, 6.49f);
        private readonly GameObject[,] roomMess = new GameObject[MotelService.RoomCount, MotelService.CleaningTaskCount];
        private readonly TextMesh[] doorNotices = new TextMesh[MotelService.RoomCount];
        private readonly GameObject[] lostProperty = new GameObject[MotelService.RoomCount];
        public static readonly Vector3 CartStation = new Vector3(-13.09f, 0, 6.66f);
        private Transform housekeepingCart;
        private TextMesh laundryStatus;
        private TextMesh laundryStock;
        private Transform storedVacuum;
        private readonly Transform[] cleaningTools = new Transform[MotelService.CleaningTaskCount];

        public static Vector3 CleaningPoint(int room, CleaningTask task)
        {
            float x = RoomX[room];
            Vector3 point = task switch
            {
                CleaningTask.Trash => new Vector3(x + .5f, 0, 11.62f),
                CleaningTask.Wipe => new Vector3(x + .92f, 0, 7.62f),
                CleaningTask.Vacuum => new Vector3(x + .59f, 0, 9.07f),
                CleaningTask.Mop => new Vector3(x + .5f, 0, 8.15f),
                CleaningTask.Bathroom => new Vector3(x + .63f, 0, 12.18f),
                CleaningTask.Towels or CleaningTask.Restock => new Vector3(x + .54f, 0, 11.44f),
                _ => new Vector3(x - .02f, 0, 10.41f)
            };
            return point + Vector3.up * FloorHeight(room);
        }

        private void BuildRoomMess(int room)
        {
            float x = RoomX[room];
            doorNotices[room] = Sign("", new Vector3(x, 1.69f, 6.359f), .092f, new Color32(255, 214, 145, 255));
            doorNotices[room].gameObject.SetActive(false);
            lostProperty[room] = Box("Forgotten travel pouch", new Vector3(x + 1.78f, .58f, 10.70f),
                new Vector3(.27f, .19f, .30f), teal);
            lostProperty[room].SetActive(false);
            Box("Bathroom tile", new Vector3(x + 1.36f, .012f, 12.04f), new Vector3(1.85f, .015f, 1.35f), tile);
            Box("Toilet cistern", new Vector3(x + .55f, .65f, 12.29f), new Vector3(.30f, .41f, .22f), linen);
            Cylinder("Toilet bowl", new Vector3(x + .55f, .37f, 12.12f), new Vector3(.31f, .10f, .38f), linen);
            Box("Bathroom vanity", new Vector3(x + 1.16f, .63f, 12.25f), new Vector3(.49f, .70f, .36f), trim);
            Cylinder("Sink basin", new Vector3(x + 1.16f, .99f, 12.23f), new Vector3(.33f, .036f, .26f), linen);
            Box("Sink tap", new Vector3(x + 1.16f, 1.065f, 12.35f), new Vector3(.042f, .118f, .067f), iron);
            Box("Shower mat", new Vector3(x + 1.90f, .043f, 12.18f), new Vector3(.55f, .035f, .51f), linen);
            Box("Shower riser", new Vector3(x + 2.31f, 1.45f, 12.15f), new Vector3(.038f, 1.31f, .039f), iron);
            Box("Shower head", new Vector3(x + 2.23f, 2.09f, 12.15f), new Vector3(.19f, .069f, .095f), iron);
            Material spill = Surface(new Color32(87, 62, 33, 255), .55f, 30);
            Material floorDirt = Surface(new Color32(76, 61, 52, 255), .64f, 24);
            Material dirtyLinen = Surface(new Color32(129, 115, 94, 255), .65f, 17);

            Transform trash = Node("Discarded trash", CleaningPoint(room, CleaningTask.Trash) - roomBuildOffset);
            Piece(trash, "Crumpled paper", new Vector3(-.27f, .041f, .05f),
                new Vector3(.27f, .064f, .19f), linen).transform.localRotation = Quaternion.Euler(4, 24, -7);
            Piece(trash, "Food carton", new Vector3(.18f, .053f, -.15f),
                new Vector3(.19f, .092f, .13f), cream).transform.localRotation = Quaternion.Euler(0, -26, 2);
            Piece(trash, "Discarded wrapper", new Vector3(.27f, .032f, .18f),
                new Vector3(.18f, .035f, .095f), teal);
            Piece(trash, "Empty tin", new Vector3(.08f, .063f, .19f),
                new Vector3(.13f, .10f, .27f), iron).transform.localRotation = Quaternion.Euler(0, 32, 9);

            Transform table = Node("Sticky tabletop spill", new Vector3(x + 1.71f, .779f, 7.61f));
            for (int i = 0; i < 5; i++)
                Piece(table, "Coffee spill edge", new Vector3((i - 2) * .068f, 0, Mathf.Sin(i * 1.4f) * .069f),
                    new Vector3(.12f, .004f, .14f), spill).transform.localRotation = Quaternion.Euler(0, i * 23, 0);

            Transform floor = Node("Carpet debris", CleaningPoint(room, CleaningTask.Vacuum) - roomBuildOffset);
            for (int i = 0; i < 11; i++)
            {
                float angle = i * 2.39f;
                float radius = .16f + .078f * (i % 5);
                Piece(floor, "Carpet crumbs", new Vector3(Mathf.Cos(angle) * radius, .019f, Mathf.Sin(angle) * radius),
                    new Vector3(.075f + i % 3 * .036f, .025f, .07f), i % 3 == 0 ? cream : floorDirt)
                    .transform.localRotation = Quaternion.Euler(0, i * 38, 0);
            }

            Transform bedding = Node("Rumpled bedding", Vector3.zero);
            Transform stripped = Node("Stripped beds", Vector3.zero);
            foreach (float z in new[] { 8.20f, 11.34f })
            {
                Piece(bedding, "Used sheet", new Vector3(x - 1.09f, .735f, z + .13f),
                    new Vector3(1.29f, .055f, .68f), dirtyLinen).transform.localRotation = Quaternion.Euler(0, -9, 0);
                Piece(bedding, "Twisted blanket", new Vector3(x - .91f, .80f, z - .14f),
                    new Vector3(1.05f, .14f, .30f), runner).transform.localRotation = Quaternion.Euler(2, 28, 4);
                Piece(bedding, "Folded blanket edge", new Vector3(x - .46f, .54f, z + .23f),
                    new Vector3(.22f, .48f, .49f), runner).transform.localRotation = Quaternion.Euler(0, -18, -12);
                Piece(stripped, "Bare mattress cover", new Vector3(x - 1.28f, .712f, z),
                    new Vector3(1.97f, .035f, 1.22f), trim);
            }

            roomMess[room, (int)CleaningTask.Trash] = trash.gameObject;
            roomMess[room, (int)CleaningTask.Wipe] = table.gameObject;
            roomMess[room, (int)CleaningTask.Vacuum] = floor.gameObject;
            roomMess[room, (int)CleaningTask.Strip] = bedding.gameObject;
            roomMess[room, (int)CleaningTask.MakeBeds] = stripped.gameObject;
            foreach (CleaningTask task in new[] { CleaningTask.Mop, CleaningTask.Bathroom, CleaningTask.Towels })
            {
                Vector3 position = CleaningPoint(room, task) - roomBuildOffset;
                Vector3 size = task == CleaningTask.Towels ? new Vector3(.28f, .055f, .32f) : new Vector3(.39f, .012f, .27f);
                roomMess[room, (int)task] = Box(task == CleaningTask.Towels ? "Used towel" : "Grimy surface",
                    position + Vector3.up * .026f, size, task == CleaningTask.Towels ? dirtyLinen : spill);
            }
            for (int task = 0; task < MotelService.CleaningTaskCount; task++)
                roomMess[room, task]?.SetActive(false);
        }

        public static readonly Vector3 UtilityApproach = new Vector3(-14.55f, 0, 3.46f);
        public static readonly Vector3 UtilityThreshold = new Vector3(-14.55f, 0, 4.94f);

        public static bool InsideUtility(Vector3 point) =>
            point.y < .75f && point.x > -16.83f && point.x < -12.10f &&
            point.z > 4.11f && point.z < 8.27f;

        private void BuildUtilityRoom(bool overview)
        {
            const float center = -14.49f;
            const float depth = 4.24f;
            Material paintedBlock = Surface(new Color32(183, 159, 133, 255), .41f, 25, .18f);
            ApplySurfaceDetail(paintedBlock, "Plaster", .52f, .74f);
            paintedBlock.SetFloat("_StuccoWear", .51f);
            Box("Utility tiled concrete floor", new Vector3(center, -.032f, 6.22f),
                new Vector3(4.76f, .09f, depth), tile, GroundLayer);
            Box("Utility west wall", new Vector3(-16.91f, 1.41f, 6.22f),
                new Vector3(.18f, 2.82f, depth), paintedBlock, SolidLayer);
            Box("Utility service window half wall", new Vector3(-12.14f, .38f, 6.22f),
                new Vector3(.12f, .76f, depth), paintedBlock, SolidLayer);
            Box("Utility service window header", new Vector3(-12.14f, 2.60f, 6.22f),
                new Vector3(.12f, .44f, depth), paintedBlock, SolidLayer);
            Box("Utility service window front jamb", new Vector3(-12.14f, 1.52f, 4.66f),
                new Vector3(.12f, 1.53f, 1.12f), paintedBlock, SolidLayer);
            Box("Utility service window rear jamb", new Vector3(-12.14f, 1.52f, 7.82f),
                new Vector3(.12f, 1.53f, 1.04f), paintedBlock, SolidLayer);
            Box("Utility back wall", new Vector3(center, 1.41f, 8.36f),
                new Vector3(4.96f, 2.82f, .18f), paintedBlock, SolidLayer);
            Box("Utility front west", new Vector3(-16.09f, 1.41f, 4.10f),
                new Vector3(1.66f, 2.82f, .18f), facade, SolidLayer);
            Box("Utility front east", new Vector3(-13.02f, 1.41f, 4.10f),
                new Vector3(1.80f, 2.82f, .18f), facade, SolidLayer);
            Box("Utility lintel", new Vector3(-14.55f, 2.58f, 4.10f),
                new Vector3(1.42f, .48f, .20f), plaster, SolidLayer);
            Box("Utility ceiling", new Vector3(center, 2.86f, 6.22f),
                new Vector3(4.94f, .14f, 4.44f), paintedBlock, SolidLayer);
            GableRoof("Utility room roof", center, 2.88f, 6.18f, 5.06f, 4.66f, .52f, true);
            Box("Utility door landing", new Vector3(-14.55f, -.04f, 3.63f),
                new Vector3(1.65f, .10f, 1.02f), stone, GroundLayer);
            Box("Housekeeping enamel plaque", new Vector3(-14.55f, 2.67f, 3.977f),
                new Vector3(1.59f, .24f, .065f), teal);
            Sign("HOUSEKEEPING", new Vector3(-14.55f, 2.67f, 3.94f), .17f, Color.white);
            if (overview)
                return;
            for (int side = -1; side <= 1; side += 2)
                Box("Painted steel door jamb", new Vector3(-14.55f + side * .69f, 1.14f, 4.08f),
                    new Vector3(.064f, 2.28f, .23f), teal);
            Box("Utility service counter", new Vector3(-12.16f, .80f, 6.27f),
                new Vector3(.34f, .084f, 2.13f), oak);
            for (int side = -1; side <= 1; side += 2)
                Box("Service window timber casing", new Vector3(-12.217f, 1.55f, 6.27f + side * 1.04f),
                    new Vector3(.051f, 1.53f, .064f), siding);
            for (int joint = 1; joint <= 7; joint++)
                Box("Utility floor grout", new Vector3(-16.81f + joint * .59f, .014f, 6.22f),
                    new Vector3(.016f, .003f, 4.12f), stone);
            for (int joint = 1; joint <= 6; joint++)
                Box("Utility floor grout", new Vector3(center, .014f, 4.13f + joint * .59f),
                    new Vector3(4.66f, .003f, .016f), stone);
            Box("Utility floor drain", new Vector3(-15.07f, .018f, 6.24f),
                new Vector3(.21f, .013f, .21f), iron);
            for (int strip = 0; strip < 5; strip++)
                Box("Floor drain slot", new Vector3(-15.15f + strip * .04f, .026f, 6.24f),
                    new Vector3(.012f, .005f, .165f), dark);
            Box("Wet-area wall guard", new Vector3(-16.808f, .24f, 6.22f),
                new Vector3(.025f, .35f, 4.06f), tile);
            Box("Utility fluorescent housing", new Vector3(-14.46f, 2.73f, 6.36f),
                new Vector3(1.29f, .11f, .31f), iron);
            Box("Utility light diffuser", new Vector3(-14.46f, 2.661f, 6.36f),
                new Vector3(1.21f, .045f, .235f), lamp);
            Light light = WarmLight(new Vector3(-14.46f, 2.49f, 6.36f), 5.6f, 1.76f);
            light.color = new Color(1f, .88f, .73f);
            BuildUtilityFixtures();
        }

        private void BuildLaundry()
        {
            Vector3 washer = new Vector3(-16.16f, .53f, 6.49f);
            Material enamel = Surface(new Color32(208, 211, 206, 255), .14f, 28, .46f);
            Material steel = Surface(new Color32(151, 159, 161, 255), .18f, 29, .49f, .56f);
            Box("Washing machine cabinet", washer, new Vector3(.70f, 1.06f, .89f), enamel, SolidLayer);
            Box("Washer kick plate", washer + new Vector3(.34f, -.45f, 0),
                new Vector3(.041f, .09f, .82f), steel);
            Box("Washer controls", washer + new Vector3(.353f, .39f, 0),
                new Vector3(.025f, .135f, .78f), dark);
            Cylinder("Washer program dial", washer + new Vector3(.38f, .39f, -.24f),
                new Vector3(.092f, .027f, .092f), steel).transform.rotation = Quaternion.Euler(0, 0, 90);
            Cylinder("Washer gasket", washer + new Vector3(.361f, -.03f, 0),
                new Vector3(.57f, .043f, .57f), dark).transform.rotation = Quaternion.Euler(0, 0, 90);
            Cylinder("Washer drum glass", washer + new Vector3(.406f, -.03f, 0),
                new Vector3(.46f, .032f, .46f), window).transform.rotation = Quaternion.Euler(0, 0, 90);
            Vector3 dryer = washer + new Vector3(0, -.035f, -.94f);
            Box("Dryer cabinet", dryer, new Vector3(.67f, 1.01f, .84f), enamel, SolidLayer);
            Box("Dryer controls", dryer + new Vector3(.342f, .35f, 0),
                new Vector3(.033f, .127f, .78f), dark);
            Box("Dryer door inset", dryer + new Vector3(.35f, -.06f, 0),
                new Vector3(.042f, .51f, .58f), steel);
            Box("Dryer handle", dryer + new Vector3(.385f, .11f, .13f), new Vector3(.042f, .045f, .22f), trim);
            Box("Laundry hamper", new Vector3(-16.18f, .31f, 7.34f),
                new Vector3(.63f, .60f, .63f), oak, SolidLayer);
            Box("Hamper rim", new Vector3(-16.18f, .62f, 7.34f),
                new Vector3(.65f, .05f, .65f), trim);
            Box("Washer conduit", new Vector3(-16.77f, 1.46f, 6.62f),
                new Vector3(.065f, .92f, .044f), steel);
            Sign("LAUNDRY", new Vector3(-16.16f, 1.52f, 6.02f), .16f, Color.white);
            laundryStatus = Sign("Ready", new Vector3(-16.16f, 1.33f, 6.02f), .12f, Color.white);
            laundryStock = Sign("Bags 0   Soap 0", new Vector3(-16.16f, 1.16f, 6.02f), .11f, Color.white);
            storedVacuum = BuildVacuum("Stored vacuum", new Vector3(-12.69f, 0, 5.29f));
            storedVacuum.gameObject.SetActive(false);

            housekeepingCart = Node("Housekeeping cart", CartStation);
            Piece(housekeepingCart, "Low cart shelf", new Vector3(0, .29f, 0), new Vector3(.38f, .07f, .51f), oak);
            Piece(housekeepingCart, "Top cart shelf", new Vector3(0, .76f, 0), new Vector3(.44f, .075f, .55f), trim);
            Piece(housekeepingCart, "Folded linens", new Vector3(-.09f, .80f, .04f), new Vector3(.18f, .08f, .26f), linen);
            Piece(housekeepingCart, "Disinfectant bottle", new Vector3(.12f, .87f, -.11f), new Vector3(.08f, .16f, .08f), teal);
            Piece(housekeepingCart, "Roll of bags", new Vector3(.08f, .81f, .11f), new Vector3(.10f, .09f, .17f), dark);
            Piece(housekeepingCart, "Canvas laundry bag", new Vector3(-.236f, .42f, .03f),
                new Vector3(.12f, .52f, .44f), siding);
            Piece(housekeepingCart, "Canvas bag hem", new Vector3(-.24f, .684f, .03f),
                new Vector3(.13f, .028f, .45f), oak);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner % 2 == 0 ? -.17f : .17f;
                float z = corner < 2 ? -.21f : .21f;
                Piece(housekeepingCart, "Cart frame", new Vector3(x, .47f, z), new Vector3(.04f, .63f, .042f), iron);
                Piece(housekeepingCart, "Cart caster", new Vector3(x, .13f, z), new Vector3(.08f, .14f, .12f), dark);
            }

            cleaningTools[(int)CleaningTask.Vacuum] = BuildVacuum("Vacuum in use", Vector3.zero);
            Transform mop = Node("Mop in use", Vector3.zero);
            Piece(mop, "Mop handle", new Vector3(0, .55f, 0), new Vector3(.038f, 1.10f, .039f), oak);
            Piece(mop, "Cotton mop head", new Vector3(0, .045f, 0), new Vector3(.26f, .07f, .18f), linen);
            cleaningTools[(int)CleaningTask.Mop] = mop;
            Transform brush = Node("Toilet brush", Vector3.zero);
            Piece(brush, "Brush handle", new Vector3(0, .082f, 0), new Vector3(.027f, .23f, .027f), trim);
            Piece(brush, "Brush bristles", new Vector3(0, -.045f, 0), new Vector3(.086f, .058f, .086f), dark);
            cleaningTools[(int)CleaningTask.Bathroom] = brush;
            Transform basket = Node("Laundry basket", Vector3.zero);
            Piece(basket, "Basket shell", Vector3.zero, new Vector3(.32f, .21f, .28f), oak);
            Piece(basket, "Basket linen", new Vector3(0, .11f, 0), new Vector3(.27f, .068f, .24f), linen);
            cleaningTools[(int)CleaningTask.Towels] = basket;
            for (int index = 0; index < cleaningTools.Length; index++)
            {
                if (cleaningTools[index] == null)
                {
                    var task = (CleaningTask)index;
                    Transform tool = Node("Housekeeping tool " + task, Vector3.zero);
                    Material material = task == CleaningTask.Trash ? dark : task == CleaningTask.Wipe ? teal : linen;
                    Vector3 size = task == CleaningTask.Trash ? new Vector3(.27f, .42f, .23f)
                        : task == CleaningTask.Wipe ? new Vector3(.22f, .058f, .18f)
                        : new Vector3(.33f, .155f, .28f);
                    Piece(tool, task.ToString(), Vector3.zero, size, material);
                    cleaningTools[index] = tool;
                }
                cleaningTools[index].gameObject.SetActive(false);
            }
        }

        private Transform BuildVacuum(string name, Vector3 position)
        {
            Transform model = Node(name, position);
            Piece(model, "Floor head", new Vector3(0, .054f, .13f), new Vector3(.30f, .10f, .26f), dark);
            Piece(model, "Vacuum housing", new Vector3(0, .30f, -.01f), new Vector3(.20f, .41f, .16f), teal);
            Piece(model, "Vacuum wand", new Vector3(0, .64f, -.03f), new Vector3(.053f, .44f, .056f), iron)
                .transform.localRotation = Quaternion.Euler(-7, 0, 0);
            Piece(model, "Hand grip", new Vector3(0, .88f, -.065f), new Vector3(.16f, .056f, .066f), dark);
            return model;
        }

        public void ShowCleaningTool(CleaningTask selected, MotelWalker worker, bool usingTool)
        {
            for (int i = 0; i < cleaningTools.Length; i++)
            {
                Transform tool = cleaningTools[i];
                bool show = usingTool && worker != null && i == (int)selected;
                if (tool.gameObject.activeSelf != show)
                    tool.gameObject.SetActive(show);
                if (!show)
                    continue;
                Vector3 position = (selected == CleaningTask.Vacuum || selected == CleaningTask.Mop)
                    ? worker.Position + worker.Orientation * new Vector3(.30f, 0, .44f)
                    : worker.CarryPosition;
                tool.SetPositionAndRotation(position, worker.Orientation);
            }
        }

        public void ShowHousekeeping(MotelService service, MotelWalker cartCarrier)
        {
            housekeepingCart.gameObject.SetActive(!service.CartInUse || cartCarrier != null);
            if (cartCarrier != null)
                housekeepingCart.SetPositionAndRotation(cartCarrier.Position + cartCarrier.Orientation * new Vector3(.42f, 0, .52f),
                    cartCarrier.Orientation);
            else
                housekeepingCart.SetPositionAndRotation(CartStation + Vector3.left * .48f, Quaternion.identity);
            for (int room = 0; room < RoomX.Length; room++)
            {
                string notice = service.DoNotDisturb(room) ? "DO NOT DISTURB" : service.RoomUnusable(room) ? "OUT OF SERVICE" : "";
                TextMesh sign = doorNotices[room];
                if (sign.text != notice)
                    sign.text = notice;
                sign.gameObject.SetActive(notice.Length > 0);
                lostProperty[room].SetActive(service.ForgottenItem(room));
                for (int i = 0; i < MotelService.CleaningTaskCount; i++)
                {
                    CleaningTask task = (CleaningTask)i;
                    bool visible = service.TaskPending(room, task);
                    if (task == CleaningTask.MakeBeds)
                        visible &= !service.TaskPending(room, CleaningTask.Strip);
                    roomMess[room, i]?.SetActive(visible);
                }
            }
            if (storedVacuum.gameObject.activeSelf != (service.Vacuums > 0))
                storedVacuum.gameObject.SetActive(service.Vacuums > 0);
            string stock = $"Bags {service.TrashBags}   Soap {service.Detergent}";
            if (laundryStock.text != stock)
                laundryStock.text = stock;
            string status = service.WasherBroken ? "Out of order" : service.LaundryRunning ? "Washing" : "Ready";
            if (laundryStatus.text != status)
                laundryStatus.text = status;
        }
    }
}
