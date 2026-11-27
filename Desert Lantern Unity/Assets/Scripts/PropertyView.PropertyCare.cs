using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PropertyCareAction = 22;
        private int inspectionRoom;
        private Fixture inspectedFixture;
        private int reportPeriod;
        private static readonly string[] AccountNames =
            { "Room revenue", "Guest service & tips", "Payroll", "Supplies", "Maintenance & insurance", "Electricity", "Water", "Internet", "Guest adjustments", "Financing cash flow" };

        private static string FixtureName(Fixture fixture) => fixture switch
        {
            Fixture.AirConditioner => "Air conditioner",
            Fixture.DoorLock => "Door lock",
            _ => fixture.ToString()
        };

        private static string FixtureProblem(Fixture fixture) => fixture switch
        {
            Fixture.Toilet => "Clogged toilet",
            Fixture.Sink => "Leaking supply line",
            Fixture.Shower => "No hot water",
            Fixture.AirConditioner => "Insufficient cooling",
            Fixture.Television => "Broken display",
            Fixture.Lights => "Burned-out bulb",
            Fixture.DoorLock => "Jammed lock",
            Fixture.Fridge => "Stopped cooling",
            Fixture.Washer => "Mechanical failure",
            _ => "Internet connection failure"
        };

        private static Vector3 InspectionPoint(int room, Fixture fixture)
        {
            if (fixture == Fixture.Washer)
                return MotelWorld.LaundryApproach;
            if (fixture == Fixture.Router)
                return MotelWorld.ComputerApproach;
            float x = MotelWorld.RoomX[room];
            float level = MotelWorld.FloorHeight(room);
            return fixture switch
            {
                Fixture.Toilet => new Vector3(x + .62f, level, 11.97f),
                Fixture.Sink => new Vector3(x + 1.24f, level, 11.99f),
                Fixture.Shower => new Vector3(x + 1.84f, level, 11.98f),
                Fixture.AirConditioner => new Vector3(x + 1.2f, level, 12.30f),
                Fixture.Television => new Vector3(x + 1.62f, level, 9.12f),
                Fixture.Lights => new Vector3(x, level, 9.90f),
                Fixture.Fridge => new Vector3(x + .07f, level, 12.15f),
                _ => MotelWorld.RoomApproach(room)
            };
        }

        private static bool AtFixture(int room, Fixture fixture, Vector3 position)
        {
            Vector3 target = InspectionPoint(room, fixture);
            return MotelWorld.NearFloor(position, target) && MotelWalker.Distance(position, target) < 1.12f;
        }

        private void RequestPropertyCare(int operation, int room = 0, Fixture fixture = Fixture.Toilet)
        {
            Vector3 data = new Vector3(operation, room, (int)fixture);
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(PropertyCareAction, data);
            else
                HandlePropertyCare(LocalWorker, data, caretaker.Position);
        }

        private void HandlePropertyCare(ulong worker, Vector3 data, Vector3 position)
        {
            if (!(data.x >= 0 && data.x <= 9 && data.y >= 0 && data.y < Guests.Length &&
                data.z >= 0 && data.z <= (int)Fixture.Router) ||
                data.x != Mathf.Floor(data.x) || data.y != Mathf.Floor(data.y) || data.z != Mathf.Floor(data.z))
                return;
            int operation = (int)data.x;
            int room = (int)data.y;
            Fixture fixture = (Fixture)(int)data.z;
            MotelService service = Service;
            if (operation <= 1 && AtFixture(room, fixture, position))
                service.Repair(room, fixture, operation == 1);
            else if (operation == 2 && AtFixture(room, Fixture.DoorLock, position))
                service.ToggleRoomClosure(room);
            else if (AtComputer(position) && (operation == 3 || operation == 4))
                service.BuyMaintenanceStock(operation == 4);
            else if (AtComputer(position) && (operation == 5 || operation == 6))
                service.ManageFinances(operation - 5);
            else if (operation == 7 && MotelWorld.NearFloor(position, MotelWorld.CartStation) &&
                MotelWalker.Distance(position, MotelWorld.CartStation) < .85f)
                service.ToggleHousekeepingCart(worker);
            else if (operation == 8 && NearActiveGuest(room, position))
                service.ResolveGuestIssue(room, (GuestIssue)(int)data.z);
            else if (operation == 9 && MotelWorld.InteriorRoom(position) == room &&
                MotelWalker.Distance(position, MotelWorld.BagDrop(room)) < 1.2f)
                service.CollectForgottenItem(room);
            Service = service;
        }

        private bool NearActiveGuest(int room, Vector3 position)
        {
            MotelWalker actor = visitorRoom == room ? serviceVisitor : backgroundGuests[room];
            return actor != null && MotelWorld.NearFloor(position, actor.Position) &&
                MotelWalker.Distance(position, actor.Position) < 1.4f;
        }

        private static string ActivityLabel(GuestActivity activity) => activity switch
        {
            GuestActivity.AtAttractions => "Visiting local attractions",
            GuestActivity.AtVending => "At vending",
            GuestActivity.HostingVisitor => "Hosting a visitor",
            GuestActivity.Walking => "Walking around the property",
            GuestActivity.Socializing => "Talking with other guests",
            GuestActivity.Showering => "Using the shower",
            GuestActivity.WatchingTelevision => "Watching television",
            GuestActivity.SeekingHelp => "Looking for assistance",
            _ => "On property"
        };

        private static string IssueLabel(GuestIssue issue) => issue switch
        {
            GuestIssue.ExtraNight => "Guest requests an additional night",
            GuestIssue.LostKey => "Guest lost their room key",
            GuestIssue.Equipment => "Guest is reporting broken equipment",
            GuestIssue.Charges => "Guest disputes a charge",
            GuestIssue.Noise => "Guest reports late-night noise",
            _ => "Guest is refusing to leave"
        };

        private static string IssueActionLabel(GuestIssue issue) => issue switch
        {
            GuestIssue.ExtraNight => "Approve another night if available",
            GuestIssue.LostKey => "Issue a replacement key",
            GuestIssue.Equipment => "Refund $15",
            GuestIssue.Charges => "Refund $12",
            GuestIssue.Noise => "Resolve noise complaint",
            _ => "Ask the guest to check out"
        };

        private void InspectFixture(int room, Fixture fixture)
        {
            inspectionRoom = room;
            inspectedFixture = fixture;
            SetReceptionPanel(ReceptionPanel.Maintenance);
        }

        private void DrawMaintenanceMarkers(float scale)
        {
            Vector3 point = caretaker.Position;
            if (InsideOffice(point))
            {
                if (DrawWorldButton(new Vector3(-11.37f, 1.62f, 5.25f), "Inspect washer", 128 * scale, scale))
                {
                    if (AtFixture(0, Fixture.Washer, point))
                        InspectFixture(0, Fixture.Washer);
                    else
                        SetDestination(InspectionPoint(0, Fixture.Washer));
                }
                return;
            }

            int room = MotelWorld.InteriorRoom(point);
            if (room == NoRoom)
                return;
            if (Service.ForgottenItem(room) && DrawWorldButton(MotelWorld.BagDrop(room) + Vector3.up * .82f,
                "Collect lost property", 152 * scale, scale))
            {
                if (MotelWalker.Distance(point, MotelWorld.BagDrop(room)) < 1.2f)
                    RequestPropertyCare(9, room);
                else
                    SetDestination(MotelWorld.BagDrop(room));
            }

            Fixture closest = Fixture.DoorLock;
            float distance = 1.15f;
            for (int index = 0; index < MotelService.RoomFixtureCount; index++)
            {
                var fixture = (Fixture)index;
                float candidate = MotelWalker.Distance(point, InspectionPoint(room, fixture));
                if (candidate < distance)
                {
                    distance = candidate;
                    closest = fixture;
                }
            }
            if (closest != Fixture.DoorLock && DrawWorldButton(InspectionPoint(room, closest) + Vector3.up * 1.27f,
                $"Inspect {FixtureName(closest).ToLowerInvariant()}", 150 * scale, scale))
                InspectFixture(room, closest);
        }

        private void DrawMaintenance(Rect area, float scale)
        {
            MotelService service = Service;
            int condition = service.Condition(inspectionRoom, inspectedFixture);
            bool broken = service.IsBroken(inspectionRoom, inspectedFixture);
            GUI.Label(new Rect(area.x, area.y, area.width, 32 * scale),
                $"{FixtureName(inspectedFixture)} · {condition}% condition", detail);
            GUI.Label(new Rect(area.x, area.y + 34 * scale, area.width, 30 * scale),
                broken ? FixtureProblem(inspectedFixture) : condition < 60 ? "Worn from use" : "Operational", label);
            GUI.enabled = condition < 100 && service.RepairParts > 0;
            if (DrawButton(new Rect(area.x, area.y + 76 * scale, area.width, 32 * scale),
                $"Repair with toolkit · {service.RepairParts} parts"))
                RequestPropertyCare(0, inspectionRoom, inspectedFixture);
            GUI.enabled = condition < 100;
            if (DrawButton(new Rect(area.x, area.y + 118 * scale, area.width, 32 * scale),
                $"Replace equipment · ${(service.Insured && broken ? 24 : 65)}"))
                RequestPropertyCare(1, inspectionRoom, inspectedFixture);
            GUI.enabled = true;
            if (inspectedFixture == Fixture.DoorLock && DrawButton(
                new Rect(area.x, area.y + 165 * scale, area.width, 32 * scale),
                service.RoomClosed(inspectionRoom) ? "Reopen room" : "Take room out of service"))
                RequestPropertyCare(2, inspectionRoom);
        }

        private void DrawFinancialReport(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            float y = panel.y + 105 * scale;
            string period = reportPeriod == 0 ? "Today" : reportPeriod == 1 ? "This week" : "This month";
            if (DrawButton(new Rect(x, y, 125 * scale, 28 * scale), period))
                reportPeriod = (reportPeriod + 1) % 3;
            GUI.Label(new Rect(x + 134 * scale, y, width - 134 * scale, 28 * scale),
                $"Cash ${service.Cash} · Loan ${service.LoanBalance}", detail);
            int profit = 0;
            for (int account = 0; account < AccountNames.Length; account++)
            {
                int value = service.ReportAmount(reportPeriod, (Account)account);
                if (account != (int)Account.Financing)
                    profit += value;
                GUI.Label(new Rect(x, y + (33 + account * 15) * scale, width, 23 * scale), AccountNames[account], label);
                GUI.Label(new Rect(x + width - 84 * scale, y + (33 + account * 15) * scale, 82 * scale, 23 * scale),
                    value.ToString("+$0;−$0;$0"), label);
            }
            GUI.Label(new Rect(x, y + 180 * scale, width, 27 * scale), $"Profit / loss   {profit:+$0;−$0;$0}", detail);
            float half = width / 2;
            if (DrawButton(new Rect(x, y + 211 * scale, half - 3 * scale, 29 * scale), "Parts ×3 · $18"))
                RequestPropertyCare(3);
            if (DrawButton(new Rect(x + half, y + 211 * scale, half, 29 * scale), "Disinfectant ×4 · $12"))
                RequestPropertyCare(4);
            if (DrawButton(new Rect(x, y + 245 * scale, half - 3 * scale, 29 * scale), "Borrow $180"))
                RequestPropertyCare(5);
            if (DrawButton(new Rect(x + half, y + 245 * scale, half, 29 * scale),
                service.Insured ? "Cancel insurance" : "Insure · $6/day"))
                RequestPropertyCare(6);
            if (DrawButton(new Rect(x, y + 279 * scale, lan == null ? half - 3 * scale : width, 29 * scale), "Inspect router"))
            {
                SetComputerVisible(false);
                InspectFixture(0, Fixture.Router);
            }
            if (lan == null && DrawButton(new Rect(x + half, y + 279 * scale, half, 29 * scale), "County statements"))
            {
                SetComputerVisible(false);
                SetReceptionPanel(ReceptionPanel.Portfolio);
            }
        }
    }
}
