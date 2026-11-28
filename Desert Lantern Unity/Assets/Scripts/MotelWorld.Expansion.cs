using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        public const float EastBoundary = 41.5f;
        private readonly GameObject[,] furnitureProps = new GameObject[MotelService.RoomCount + 1, MotelService.FurnitureCatalog.Length];
        private readonly Transform[] expansionRoots = new Transform[MotelService.Expansions.Length];

        private void BuildRoomWing(bool overview)
        {
            for (int room = 0; room < RoomX.Length; room++)
            {
                roomBuildOffset = Vector3.up * FloorHeight(room);
                if (room < MotelService.BaseRoomCount)
                {
                    BuildRoom(room, overview);
                    continue;
                }
                int first = root.childCount;
                BuildRoom(room, overview);
                AttachExpansionObjects(first, room < 10 ? Expansion.Annex : Expansion.UpperAnnex);
            }
            roomBuildOffset = Vector3.zero;
        }

        private void AttachExpansionObjects(int first, Expansion project)
        {
            int index = (int)project;
            int count = root.childCount - first;
            if (expansionRoots[index] == null)
            {
                expansionRoots[index] = Node(MotelService.Expansions[index].Name, Vector3.zero);
                expansionRoots[index].gameObject.SetActive(false);
            }
            for (int item = 0; item < count; item++)
                root.GetChild(first).SetParent(expansionRoots[index], true);
        }

        private void BuildExpansionGrounds()
        {
            Box("East property drive", new Vector3(29.56f, -.14f, -2.4f),
                new Vector3(25.15f, .28f, 25), asphalt, GroundLayer);

            int first = root.childCount;
            Box("Annex ground walkway", new Vector3(21.8f, -.035f, 5.66f), new Vector3(11.05f, .09f, 2.22f), tile, GroundLayer);
            Sign("GUEST ANNEX", new Vector3(21.9f, 2.57f, 6.39f), .20f, Color.white);
            AttachExpansionObjects(first, Expansion.Annex);

            first = root.childCount;
            Box("Annex upper walkway and connector", new Vector3(21.47f, 2.97f, 5.79f),
                new Vector3(11.68f, .22f, 2.05f), trim, GroundLayer);
            Box("Annex balcony rail", new Vector3(21.48f, 4.12f, 4.72f), new Vector3(11.64f, .13f, .16f), trim, SolidLayer);
            for (int post = 0; post < 22; post++)
                Box("Annex balcony baluster", new Vector3(15.83f + post * .54f, 3.68f, 4.72f),
                    new Vector3(.07f, .82f, .10f), trim);
            Box("Annex end rail", new Vector3(27.27f, 4.10f, 5.75f), new Vector3(.11f, .16f, 2.13f), trim, SolidLayer);
            AttachExpansionObjects(first, Expansion.UpperAnnex);

            Vector3 machine = new Vector3(-6.7f, 0, 4.15f);
            first = root.childCount;
            Box("Vending machine shell", machine + Vector3.up * .86f, new Vector3(.81f, 1.72f, .51f), teal, SolidLayer);
            Box("Vending display", machine + new Vector3(0, 1.05f, -.262f), new Vector3(.65f, .81f, .025f), window);
            for (int item = 0; item < 12; item++)
                Box("Vending product", machine + new Vector3(-.25f + item % 4 * .163f, .79f + item / 4 * .22f, -.28f),
                    new Vector3(.095f, .13f, .026f), item % 2 == 0 ? cream : linen);
            Sign("COLD DRINKS", machine + new Vector3(0, 1.58f, -.28f), .094f, Color.white);
            AttachExpansionObjects(first, Expansion.Vending);

            first = root.childCount;
            Vector3 pool = new Vector3(33f, 0, 6f);
            Box("Pool deck", pool, new Vector3(9.1f, .04f, 5.7f), tile, GroundLayer);
            Material water = Surface(new Color32(49, 143, 156, 255), .12f, 8);
            Box("Swimming pool water", pool + Vector3.up * .044f, new Vector3(6.7f, .04f, 3.65f), water);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Pool coping", pool + new Vector3(0, .105f, side * 1.93f), new Vector3(7.10f, .14f, .21f), linen);
                Box("Pool end coping", pool + new Vector3(side * 3.44f, .105f, 0), new Vector3(.19f, .14f, 3.90f), linen);
            }
            Box("Pool rules board", pool + new Vector3(3.85f, 1.13f, -2.28f), new Vector3(.86f, .57f, .12f), teal);
            Sign("POOL", pool + new Vector3(3.85f, 1.16f, -2.352f), .17f, Color.white);
            AttachExpansionObjects(first, Expansion.Pool);

            BuildAmenityFront(Expansion.Breakfast, new Vector3(29.0f, 0, -.75f), "BREAKFAST ROOM");
            BuildAmenityFront(Expansion.RoomService, new Vector3(34.0f, 0, -.75f), "ROOM SERVICE");
            BuildAmenityFront(Expansion.Restaurant, new Vector3(39.0f, 0, -.75f), "RESTAURANT & BAR");
            BuildAmenityFront(Expansion.Fitness, new Vector3(29.0f, 0, -8.25f), "FITNESS CENTER");
            BuildAmenityFront(Expansion.Conference, new Vector3(34.0f, 0, -8.25f), "CONFERENCE CENTER");
            BuildAmenityFront(Expansion.Spa, new Vector3(39.0f, 0, -8.25f), "DESERT LANTERN SPA");
        }

        private void BuildAmenityFront(Expansion project, Vector3 point, string title)
        {
            int first = root.childCount;
            Box("Amenity foundation", point + Vector3.down * .03f, new Vector3(4.48f, .10f, 3.55f), tile, GroundLayer);
            Box("Amenity building", point + new Vector3(0, 1.32f, .28f), new Vector3(4.25f, 2.63f, 3.0f), plaster, SolidLayer);
            Box("Amenity canopy", point + new Vector3(0, 2.38f, -1.43f), new Vector3(4.47f, .19f, .91f), teal);
            Box("Amenity entry", point + new Vector3(0, .94f, -1.25f), new Vector3(.82f, 1.88f, .07f), oak);
            for (int side = -1; side <= 1; side += 2)
                Box("Amenity window", point + new Vector3(side * 1.20f, 1.28f, -1.25f), new Vector3(1.03f, .91f, .07f), window);
            Box("Amenity sign board", point + new Vector3(0, 2.11f, -1.30f), new Vector3(2.42f, .32f, .08f), oak);
            Sign(title, point + new Vector3(0, 2.12f, -1.35f), .125f, Color.white);
            AttachExpansionObjects(first, project);
        }

        private void BuildFurniture(int location)
        {
            bool reception = location == MotelService.ReceptionFurniture;
            float x = reception ? -9.65f : RoomX[location];
            if (reception)
            {
                Transform bench = Node("Upgraded lobby bench", new Vector3(-8.02f, 0, 5.85f));
                Piece(bench, "Bench base", new Vector3(0, .25f, 0), new Vector3(.62f, .48f, .91f), oak);
                Piece(bench, "Bench cushion", new Vector3(0, .53f, 0), new Vector3(.64f, .12f, .94f), teal);
                furnitureProps[location, 0] = bench.gameObject;

                Transform art = Node("Reception artwork", new Vector3(-10.57f, 1.77f, 8.53f + ReceptionExtension));
                Piece(art, "Picture frame", Vector3.zero, new Vector3(.46f, .34f, .064f), oak);
                Piece(art, "Desert landscape", new Vector3(0, 0, -.039f), new Vector3(.39f, .26f, .021f), cream);
                Piece(art, "Landscape horizon", new Vector3(0, -.05f, -.055f), new Vector3(.35f, .08f, .013f), teal);
                furnitureProps[location, 1] = art.gameObject;

                Transform display = Node("Reception display cabinet", new Vector3(-11.39f, 0, 7.91f));
                Piece(display, "Display shell", new Vector3(0, .50f, 0), new Vector3(.50f, 1.0f, .59f), oak);
                Piece(display, "Display glass", new Vector3(0, .68f, -.31f), new Vector3(.41f, .52f, .041f), window);
                furnitureProps[location, 2] = display.gameObject;
            }
            else
            {
                Transform bedding = Node("Premium bedding", new Vector3(x, 0, 0));
                foreach (float bed in new[] { 8.20f, 11.34f })
                    Piece(bedding, "Premium coverlet", new Vector3(-1.29f, .746f, bed),
                        new Vector3(1.72f, .054f, .87f), teal);
                furnitureProps[location, 0] = bedding.gameObject;

                Transform readingLamp = Node("Premium reading lamp", new Vector3(x - 1.88f, 0, 9.75f));
                Piece(readingLamp, "Reading lamp stand", new Vector3(0, .95f, 0), new Vector3(.075f, .39f, .075f), iron);
                Piece(readingLamp, "Reading lamp shade", new Vector3(0, 1.17f, 0), new Vector3(.24f, .18f, .24f), lamp);
                furnitureProps[location, 1] = readingLamp.gameObject;

                Transform chair = Node("Premium guest chair", new Vector3(x + 1.84f, 0, 8.58f));
                Piece(chair, "Upholstered seat", new Vector3(0, .46f, 0), new Vector3(.43f, .14f, .43f), teal);
                Piece(chair, "Upholstered back", new Vector3(0, .72f, .18f), new Vector3(.43f, .45f, .115f), teal);
                for (int foot = 0; foot < 4; foot++)
                    Piece(chair, "Chair leg", new Vector3(foot % 2 == 0 ? -.16f : .16f, .21f, foot < 2 ? -.16f : .16f),
                        new Vector3(.063f, .42f, .063f), oak);
                furnitureProps[location, 2] = chair.gameObject;
            }
            for (int item = 0; item < MotelService.FurnitureCatalog.Length; item++)
                furnitureProps[location, item].SetActive(false);
        }

        private void ShowFurniture(MotelService service)
        {
            for (int location = 0; location <= MotelService.RoomCount; location++)
                for (int item = 0; item < MotelService.FurnitureCatalog.Length; item++)
                {
                    GameObject prop = furnitureProps[location, item];
                    if (prop == null)
                        continue;
                    bool visible = service.HasFurniture(location, item) &&
                        (location == MotelService.ReceptionFurniture || item != 0 || !service.TaskPending(location, CleaningTask.MakeBeds));
                    if (prop.activeSelf != visible)
                        prop.SetActive(visible);
                }
        }

        public void ShowExpansion(MotelService service)
        {
            ShowFurniture(service);
            for (int project = 0; project < expansionRoots.Length; project++)
                if (expansionRoots[project] != null && expansionRoots[project].gameObject.activeSelf != service.HasExpansion((Expansion)project))
                    expansionRoots[project].gameObject.SetActive(service.HasExpansion((Expansion)project));
        }
    }
}
