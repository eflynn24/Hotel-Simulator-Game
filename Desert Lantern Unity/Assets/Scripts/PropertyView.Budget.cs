using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private PropertyId foregroundProperty;
        private bool AtBudgetHotel => foregroundProperty == PropertyId.BudgetHotel;
        private bool budgetComputer;
        private string budgetFeedback;

        private void StartBudgetLobby()
        {
            BudgetTestLobby.Build(transform);
            view = new GameObject("Budget Hotel camera").AddComponent<Camera>();
            view.gameObject.AddComponent<AudioListener>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(55, 68, 78, 255);
            view.fieldOfView = 53;
            view.transform.position = new Vector3(0, 6.9f, -8.5f);
            view.transform.LookAt(new Vector3(0, .25f, .25f));
            caretaker = new MotelWalker(transform, "Caretaker", BudgetTestLobby.Spawn);
            countyProperty = CountyPortfolio.MapSlot(foregroundProperty);
        }

        private void UpdateBudgetLobby(float delta)
        {
            TickCounty(delta, foregroundProperty);
            if (Modal)
                return;
            UpdateWalk(delta);
            if (Input.GetKeyDown(KeyCode.P))
                SetReceptionPanel(ReceptionPanel.Portfolio);
            else if (Input.GetKeyDown(KeyCode.E))
            {
                if (NearBudgetStation(BudgetTestLobby.DeskApproach))
                    SetBudgetComputer(true);
                else if (NearBudgetStation(BudgetTestLobby.HousekeepingStation))
                    RunBudgetStation(1);
                else if (NearBudgetStation(BudgetTestLobby.DeliveryStation))
                    RunBudgetStation(0);
            }
        }

        private bool NearBudgetStation(Vector3 station) => MotelWalker.Distance(caretaker.Position, station) < 1.28f;

        private void SetBudgetComputer(bool open)
        {
            budgetComputer = open;
            walkRoute.Clear();
            SetActorPause(open);
        }

        private void RunBudgetStation(int action)
        {
            if (action == 0 && Campaign.HotelDeliveryReady(foregroundProperty))
                action = 6;
            bool worked = Campaign.RunHotelAction(foregroundProperty, action);
            budgetFeedback = worked ? action switch
            {
                0 => "Bulk stock ordered.",
                6 => "Delivery unpacked.",
                _ => "Available rooms prepared."
            } : action == 0 ? "Check operating cash and current orders." : "No available task at this station.";
            if (worked)
                Campaign.Save();
        }

        private void DrawBudgetLobby(Rect screen, float margin, float scale)
        {
            if (DrawButton(new Rect(margin, margin, 139 * scale, 42 * scale), "Back to county"))
            {
                Campaign.Save();
                PropertyTravelManager.ReturnToCounty();
                return;
            }
            if (DrawButton(new Rect(margin + 146 * scale, margin, 123 * scale, 42 * scale), "Management phone"))
                SetReceptionPanel(ReceptionPanel.Portfolio);

            if (DrawWorldButton(BudgetTestLobby.Computer + Vector3.up * .35f, "Reception computer", 150 * scale, scale))
            {
                if (NearBudgetStation(BudgetTestLobby.DeskApproach))
                    SetBudgetComputer(true);
                else
                    SetDestination(BudgetTestLobby.DeskApproach);
            }
            DrawBudgetStation(BudgetTestLobby.HousekeepingStation, "Housekeeping", 1, scale);
            DrawBudgetStation(BudgetTestLobby.DeliveryStation, "Bulk inventory", 0, scale);

            var state = Campaign.Business(foregroundProperty);
            Rect hud = new Rect(margin, screen.yMax - margin - 57 * scale, Mathf.Min(570 * scale, screen.width - 2 * margin), 57 * scale);
            Fill(hud, Ink);
            GUI.Label(new Rect(hud.x + 9 * scale, hud.y + 2 * scale, hud.width - 18 * scale, 26 * scale),
                $"Budget Hotel · {state.occupied}/20 occupied · {state.prepared} ready · ${state.cash:N0}", detail);
            GUI.Label(new Rect(hud.x + 9 * scale, hud.y + 28 * scale, hud.width - 18 * scale, 26 * scale),
                budgetFeedback ?? "Approach a station and press E, or select its marker.", label);
        }

        private void DrawBudgetStation(Vector3 station, string title, int action, float scale)
        {
            if (!DrawWorldButton(station + Vector3.up * 1.25f, title, 126 * scale, scale))
                return;
            if (NearBudgetStation(station))
                RunBudgetStation(action);
            else
                SetDestination(station);
        }

        private void DrawBudgetComputer(Rect screen, float scale)
        {
            PrepareCountyStyles(scale);
            Rect panel = DrawOverlay(screen, 400 * scale, scale);
            float x = panel.x + 15 * scale, width = panel.width - 30 * scale;
            GUI.Label(new Rect(x, panel.y + 9 * scale, width, 49 * scale), "Budget Hotel desk", title);
            DrawHotelOperations(new Rect(x, panel.y + 68 * scale, width, 269 * scale), scale);
            if (DrawButton(new Rect(x, panel.y + 351 * scale, width, 35 * scale), "Close computer"))
                SetBudgetComputer(false);
        }
    }
}
