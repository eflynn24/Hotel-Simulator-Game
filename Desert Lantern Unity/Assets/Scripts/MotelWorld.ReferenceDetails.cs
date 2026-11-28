using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class MotelWorld
    {
        private void BuildForecourtDetails()
        {
            Material crack = Surface(new Color32(46, 39, 35, 255), .66f, 36);
            var random = new System.Random(615);
            for (int seam = 0; seam < 11; seam++)
            {
                Vector3 start = new Vector3(-15.7f + seam * 2.66f, .012f, -.3f - (float)random.NextDouble() * 8f);
                for (int section = 0; section < 5; section++)
                {
                    float length = .12f + (float)random.NextDouble() * .72f;
                    Vector3 end = start + new Vector3((float)random.NextDouble() - .33f, 0, length);
                    var mark = Box("Asphalt hairline crack", (start + end) * .5f,
                        new Vector3(.012f + section % 3 * .007f, .002f, Vector3.Distance(start, end)), crack);
                    mark.transform.rotation = Quaternion.LookRotation(end - start);
                    start = end;
                }
            }

            Material enamel = Surface(new Color32(71, 87, 97, 255), .32f, 21, .27f);
            Vector3 ice = new Vector3(-16.04f, .72f, 3.81f);
            Box("Ice machine cabinet", ice, new Vector3(.72f, 1.43f, .55f), enamel, SolidLayer);
            Box("Ice machine face", ice + new Vector3(0, .06f, -.288f), new Vector3(.59f, 1.18f, .032f), shadowGlass);
            Box("Ice access panel", ice + new Vector3(0, -.18f, -.313f), new Vector3(.41f, .38f, .026f), enamel);
            Box("Ice machine handle", ice + new Vector3(0, -.03f, -.338f), new Vector3(.16f, .034f, .052f), trim);
            Sign("ICE", ice + new Vector3(0, .43f, -.318f), .23f, new Color32(225, 238, 239, 255));

            Vector3 bench = new Vector3(-11.05f, 0, 3.60f);
            for (int slat = 0; slat < 3; slat++)
            {
                Box("Bench seat slat", bench + new Vector3(0, .45f, (slat - 1) * .105f), new Vector3(1.03f, .075f, .09f), oak);
                Box("Bench back slat", bench + new Vector3(0, .61f + slat * .105f, .16f), new Vector3(1.03f, .09f, .055f), oak);
            }
            for (int side = -1; side <= 1; side += 2)
                Box("Bench steel end", bench + new Vector3(side * .43f, .23f, 0), new Vector3(.07f, .45f, .32f), railPaint);
        }

        private void BuildUtilityFixtures()
        {
            var badge = new Color32(242, 201, 130, 255);
            Box("Housekeeping checklist backing", new Vector3(-12.222f, 1.68f, 4.80f),
                new Vector3(.045f, .62f, .44f), oak);
            var checklist = Sign("HOUSEKEEPING\n•  Collect used linens\n•  Clean the room\n•  Restock supplies",
                new Vector3(-12.252f, 1.68f, 4.80f), .105f, badge);
            checklist.transform.rotation = Quaternion.Euler(0, 90, 0);

            for (int tool = 0; tool < 3; tool++)
            {
                float z = 7.47f + tool * .16f;
                GameObject handle = Box("Stored cleaning tool", new Vector3(-12.34f, .69f, z),
                    new Vector3(.027f, 1.31f, .029f), tool == 1 ? teal : oak);
                handle.transform.rotation = Quaternion.Euler(-8 + tool * 5, 0, -10);
                if (tool == 0)
                    Box("Broom bristles", new Vector3(-12.44f, .083f, z), new Vector3(.15f, .14f, .24f), siding);
                else
                    Box("Mop head", new Vector3(-12.44f, .047f, z), new Vector3(.19f, .073f, .17f), linen);
            }

            for (int slat = 0; slat < 7; slat++)
                Box("Laundry wall vent louver", new Vector3(-16.792f, 1.62f + slat * .07f, 7.34f),
                    new Vector3(.04f, .058f, .72f), slat % 2 == 0 ? teal : iron);
            for (int shelf = 0; shelf < 2; shelf++)
            {
                float y = 1.59f + shelf * .40f;
                Box("Laundry wall shelf", new Vector3(-16.56f, y, 5.63f), new Vector3(.43f, .065f, .98f), oak);
                for (int product = 0; product < 3; product++)
                {
                    Vector3 item = new Vector3(-16.54f, y + .135f, 5.32f + product * .27f);
                    Box("Laundry wash bottle", item, new Vector3(.15f, .20f, .13f), product == 1 ? linen : teal);
                    Cylinder("Bottle screw cap", item + Vector3.up * .11f, new Vector3(.063f, .029f, .063f), trim);
                }
            }
        }
    }
}
