using UnityEngine;

namespace DesertLantern
{
    // The test lobby is the playable operations area for the Budget Hotel.
    // Its twenty guest rooms use the existing hotel inventory and desk simulation.
    internal static class BudgetTestLobby
    {
        public static readonly Vector3 Spawn = new Vector3(0, 0, -3.0f);
        public static readonly Vector3 Computer = new Vector3(.65f, 1.40f, 2f);
        public static readonly Vector3 DeskApproach = new Vector3(.65f, 0, 1.0f);
        public static readonly Vector3 HousekeepingStation = new Vector3(-3.1f, 0, .35f);
        public static readonly Vector3 DeliveryStation = new Vector3(3.15f, 0, .10f);

        public static void Build(Transform parent)
        {
            Color walls = new Color32(173, 179, 174, 255);
            Color timber = new Color32(117, 77, 52, 255);
            Color blue = new Color32(41, 76, 112, 255);
            Color cream = new Color32(232, 226, 208, 255);
            Color metal = new Color32(74, 82, 90, 255);
            Box(parent, "Lobby floor", new Vector3(0, -.10f, -.1f), new Vector3(8.6f, .20f, 8),
                new Color32(116, 125, 130, 255), MotelWorld.GroundLayer);
            Box(parent, "Reception wall", new Vector3(0, 1.35f, 3.75f), new Vector3(8.6f, 2.7f, .2f), walls, MotelWorld.SolidLayer);
            Box(parent, "Front threshold", new Vector3(0, .10f, -4.03f), new Vector3(8.6f, .20f, .16f), timber);
            Box(parent, "Front boundary", new Vector3(0, 1.35f, -4.03f), new Vector3(8.6f, 2.7f, .16f),
                walls, MotelWorld.SolidLayer).GetComponent<Renderer>().enabled = false;
            for (int side = -1; side <= 1; side += 2)
            {
                Box(parent, "Side wall", new Vector3(side * 4.24f, 1.35f, -.1f), new Vector3(.18f, 2.7f, 8),
                    walls, MotelWorld.SolidLayer);
                Box(parent, "Lobby bench", new Vector3(side * 2.65f, .25f, -2.35f), new Vector3(1.45f, .50f, .53f), blue, MotelWorld.SolidLayer);
                Box(parent, "Bench back", new Vector3(side * 2.65f, .62f, -2.61f), new Vector3(1.45f, .70f, .12f), blue);
            }
            Box(parent, "Desk base", new Vector3(0, .50f, 2.1f), new Vector3(3.3f, 1.0f, .74f), timber, MotelWorld.SolidLayer);
            Box(parent, "Desk top", new Vector3(0, 1.05f, 2.1f), new Vector3(3.46f, .14f, .88f), cream);
            Box(parent, "Computer", Computer, new Vector3(.57f, .37f, .16f), metal);
            Box(parent, "Monitor screen", Computer + Vector3.back * .085f, new Vector3(.49f, .29f, .015f), blue);
            Box(parent, "Desk panel", new Vector3(0, .51f, 1.72f), new Vector3(2.9f, .55f, .022f), blue);
            Box(parent, "Housekeeping cart", HousekeepingStation + new Vector3(-.51f, .34f, .15f),
                new Vector3(.62f, .68f, .64f), metal, MotelWorld.SolidLayer);
            Box(parent, "Clean linen stack", HousekeepingStation + new Vector3(-.51f, .78f, .15f),
                new Vector3(.52f, .20f, .44f), cream);
            for (int box = 0; box < 3; box++)
                Box(parent, "Inventory carton", DeliveryStation + new Vector3(.55f, .22f + box * .42f, 0),
                    new Vector3(.54f, .42f, .52f), timber);
            Box(parent, "Lobby runner", new Vector3(0, .012f, -.61f), new Vector3(1.65f, .02f, 4.72f), blue);
            Box(parent, "Hotel sign board", new Vector3(0, 2.15f, 3.61f), new Vector3(3.50f, .56f, .09f), blue);
            var sign = new GameObject("Budget Hotel sign");
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = new Vector3(0, 2.15f, 3.53f);
            TextMesh text = sign.AddComponent<TextMesh>();
            text.text = "BUDGET HOTEL";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = .084f;
            text.fontSize = 53;
            text.color = cream;

            var source = new GameObject("Lobby lighting");
            source.transform.SetParent(parent, false);
            source.transform.rotation = Quaternion.Euler(54, -28, 0);
            var lamp = source.AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.color = new Color32(255, 235, 206, 255);
            lamp.intensity = 1.35f;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Color color, int layer = 0)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Renderer renderer = box.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
            box.GetComponent<BoxCollider>().enabled = layer != 0;
            return box;
        }
    }
}
