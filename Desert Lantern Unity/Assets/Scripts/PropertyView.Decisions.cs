using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte DecisionAction = 25;
        private bool viewingDecisions;
        private bool viewingMastery;

        private void HandleDecision(Vector3 data, Vector3 position)
        {
            if (!AtComputer(position) || data.x < 0 || data.x > 2 || data.x != Mathf.Floor(data.x) ||
                data.y <= 0 || data.y >= int.MaxValue || data.y != Mathf.Floor(data.y))
                return;
            MotelService service = Service;
            if (service.ResolveDecision((int)data.y, (int)data.x))
                Service = service;
        }

        private void DrawDecisions(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            if (service.PendingDecision == ManagementDecision.None)
            {
                GUI.Label(new Rect(x, panel.y + 150 * scale, width, 50 * scale), "No decisions waiting.", label);
                return;
            }

            bool vip = service.PendingDecision == ManagementDecision.UnexpectedArrival;
            bool supplier = service.PendingDecision == ManagementDecision.SupplierQuote;
            string heading = vip ? "Unexpected arrival" : supplier ? "Supplier quote" : "Maintenance quote";
            string summary = vip
                ? $"A walk-in offers $250 for Room {service.DecisionRoom + 1}, held for a ${service.DecisionQuote}/night booking. Displacement costs $35 and 9 reputation."
                : supplier ? $"Only {service.Towels} towels in stock. Two suppliers are offering a four-towel pack."
                : $"Room {service.DecisionRoom + 1} · {FixtureName(service.DecisionFixture)} · {service.Condition(service.DecisionRoom, service.DecisionFixture)}% condition.";
            GUI.Label(new Rect(x, panel.y + 138 * scale, width, 31 * scale), heading, detail);
            GUI.Label(new Rect(x, panel.y + 172 * scale, width, 50 * scale), summary, label);
            GUI.Label(new Rect(x, panel.y + 223 * scale, width, 23 * scale),
                $"Respond by {MotelService.DisplayTime(service.DecisionDeadline)}", label);
            int upgrade = vip ? service.UpgradedAlternative() : -1;
            string[] options = vip ? new[]
            {
                "Honor the existing reservation",
                "Book the $250 guest and bump the booking",
                upgrade < 0 ? "Upgrade unavailable" : $"Offer Room {upgrade + 1} · $175 · Keep booking"
            } : supplier ? new[]
            {
                "Standard · $18 · Delivery in 72m",
                "Economy · $11 · Delivery in 3h",
                "Keep existing stock"
            } : new[]
            {
                "Repair · $18 + part · +65 condition",
                $"Replace · ${service.DecisionQuote} · Restore to 100%",
                "Defer service · Condition −12%"
            };
            for (int choice = 0; choice < options.Length; choice++)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled &= service.CanResolveDecision(choice);
                if (DrawButton(new Rect(x, panel.y + (253 + choice * 43) * scale, width, 36 * scale), options[choice]))
                {
                    var data = new Vector3(choice, service.DecisionDeadline, 0);
                    if (lan != null && !lan.IsHost)
                        lan.State?.ActionRpc(DecisionAction, data);
                    else
                        HandleDecision(data, caretaker.Position);
                }
                GUI.enabled = wasEnabled;
            }
        }

        private void DrawMastery(Rect area, float scale)
        {
            int slot = countyProperty;
            PropertyMastery mastery = Campaign.Business(slot).mastery;
            var improvements = Campaign.MasteryImprovements(slot);
            float stars = Campaign.MasteryStars(slot);
            int staff = Campaign.StaffCount(slot);
            int roles = CountyPortfolio.StaffRoles.Length;
            GUI.Label(new Rect(area.x, area.y, area.width, 27 * scale),
                $"Property mastery · Optional ${CountyPortfolio.MasteryReward} company reward", detail);
            DrawMasteryRow(area, scale, 0, $"Guest rating · {stars:0.0} / 4 stars", stars / 4);
            DrawMasteryRow(area, scale, 1, $"80% occupancy · {mastery.occupancyDays}/7 days · Last day {mastery.lastOccupancy}%", mastery.occupancyDays / 7f);
            DrawMasteryRow(area, scale, 2, $"Cumulative profit · ${mastery.profit:N0} / ${CountyPortfolio.ProfitTarget:N0}", mastery.profit / (float)CountyPortfolio.ProfitTarget);
            DrawMasteryRow(area, scale, 3, $"{improvements.Name} · {improvements.Current}/{improvements.Target}", improvements.Current / (float)improvements.Target);
            DrawMasteryRow(area, scale, 4, $"Full employee coverage · {staff}/{roles}", staff / (float)roles);
            DrawMasteryRow(area, scale, 5, $"Independent operation while away · {mastery.independentDays}/3 days", mastery.independentDays / 3f);
            bool before = GUI.enabled;
            GUI.enabled &= Campaign.Mastered(slot) && !mastery.rewardClaimed;
            if (DrawButton(new Rect(area.x, area.y + 258 * scale, area.width, 32 * scale),
                mastery.rewardClaimed ? "Mastery reward claimed" : "Claim mastery reward"))
                Campaign.ClaimMastery(slot);
            GUI.enabled = before;
        }

        private void DrawMasteryRow(Rect area, float scale, int row, string caption, float progress)
        {
            float y = area.y + (32 + row * 36) * scale;
            GUI.Label(new Rect(area.x, y, area.width, 23 * scale), caption, countyBodyStyle ?? label);
            Fill(new Rect(area.x, y + 24 * scale, area.width, 5 * scale), CountyGray);
            Fill(new Rect(area.x, y + 24 * scale, area.width * Mathf.Clamp01(progress), 5 * scale), CountyGold);
        }
    }
}
