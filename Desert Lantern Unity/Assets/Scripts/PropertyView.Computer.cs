using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private bool computerOpen;

        private static bool AtComputer(Vector3 position) =>
            position.z > 5.63f && MotelWalker.Distance(position, MotelWorld.ComputerApproach) < 1.15f;

        private void SetComputerVisible(bool open)
        {
            computerOpen = open;
            if (open)
                walkRoute.Clear();
            SetActorPause(open);
        }

        private void TryPurchase(int offer, Vector3 position)
        {
            MotelService service = Service;
            if (AtComputer(position) && service.Buy(offer))
                Service = service;
        }

        private void Purchase(int offer)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(PurchaseAction, new Vector3(offer, 0, 0));
            else
                TryPurchase(offer, caretaker.Position);
        }

        private void DrawSupplyComputer(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 338 * scale, scale);
            float x = panel.x + 18 * scale;
            float width = panel.width - 36 * scale;
            GUI.Label(new Rect(x, panel.y + 9 * scale, width, 47 * scale), "Motel supplies", title);
            GUI.Label(new Rect(x, panel.y + 61 * scale, width, 31 * scale), $"Available cash  ${Service.Cash}", label);
            for (int row = 0; row < MotelService.Catalog.Length; row++)
            {
                SupplyOffer offer = MotelService.Catalog[row];
                float y = panel.y + (99 + 58 * row) * scale;
                GUI.Label(new Rect(x, y, width - 91 * scale, 26 * scale),
                    $"{offer.Name} ×{offer.Quantity}", detail);
                GUI.Label(new Rect(x, y + 24 * scale, width - 91 * scale, 25 * scale),
                    $"{Service.Stock(row)} in stock", label);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && Service.Cash >= offer.Price;
                if (DrawButton(new Rect(panel.xMax - 101 * scale, y + 5 * scale, 83 * scale, 34 * scale),
                    $"Buy  ${offer.Price}"))
                    Purchase(row);
                GUI.enabled = enabled;
            }

            if (DrawButton(new Rect(x, panel.y + 288 * scale, width, 35 * scale), "Close computer"))
                SetComputerVisible(false);
        }
    }
}
