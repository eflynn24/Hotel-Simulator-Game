using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte ExpansionOfficeAction = 23;
        private int selectedExpansion = -1;
        private int conferenceRoomCount = 2;
        private int conferenceLead = 1;
        private int mealRoom;
        private string expansionFeedback;

        private void HandleExpansionAction(Vector3 data, Vector3 position)
        {
            if (!AtComputer(position) || data.x < 0 || data.x >= MotelService.Expansions.Length ||
                data.y < 0 || data.y > (int)ExpansionAction.Operate || data.z < 0 || data.z > 100 ||
                data.x != Mathf.Floor(data.x) || data.y != Mathf.Floor(data.y) || data.z != Mathf.Floor(data.z))
                return;
            var project = (Expansion)(int)data.x;
            var action = (ExpansionAction)(int)data.y;
            MotelService service = Service;
            bool succeeded = service.ManageExpansion(project, action, (int)data.z);
            expansionFeedback = succeeded ? "" : project == Expansion.Conference
                ? "Check that the same dates have enough available rooms." : "No change. Check funds, stock or demand.";
            Service = service;
        }

        private void RequestExpansion(Expansion project, ExpansionAction action, int option = 0)
        {
            Vector3 data = new Vector3((int)project, (int)action, option);
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(ExpansionOfficeAction, data);
            else
                HandleExpansionAction(data, caretaker.Position);
        }

        private void DrawExpansionOffice(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            float y = panel.y + 100 * scale;
            DrawDeskSummary(new Rect(x, y, width, 28 * scale), scale,
                $"Cash ${service.Cash}    ·    {service.RoomCapacity} rooms    ·    Amenity sales ${service.ExpansionRevenue}");
            if (selectedExpansion == -2)
            {
                DrawManagement(panel, x, width, scale);
                return;
            }
            if (selectedExpansion < 0)
            {
                float column = (width - 6 * scale) / 2;
                for (int index = 0; index < MotelService.Expansions.Length; index++)
                {
                    ExpansionOffer offer = MotelService.Expansions[index];
                    string status = service.HasExpansion((Expansion)index) ? "Manage" : $"${offer.Investment}";
                    float left = x + (index % 2) * (column + 6 * scale);
                    float top = y + (36 + index / 2 * 49) * scale;
                    if (DrawButton(new Rect(left, top, column, 43 * scale), $"{offer.Name}\n{status}"))
                    {
                        selectedExpansion = index;
                        expansionFeedback = null;
                    }
                }
                if (DrawButton(new Rect(x, panel.y + 388 * scale, width, 30 * scale), "Manage property"))
                    selectedExpansion = -2;
                return;
            }

            var project = (Expansion)selectedExpansion;
            ExpansionOffer selected = MotelService.Expansions[selectedExpansion];
            if (DrawButton(new Rect(x, y + 33 * scale, 76 * scale, 28 * scale), "Back"))
            {
                selectedExpansion = -1;
                return;
            }
            DrawFittedText(new Rect(x + 84 * scale, y + 33 * scale, width - 84 * scale, 29 * scale), selected.Name, detail);
            if (!service.HasExpansion(project))
            {
                if (project == Expansion.Annex && !service.OwnsAnnexLand)
                {
                    DrawFittedText(new Rect(x, y + 74 * scale, width, 58 * scale),
                        "Purchase the neighboring parcel before building the annex. Complete two stays to qualify.", label);
                    GUI.enabled = service.CompletedStays >= MotelService.ExpansionMinimumStays && service.Cash >= MotelService.AnnexLandCost;
                    if (DrawButton(new Rect(x, y + 139 * scale, width, 34 * scale), $"Purchase neighboring lot · ${MotelService.AnnexLandCost}"))
                        RequestManagement(ManagementAction.PurchaseLand);
                    GUI.enabled = true;
                    return;
                }
                string prerequisite = service.CompletedStays < MotelService.ExpansionMinimumStays
                    ? $"Complete {MotelService.ExpansionMinimumStays} stays to start investing ({service.CompletedStays} completed)."
                    : project == Expansion.UpperAnnex && !service.HasExpansion(Expansion.Annex)
                        ? "Build the guest annex first." : ExpansionDescription(project);
                DrawFittedText(new Rect(x, y + 74 * scale, width, 58 * scale), prerequisite, label);
                GUI.enabled = service.CanExpand(project);
                if (DrawButton(new Rect(x, y + 139 * scale, width, 34 * scale), $"Invest ${selected.Investment}"))
                    RequestExpansion(project, ExpansionAction.Build);
                GUI.enabled = true;
                return;
            }

            float topRow = y + 77 * scale;
            switch (project)
            {
                case Expansion.Vending:
                    ExpansionStatus(x, topRow, width, scale, $"Snacks {service.Snacks} · Drinks {service.Drinks}\nGuests buy from available stock.");
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 0, "Restock snacks ×6 · $12");
                    ExpansionButton(project, ExpansionAction.Alternate, x, topRow, width, scale, 1, "Restock drinks ×6 · $12");
                    break;
                case Expansion.Breakfast:
                    ExpansionStatus(x, topRow, width, scale,
                        $"Ingredients {service.BreakfastIngredients} · Prepared {service.BreakfastReady}\nBreakfast is served at 08:00.");
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 0,
                        $"Buy ingredients ×6 · ${MotelService.BreakfastStockCost}");
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 1, "Prepare up to 6 portions",
                        service.BreakfastIngredients > 0 && service.BreakfastReady < MotelService.RoomCount * 2);
                    break;
                case Expansion.RoomService:
                    DrawRoomMealOrders(service, project, x, topRow, width, scale);
                    break;
                case Expansion.Pool:
                    ExpansionStatus(x, topRow, width, scale,
                        $"Water quality {service.PoolWater}% · Condition {service.PoolCondition}%\n{(service.PoolWater >= 50 && service.PoolCondition >= 40 ? "Open to guests" : "Closed for maintenance")}");
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 0, "Treat pool water · $18", service.PoolWater < 100);
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 1, "Service pool equipment · $35", service.PoolCondition < 100);
                    break;
                case Expansion.Restaurant:
                    ExpansionStatus(x, topRow, width, scale, $"Meals {service.RestaurantFood} · Bar stock {service.BarStock}\nLunch at 12:00 · Dinner at 19:00");
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 0,
                        service.HasCook ? "Dismiss cook" : $"Hire cook · $38 + ${MotelService.RestaurantStaffWage}/day");
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 1,
                        service.HasServer ? "Dismiss server" : $"Hire server · $38 + ${MotelService.RestaurantStaffWage}/day", option: 1);
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 2, "Buy food ×8 · $22");
                    ExpansionButton(project, ExpansionAction.Alternate, x, topRow, width, scale, 3, "Restock bar ×8 · $16");
                    break;
                case Expansion.Fitness:
                    ExpansionStatus(x, topRow, width, scale, service.GymCondition == 0 ? "Exercise equipment required."
                        : $"Equipment condition {service.GymCondition}%\n{(service.GymCondition >= 25 ? "Open to guests" : "Closed for maintenance")}");
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 0, "Purchase exercise equipment · $75", service.GymCondition == 0);
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 1, "Maintain equipment · $28", service.GymCondition > 0 && service.GymCondition < 100);
                    break;
                case Expansion.Conference:
                    DrawConference(service, project, x, topRow, width, scale);
                    break;
                case Expansion.Spa:
                    ExpansionStatus(x, topRow, width, scale, $"Treatment kits {service.SpaStock} · Requests {service.SpaRequests}");
                    ExpansionButton(project, ExpansionAction.Buy, x, topRow, width, scale, 0, "Buy treatment kits ×4 · $32");
                    ExpansionButton(project, ExpansionAction.Operate, x, topRow, width, scale, 1, "Provide treatment · Earn $38",
                        service.SpaRequests > 0 && service.SpaStock > 0);
                    break;
                default:
                    ExpansionStatus(x, topRow, width, scale, project == Expansion.Annex
                        ? "Rooms 9 and 10 are open for bookings." : "Rooms 11 and 12 are open for bookings.");
                    break;
            }

            if (!string.IsNullOrEmpty(expansionFeedback))
                DrawFittedText(new Rect(x, panel.y + 387 * scale, width, 31 * scale), expansionFeedback, label);
        }

        private void DrawRoomMealOrders(MotelService service, Expansion project, float x, float y, float width, float scale)
        {
            int pending = 0;
            int next = -1;
            for (int room = 0; room < service.RoomCapacity; room++)
                if (service.RoomMealPending(room))
                {
                    pending++;
                    if (next < 0 || (room > mealRoom && (next <= mealRoom || room < next)))
                        next = room;
                }

            ExpansionStatus(x, y, width, scale, $"Meals in stock {service.RoomMealStock} · Orders {pending}\nGuests can order dinner or request it during a visit.");
            ExpansionButton(project, ExpansionAction.Buy, x, y, width, scale, 0, $"Buy meal stock ×4 · ${MotelService.RoomMealStockCost}");
            if (pending == 0)
                return;
            if (!service.RoomMealPending(mealRoom))
                mealRoom = next;
            if (DrawButton(new Rect(x, y + 87 * scale, width, 30 * scale), $"Room {mealRoom + 1} · Select next order"))
                mealRoom = next;
            ExpansionButton(project, ExpansionAction.Operate, x, y, width, scale, 2, $"Send meal to Room {mealRoom + 1} · Earn $18",
                service.RoomMealStock > 0, mealRoom);
        }

        private void DrawConference(MotelService service, Expansion project, float x, float y, float width, float scale)
        {
            if (service.ConferenceStart != 0)
            {
                string rooms = "";
                for (int room = 0; room < service.RoomCapacity; room++)
                    if (service.ConferenceRoom(room))
                        rooms += (rooms.Length == 0 ? "" : ", ") + (room + 1);
                ExpansionStatus(x, y, width, scale, service.ConferenceInProgress
                    ? $"Event in progress · Group rooms {rooms}\nEnds {MotelService.DisplayTime(service.ConferenceEnd)}"
                    : $"{MotelService.DisplayTime(service.ConferenceStart)} · Group rooms {rooms}");
                if (!service.ConferenceInProgress)
                    ExpansionButton(project, ExpansionAction.Alternate, x, y, width, scale, 0, "Cancel group and event");
                return;
            }
            int date = (service.ClockMinute / MotelService.MinutesPerDay + conferenceLead) * MotelService.MinutesPerDay + 10 * 60;
            ExpansionStatus(x, y, width, scale, $"Events hosted {service.EventsHosted} · {MotelService.DisplayTime(date)}");
            if (DrawButton(new Rect(x, y + 52 * scale, width, 30 * scale), $"Group rooms needed · {conferenceRoomCount}"))
                conferenceRoomCount = conferenceRoomCount == 4 ? 2 : conferenceRoomCount + 1;
            if (DrawButton(new Rect(x, y + 87 * scale, width, 30 * scale), $"Arrival in {conferenceLead} day(s)"))
                conferenceLead = conferenceLead % 7 + 1;
            ExpansionButton(project, ExpansionAction.Operate, x, y, width, scale, 2, "Reserve rooms and conference space",
                option: conferenceRoomCount + (conferenceLead - 1) * 16);
        }

        private void ExpansionStatus(float x, float y, float width, float scale, string text) =>
            DrawFittedText(new Rect(x, y, width, 49 * scale), text, label);

        private void ExpansionButton(Expansion project, ExpansionAction action, float x, float y, float width, float scale,
            int row, string caption, bool enabled = true, int option = 0)
        {
            GUI.enabled = enabled;
            if (DrawButton(new Rect(x, y + (52 + row * 35) * scale, width, 30 * scale), caption))
                RequestExpansion(project, action, option);
            GUI.enabled = true;
        }

        private static string ExpansionDescription(Expansion project) => project switch
        {
            Expansion.Annex => "Build a guest wing with two rooms and dedicated parking.",
            Expansion.UpperAnnex => "Add two rooms above the annex and connect them to the upper balcony.",
            Expansion.Breakfast => "Prepare breakfast portions for the morning service.",
            Expansion.RoomService => "Take guest meal orders and send meals to rooms.",
            Expansion.Pool => "Open a pool and maintain its water and equipment.",
            Expansion.Restaurant => "Hire a cook and server, stock the kitchen and bar, and open for meals.",
            Expansion.Fitness => "Equip a fitness room and keep the machines in working order.",
            Expansion.Conference => "Book business events and reserve overnight rooms together.",
            Expansion.Spa => "Stock treatment kits and fulfill premium service requests.",
            _ => "Sell snacks and drinks to guests who visit the machines."
        };
    }
}
