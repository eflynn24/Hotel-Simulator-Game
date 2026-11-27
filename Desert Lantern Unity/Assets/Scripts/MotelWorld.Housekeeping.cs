using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public static readonly Vector3 LaundryApproach = new Vector3(-10.78f, 0, 5.28f);
        private readonly GameObject[,] roomMess = new GameObject[4, MotelService.CleaningTaskCount];
        private TextMesh laundryStatus;
        private TextMesh laundryStock;
        private Transform storedVacuum;
        private readonly Transform[] cleaningTools = new Transform[MotelService.CleaningTaskCount];

        public static Vector3 CleaningPoint(int room, CleaningTask task)
        {
            float x = RoomX[room];
            return task switch
            {
                CleaningTask.Trash => new Vector3(x + .5f, 0, 11.62f),
                CleaningTask.Wipe => new Vector3(x + .92f, 0, 7.62f),
                CleaningTask.Vacuum => new Vector3(x + .59f, 0, 9.07f),
                _ => new Vector3(x - .02f, 0, 10.41f)
            };
        }

        private void BuildRoomMess(int room)
        {
            float x = RoomX[room];
            Material spill = Surface(new Color32(87, 62, 33, 255), .55f, 30);
            Material floorDirt = Surface(new Color32(76, 61, 52, 255), .64f, 24);
            Material dirtyLinen = Surface(new Color32(129, 115, 94, 255), .65f, 17);

            Transform trash = Node("Discarded trash", CleaningPoint(room, CleaningTask.Trash));
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

            Transform floor = Node("Carpet debris", CleaningPoint(room, CleaningTask.Vacuum));
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
            for (int task = 0; task < MotelService.CleaningTaskCount; task++)
                roomMess[room, task].SetActive(false);
        }

        private void BuildLaundry()
        {
            Vector3 washer = new Vector3(-11.55f, .53f, 5.28f);
            Box("Washing machine cabinet", washer, new Vector3(.70f, 1.06f, .89f), trim, SolidLayer);
            Box("Washer controls", washer + new Vector3(.353f, .39f, 0),
                new Vector3(.025f, .135f, .78f), dark);
            var door = Cylinder("Washer glass door", washer + new Vector3(.356f, -.03f, 0),
                new Vector3(.26f, .038f, .26f), window);
            door.transform.rotation = Quaternion.Euler(0, 0, 90);
            Box("Laundry hamper", new Vector3(-11.55f, .30f, 6.21f),
                new Vector3(.62f, .60f, .69f), oak, SolidLayer);
            Sign("LAUNDRY", new Vector3(-11.5f, 1.40f, 4.79f), .18f, Color.white);
            laundryStatus = Sign("Ready", new Vector3(-11.5f, 1.23f, 4.79f), .13f, Color.white);
            laundryStock = Sign("Bags 0   Soap 0", new Vector3(-11.50f, 1.08f, 4.79f), .115f, Color.white);
            storedVacuum = BuildVacuum("Stored vacuum", new Vector3(-11.69f, 0, 4.48f));
            storedVacuum.gameObject.SetActive(false);

            cleaningTools[(int)CleaningTask.Vacuum] = BuildVacuum("Vacuum in use", Vector3.zero);
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
                Vector3 position = selected == CleaningTask.Vacuum
                    ? worker.Position + worker.Orientation * new Vector3(.30f, 0, .44f)
                    : worker.CarryPosition;
                tool.SetPositionAndRotation(position, worker.Orientation);
            }
        }

        public void ShowHousekeeping(MotelService service)
        {
            for (int room = 0; room < RoomX.Length; room++)
            {
                for (int i = 0; i < MotelService.CleaningTaskCount; i++)
                {
                    CleaningTask task = (CleaningTask)i;
                    bool visible = service.TaskPending(room, task);
                    if (task == CleaningTask.MakeBeds)
                        visible &= !service.TaskPending(room, CleaningTask.Strip);
                    roomMess[room, i].SetActive(visible);
                }
            }
            if (storedVacuum.gameObject.activeSelf != (service.Vacuums > 0))
                storedVacuum.gameObject.SetActive(service.Vacuums > 0);
            string stock = $"Bags {service.TrashBags}   Soap {service.Detergent}";
            if (laundryStock.text != stock)
                laundryStock.text = stock;
            string status = service.LaundryRunning ? "Washing" : "Ready";
            if (laundryStatus.text != status)
                laundryStatus.text = status;
        }
    }
}
