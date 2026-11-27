using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte ManagementOfficeAction = 24;
        private int managementTab;
        private int furnitureRoom = MotelService.ReceptionFurniture;

        private void RequestManagement(ManagementAction action, int location = 0, int option = 0)
        {
            Vector3 data = new Vector3((int)action, location, option);
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(ManagementOfficeAction, data);
            else
                HandleManagement(data, caretaker.Position);
        }

        private void HandleManagement(Vector3 data, Vector3 point)
        {
            if (!AtComputer(point) || !(data.x >= 0 && data.x <= (int)ManagementAction.PurchaseLand &&
                data.y >= 0 && data.y <= MotelService.ReceptionFurniture && data.z >= 0 && data.z < MotelService.FurnitureCatalog.Length) ||
                data.x != Mathf.Floor(data.x) || data.y != Mathf.Floor(data.y) || data.z != Mathf.Floor(data.z))
                return;
            MotelService service = Service;
            service.ManageProperty((ManagementAction)(int)data.x, (int)data.y, (int)data.z);
            Service = service;
        }

        private void DrawManagement(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            float y = panel.y + 137 * scale;
            float columns = width / 3;
            string[] pages = { "Marketing", "Furnishings", "Incidents" };
            for (int tab = 0; tab < pages.Length; tab++)
                if (DrawButton(new Rect(x + columns * tab, y, columns - 3 * scale, 29 * scale), pages[tab]))
                    managementTab = tab;
            y += 42 * scale;
            if (managementTab == 0)
            {
                GUI.Label(new Rect(x, y, width, 29 * scale), $"Reputation {service.Reputation}% · Guest demand {service.Demand}%", detail);
                GUI.Label(new Rect(x, y + 38 * scale, width - 104 * scale, 31 * scale), $"Base nightly rate · ${service.ListedRate}", label);
                if (DrawButton(new Rect(x + width - 100 * scale, y + 38 * scale, 47 * scale, 31 * scale), "−$5"))
                    RequestManagement(ManagementAction.AdjustRate);
                if (DrawButton(new Rect(x + width - 47 * scale, y + 38 * scale, 47 * scale, 31 * scale), "+$5"))
                    RequestManagement(ManagementAction.AdjustRate, option: 1);
                if (DrawButton(new Rect(x, y + 79 * scale, width, 32 * scale),
                    service.Promotion ? "End the $10 nightly promotion" : "Start a $10 nightly promotion"))
                    RequestManagement(ManagementAction.Promotion);
                GUI.enabled = service.AdvertisingUntil <= service.ClockMinute && service.Cash >= MotelService.MarketingCost;
                if (DrawButton(new Rect(x, y + 117 * scale, width, 32 * scale), service.AdvertisingUntil > service.ClockMinute
                    ? $"Advertising runs through {MotelService.DisplayTime(service.AdvertisingUntil)}" : "Run advertising for 3 days · $40"))
                    RequestManagement(ManagementAction.Advertise);
                GUI.enabled = true;
            }
            else if (managementTab == 1)
            {
                bool reception = furnitureRoom == MotelService.ReceptionFurniture;
                if (DrawButton(new Rect(x, y, width, 32 * scale), reception ? "Reception · Select room"
                    : $"Room {furnitureRoom + 1} · Select next"))
                    furnitureRoom = reception ? 0 : furnitureRoom + 1 < service.RoomCapacity
                        ? furnitureRoom + 1 : MotelService.ReceptionFurniture;
                int quality = reception ? service.ReceptionQuality : service.RoomQuality(furnitureRoom);
                GUI.Label(new Rect(x, y + 34 * scale, width, 30 * scale), reception ? $"Reception quality {quality}%"
                    : $"Room quality {quality}% · Nightly rate ${service.NightlyRate(furnitureRoom)}", label);
                bool canChange = reception || (service.ActiveRooms & (1 << furnitureRoom)) == 0;
                for (int item = 0; item < MotelService.FurnitureCatalog.Length; item++)
                {
                    FurnitureOffer offer = MotelService.FurnitureCatalog[item];
                    bool placed = service.HasFurniture(furnitureRoom, item);
                    string name = reception ? offer.ReceptionName : offer.RoomName;
                    GUI.enabled = canChange && (placed || offer.Price <= service.Cash);
                    if (DrawButton(new Rect(x, y + (71 + item * 37) * scale, width, 32 * scale), placed
                        ? $"Remove {name.ToLowerInvariant()} · +${offer.Price / 4}" : $"Place {name.ToLowerInvariant()} · ${offer.Price}"))
                        RequestManagement(placed ? ManagementAction.RemoveFurniture : ManagementAction.PlaceFurniture, furnitureRoom, item);
                    GUI.enabled = true;
                }
                if (!canChange)
                    GUI.Label(new Rect(x, y + 185 * scale, width, 28 * scale), "Furnishings can be changed after checkout.", label);
            }
            else
            {
                string notice = service.Incident switch
                {
                    PropertyIncident.Pests => $"Pest report in Room {service.IncidentRoom + 1}. Check-ins are suspended there.",
                    PropertyIncident.SuspiciousActivity => "Suspicious activity reported near the parking lot.",
                    PropertyIncident.UtilityEmergency => "Utility disruption. The pool is temporarily closed.",
                    _ => "No incidents pending."
                };
                GUI.Label(new Rect(x, y, width, 66 * scale), notice, detail);
                if (service.Incident != PropertyIncident.None)
                {
                    string response = service.Incident == PropertyIncident.Pests ? "Dispatch pest control · $22"
                        : service.Incident == PropertyIncident.UtilityEmergency ? "Call emergency utility repair · $38" : "Review parking lot incident";
                    GUI.Label(new Rect(x, y + 66 * scale, width, 30 * scale),
                        $"Response due {MotelService.DisplayTime(service.IncidentDue)}", label);
                    if (DrawButton(new Rect(x, y + 108 * scale, width, 32 * scale), response))
                        RequestManagement(ManagementAction.Investigate);
                }
            }

            if (DrawButton(new Rect(x, panel.y + 383 * scale, width, 31 * scale), "Back to expansion"))
                selectedExpansion = -1;
        }
    }
}
