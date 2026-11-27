using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private static CountyPortfolio campaign;
        private static CountyPortfolio Campaign => campaign ??= CountyPortfolio.Load();

        private static readonly Color CountyGold = new Color32(227, 190, 119, 255);
        private static readonly Color CountyInk = new Color32(20, 28, 30, 244);
        private static readonly Color CountyPaper = new Color32(245, 228, 194, 255);
        private static readonly Color CountyGreen = new Color32(36, 125, 72, 255);
        private static readonly Color CountyBlue = new Color32(40, 89, 198, 255);
        private static readonly Color CountyGray = new Color32(82, 83, 82, 255);
        private static readonly string[] CountyTabs = { "County Map", "My Properties", "Finances", "Staff", "Upgrades" };
        private static readonly string[] LuxuryDepartments = { "Restaurant and bar", "Concierge and villas", "Pool and recreation", "Spa", "Entertainment", "VIP lounge" };
        private static readonly Vector2[] CountyPins =
        {
            new Vector2(.217f, .639f), new Vector2(.313f, .418f), new Vector2(.533f, .277f),
            new Vector2(.283f, .137f), new Vector2(.692f, .139f), new Vector2(.741f, .527f)
        };

        private Texture2D countyBackdrop;
        private int countyTab;
        private int countySelection = -1;
        private int countyProperty = CountyPortfolio.MapSlot(PropertyId.RoadsideMotel);
        private int countySpeed = 1;
        private float countyClockFraction;
        private Vector2 countyScroll;
        private Vector2 inboxScroll;
        private GUIStyle countyNameStyle, countyStatusStyle, countyTitleStyle, countyPinStyle, countyBodyStyle;
        private static Texture2D countyPinDisc;

        private void TickCounty(float delta, PropertyId foreground = PropertyId.None)
        {
            if (LanSession.Active != null || showSettings || lanMenu || !Campaign.Started)
                return;
            countyClockFraction += delta * 6f * countySpeed;
            int elapsed = (int)countyClockFraction;
            if (elapsed == 0)
                return;
            countyClockFraction -= elapsed;
            Campaign.Advance(Campaign.ClockMinute + elapsed, foreground, default);
        }

        private void PrepareCountyStyles(float scale)
        {
            if (countyNameStyle == null)
            {
                countyNameStyle = new GUIStyle(detail) { fontStyle = FontStyle.Bold };
                countyNameStyle.normal.textColor = new Color32(30, 31, 25, 255);
                countyStatusStyle = new GUIStyle(label);
                countyStatusStyle.normal.textColor = new Color32(55, 51, 37, 255);
                countyBodyStyle = new GUIStyle(label) { wordWrap = true };
                countyTitleStyle = new GUIStyle(title) { fontStyle = FontStyle.BoldAndItalic, wordWrap = false };
                countyTitleStyle.normal.textColor = CountyGold;
                countyPinStyle = new GUIStyle(detail) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                countyPinStyle.normal.textColor = Color.white;
            }
            countyNameStyle.fontSize = Mathf.RoundToInt(14 * scale);
            countyStatusStyle.fontSize = Mathf.RoundToInt(11 * scale);
            countyBodyStyle.fontSize = Mathf.RoundToInt(12 * scale);
            countyTitleStyle.fontSize = Mathf.RoundToInt(17 * scale);
            countyPinStyle.fontSize = Mathf.RoundToInt(17 * scale);
        }

        private void DrawMap(Rect screen, float margin, float scale)
        {
            PrepareCountyStyles(scale);
            button.fontSize = Mathf.RoundToInt(13 * scale);
            if (countyBackdrop != null)
                GUI.DrawTexture(screen, countyBackdrop, ScaleMode.StretchToFill);
            Fill(screen, new Color(0, 0, 0, 1 - brightness));
            float toolbarHeight = 57 * scale;
            Fill(new Rect(0, 0, screen.width, toolbarHeight), CountyInk);
            Fill(new Rect(0, toolbarHeight - 2 * scale, screen.width, 2 * scale), CountyGold);
            GUI.Label(new Rect(13 * scale, 4 * scale, 134 * scale, 46 * scale), "Desert Lantern\nHospitality Group", countyBodyStyle);

            float left = 151 * scale;
            float rightWidth = 181 * scale;
            float tabWidth = (screen.width - left - rightWidth) / CountyTabs.Length;
            for (int tab = 0; tab < CountyTabs.Length; tab++)
            {
                var rect = new Rect(left + tab * tabWidth, 9 * scale, tabWidth - 5 * scale, 37 * scale);
                if (tab == countyTab)
                    Fill(new Rect(rect.x, rect.yMax, rect.width, 3 * scale), CountyGold);
                if (DrawButton(rect, tab == 1 && tabWidth < 105 * scale ? "Properties" : CountyTabs[tab]))
                    SwitchCountyTab(tab);
            }

            float right = screen.width - rightWidth;
            GUI.Label(new Rect(right + 4 * scale, 4 * scale, 126 * scale, 23 * scale), $"Group funds  ${Campaign.Funds:N0}", countyBodyStyle);
            GUI.Label(new Rect(right + 4 * scale, 26 * scale, 126 * scale, 23 * scale), MotelService.DisplayTime(Campaign.ClockMinute), detail);
            if (DrawButton(new Rect(screen.width - 64 * scale, 9 * scale, 55 * scale, 37 * scale), "Menu"))
                SetSettingsVisible(true);

            var map = new Rect(0, toolbarHeight, screen.width, screen.height - toolbarHeight);
            if (countyTab == 0)
            {
                DrawCountyOverview(map, scale);
                DrawCountyPins(screen, scale);
                if (countySelection >= 0)
                    DrawAcquisition(screen, scale);
            }
            else
                DrawCountyManagement(map, scale);
        }

        private void SwitchCountyTab(int tab)
        {
            countyTab = tab;
            if (tab == 2)
                Campaign.ReadUpdates();
            countySelection = -1;
            countyScroll = Vector2.zero;
        }

        private void DrawCountyOverview(Rect map, float scale)
        {
            float x = 14 * scale;
            float y = map.y + 14 * scale;
            var overview = new Rect(x, y, 190 * scale, 133 * scale);
            DrawCountyPanel(overview, scale);
            GUI.Label(new Rect(x + 10 * scale, y + 7 * scale, 177 * scale, 40 * scale), "Riverside County", countyTitleStyle);
            GUI.Label(new Rect(x + 12 * scale, y + 51 * scale, 170 * scale, 25 * scale), "Select a property.", countyBodyStyle);

            int owned = 0;
            int automated = 0;
            for (int property = 0; property < CountyPortfolio.Offers.Length; property++)
            {
                if (Campaign.Owned(property))
                    owned++;
                if (Campaign.Staffed(property))
                    automated++;
            }
            GUI.Label(new Rect(x + 12 * scale, y + 80 * scale, 170 * scale, 20 * scale), $"Owned {owned}/5 · Auto {automated}", countyBodyStyle);
            if (DrawButton(new Rect(x + 10 * scale, y + 105 * scale, 79 * scale, 22 * scale), $"Time ×{countySpeed}"))
                countySpeed = countySpeed == 8 ? 1 : countySpeed * 2;
            if (DrawButton(new Rect(x + 94 * scale, y + 105 * scale, 86 * scale, 22 * scale), quickJoinLan ? "Join LAN" : "LAN co-op"))
            {
                if (quickJoinLan)
                    JoinConfiguredLan();
                else
                    lanMenu = true;
            }

            var legend = new Rect(map.xMax - 331 * scale, map.yMax - 47 * scale, 318 * scale, 33 * scale);
            DrawCountyPanel(legend, scale);
            DrawCountyLegend(legend.x + 9 * scale, legend.y + 7 * scale, "Automated", CountyGreen, scale);
            DrawCountyLegend(legend.x + 97 * scale, legend.y + 7 * scale, "Active", CountyBlue, scale);
            DrawCountyLegend(legend.x + 165 * scale, legend.y + 7 * scale, "Available", CountyPaper, scale);
            DrawCountyLegend(legend.x + 253 * scale, legend.y + 7 * scale, "Locked", CountyGray, scale);
        }

        private void DrawCountyLegend(float x, float y, string text, Color color, float scale)
        {
            Fill(new Rect(x, y + 4 * scale, 9 * scale, 9 * scale), color);
            GUI.Label(new Rect(x + 13 * scale, y - 1 * scale, 76 * scale, 22 * scale), text, countyBodyStyle);
        }

        private void DrawCountyPins(Rect screen, float scale)
        {
            for (int property = 0; property < CountyPins.Length; property++)
            {
                bool future = property == CountyPortfolio.Offers.Length;
                bool owned = !future && Campaign.Owned(property);
                bool automated = owned && Campaign.Staffed(property);
                bool active = owned && property == Campaign.LastVisited;
                bool available = !future && Campaign.Available(property);
                Color color = active ? CountyBlue : automated ? CountyGreen : available ? CountyGold : CountyGray;
                float x = CountyPins[property].x * screen.width;
                float y = CountyPins[property].y * screen.height;
                var badge = new Rect(x - 16 * scale, y - 13 * scale, 31 * scale, 30 * scale);
                DrawCountyBadge(new Rect(badge.x - 2 * scale, badge.y - 2 * scale, badge.width + 4 * scale, badge.height + 4 * scale), CountyPaper);
                DrawCountyBadge(badge, color);
                string badgeText = automated && !active ? "✓" : (property + 1).ToString();
                GUI.Label(badge, badgeText, countyPinStyle);

                float width = 158 * scale;
                var card = new Rect(x + 24 * scale, y + 17 * scale, width, 47 * scale);
                Fill(new Rect(card.x + 2 * scale, card.y + 3 * scale, card.width, card.height), CountyInk);
                Fill(card, CountyPaper);
                string name = future ? "Lakeside Resort" : CountyPortfolio.Offers[property].Name;
                string state = future ? "Future opportunity" : active ? "Active property"
                    : automated ? "Owned · Automated" : owned ? "Owned · Staffing needed"
                    : available ? $"Available · ${CountyPortfolio.Offers[property].Price:N0}" : "Locked";
                GUI.Label(new Rect(card.x + 8 * scale, card.y + 3 * scale, card.width - 8 * scale, 22 * scale), name, countyNameStyle);
                GUI.Label(new Rect(card.x + 8 * scale, card.y + 24 * scale, card.width - 8 * scale, 20 * scale), state, countyStatusStyle);
                if (countySelection < 0 && GUI.Button(new Rect(badge.x, badge.y, card.xMax - badge.x, card.yMax - badge.y),
                    GUIContent.none, GUIStyle.none))
                    countySelection = property;
            }
        }

        private static void DrawCountyBadge(Rect rect, Color tint)
        {
            if (countyPinDisc == null)
            {
                const int size = 32;
                countyPinDisc = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + .5f - size / 2f;
                        float dy = y + .5f - size / 2f;
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(size / 2f - Mathf.Sqrt(dx * dx + dy * dy)) * 255);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                countyPinDisc.SetPixels32(pixels);
                countyPinDisc.Apply(false, true);
            }
            Color old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, countyPinDisc);
            GUI.color = old;
        }

        private void DrawCountyPanel(Rect panel, float scale)
        {
            Fill(panel, CountyInk);
            Fill(new Rect(panel.x, panel.y, panel.width, 2 * scale), CountyGold);
        }

        private void DrawAcquisition(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 295 * scale, scale);
            float x = panel.x + 16 * scale, y = panel.y + 10 * scale, width = panel.width - 32 * scale;
            int property = countySelection;
            bool future = property == CountyPortfolio.Offers.Length;
            string name = future ? "Lakeside Resort" : CountyPortfolio.Offers[property].Name;
            GUI.Label(new Rect(x, y, width, 48 * scale), name, countyTitleStyle);
            if (future)
                GUI.Label(new Rect(x, y + 62 * scale, width, 55 * scale), "Future acquisition opportunity.", detail);
            else
            {
                var offer = CountyPortfolio.Offers[property];
                bool owned = Campaign.Owned(property);
                int rooms = offer.Id == PropertyId.RoadsideMotel && owned ? Campaign.Motel.RoomCapacity : offer.Rooms;
                GUI.Label(new Rect(x, y + 53 * scale, width, 47 * scale), $"{rooms} rooms  ·  {offer.Focus}", countyBodyStyle);
                if (owned)
                {
                    GUI.Label(new Rect(x, y + 105 * scale, width, 27 * scale),
                        $"Operating cash ${Campaign.Cash(property):N0} · Staff {Campaign.StaffCount(property)}/{CountyPortfolio.StaffRoles.Length}", detail);
                    if (DrawButton(new Rect(x, y + 153 * scale, width * .49f, 36 * scale), "Visit property"))
                        VisitCountyProperty(property);
                    if (DrawButton(new Rect(x + width * .51f, y + 153 * scale, width * .49f, 36 * scale), "Manage staff"))
                    {
                        countyProperty = property;
                        SwitchCountyTab(3);
                    }
                }
                else
                {
                    bool available = Campaign.Available(property);
                    string status = available ? $"Purchase ${offer.Price:N0} · Group funds ${Campaign.Funds:N0}"
                        : $"Staff all ten roles at {CountyPortfolio.PropertyName(offer.Requires)} to unlock.";
                    GUI.Label(new Rect(x, y + 105 * scale, width, 37 * scale), status, countyBodyStyle);
                    bool before = GUI.enabled;
                    GUI.enabled &= available && Campaign.Funds >= offer.Price;
                    if (DrawButton(new Rect(x, y + 153 * scale, width, 36 * scale), "Acquire property"))
                        Campaign.BuyProperty(property);
                    GUI.enabled = before;
                }
            }
            if (DrawButton(new Rect(x, y + 220 * scale, width, 35 * scale), "Back to county"))
                countySelection = -1;
        }

        private void VisitCountyProperty(int property)
        {
            Campaign.Visit(property);
            if (!PropertyTravelManager.Visit(CountyPortfolio.Offers[property].Id))
            {
                countyProperty = property;
                SwitchCountyTab(4);
            }
        }

        private void DrawCountyManagement(Rect map, float scale)
        {
            float width = Mathf.Min(675 * scale, map.width - 32 * scale);
            var panel = new Rect((map.width - width) / 2, map.y + 15 * scale, width, map.height - 30 * scale);
            DrawCountyPanel(panel, scale);
            float x = panel.x + 18 * scale, y = panel.y + 10 * scale, inner = width - 36 * scale;
            GUI.Label(new Rect(x, y, inner, 43 * scale), CountyTabs[countyTab], countyTitleStyle);
            if (countyTab == 2)
            {
                DrawPortfolioFinances(new Rect(x, y + 48 * scale, inner, panel.height - 70 * scale), scale);
                return;
            }

            if (countyTab == 1)
            {
                for (int property = 0; property < CountyPortfolio.Offers.Length; property++)
                {
                    var offer = CountyPortfolio.Offers[property];
                    if (!Campaign.Owned(property))
                        continue;
                    float row = y + (53 + property * 55) * scale;
                    GUI.Label(new Rect(x, row, inner - 195 * scale, 26 * scale), offer.Name, detail);
                    GUI.Label(new Rect(x, row + 23 * scale, inner - 195 * scale, 24 * scale),
                        $"${Campaign.Cash(property):N0} · {(Campaign.Staffed(property) ? "Automated" : "Staffing needed")}", countyBodyStyle);
                    if (DrawButton(new Rect(x + inner - 191 * scale, row, 90 * scale, 32 * scale), "Visit"))
                        VisitCountyProperty(property);
                    if (DrawButton(new Rect(x + inner - 94 * scale, row, 94 * scale, 32 * scale), "Staff"))
                    {
                        countyProperty = property;
                        SwitchCountyTab(3);
                    }
                }
                return;
            }

            if (DrawButton(new Rect(x, y + 48 * scale, inner, 30 * scale),
                $"{CountyPortfolio.Offers[countyProperty].Name} · Change property"))
            {
                do countyProperty = (countyProperty + 1) % CountyPortfolio.Offers.Length;
                while (!Campaign.Owned(countyProperty));
                countyScroll = Vector2.zero;
            }

            GUI.Label(new Rect(x, y + 82 * scale, inner, 25 * scale),
                $"Cash ${Campaign.Cash(countyProperty):N0} · Staff {Campaign.StaffCount(countyProperty)}/{CountyPortfolio.StaffRoles.Length} · Rating {Campaign.Reputation(CountyPortfolio.Offers[countyProperty].Id)}%", countyBodyStyle);
            if (countyTab == 3)
                DrawCountyStaff(new Rect(x, y + 113 * scale, inner, panel.height - 137 * scale), scale);
            else
            {
                if (DrawButton(new Rect(x, y + 111 * scale, inner, 29 * scale),
                    viewingMastery ? "Back to operations" : "View property mastery"))
                {
                    viewingMastery = !viewingMastery;
                    countyScroll = Vector2.zero;
                }
                var operations = new Rect(x, y + 146 * scale, inner, panel.height - 171 * scale);
                if (viewingMastery)
                {
                    countyScroll = GUI.BeginScrollView(operations, countyScroll, new Rect(0, 0, inner - 20 * scale, 294 * scale));
                    DrawMastery(new Rect(0, 0, inner - 20 * scale, 294 * scale), scale);
                    GUI.EndScrollView();
                }
                else
                    DrawHotelOperations(operations, scale);
            }
        }

        private void DrawCountyStaff(Rect view, float scale)
        {
            float width = view.width - 20 * scale;
            countyScroll = GUI.BeginScrollView(view, countyScroll,
                new Rect(0, 0, width, CountyPortfolio.StaffRoles.Length * 49 * scale));
            for (int role = 0; role < CountyPortfolio.StaffRoles.Length; role++)
            {
                float y = role * 49 * scale;
                bool employed = Campaign.HasEmployee(countyProperty, role);
                GUI.Label(new Rect(0, y, width - 110 * scale, 24 * scale), CountyPortfolio.StaffRoles[role], detail);
                GUI.Label(new Rect(0, y + 21 * scale, width - 110 * scale, 24 * scale), CountyPortfolio.Duties[role], countyBodyStyle);
                bool before = GUI.enabled;
                GUI.enabled &= employed || Campaign.Cash(countyProperty) >= CountyPortfolio.HireCost;
                if (DrawButton(new Rect(width - 108 * scale, y + 5 * scale, 108 * scale, 32 * scale),
                    employed ? "Dismiss" : $"Hire ${CountyPortfolio.HireCost}"))
                    Campaign.ToggleEmployee(countyProperty, role);
                GUI.enabled = before;
            }
            GUI.EndScrollView();
        }

        private void DrawHotelOperations(Rect viewport, float scale)
        {
            float width = viewport.width - 20 * scale;
            countyScroll = GUI.BeginScrollView(viewport, countyScroll, new Rect(0, 0, width, 565 * scale));
            int property = countyProperty;
            GUI.Label(new Rect(0, 0, width, 27 * scale), CountyPortfolio.Offers[property].Focus, countyBodyStyle);
            float half = (width - 6 * scale) / 2;
            if (DrawButton(new Rect(0, 31 * scale, half, 30 * scale), "Move up to $250 to group"))
                Campaign.Transfer(property, false);
            if (DrawButton(new Rect(half + 6 * scale, 31 * scale, half, 30 * scale), "Send up to $250 to property"))
                Campaign.Transfer(property, true);

            if (CountyPortfolio.Offers[property].Id == PropertyId.RoadsideMotel)
            {
                if (DrawButton(new Rect(0, 72 * scale, width, 33 * scale), "Visit roadside motel"))
                    VisitCountyProperty(CountyPortfolio.MapSlot(PropertyId.RoadsideMotel));
            }
            else
            {
                CountyPortfolio.Hotel hotel = Campaign.Business(property);
                GUI.Label(new Rect(0, 73 * scale, width, 25 * scale),
                    $"Occupied {hotel.occupied}/{CountyPortfolio.Offers[property].Rooms} · Arrivals {hotel.arriving} · Ready {hotel.prepared} · Stock {hotel.stock}", detail);
                if (hotel.deliveryQuantity > 0)
                {
                    bool ready = Campaign.HotelDeliveryReady(hotel.propertyId);
                    HotelAction(6, 105, width, scale, ready ? "Receive order at the supply bay"
                        : $"Delivery due {MotelService.DisplayTime(hotel.deliveryDue)}", ready);
                }
                else
                    HotelAction(0, 105, width, scale, $"Order bulk stock ×{CountyPortfolio.Offers[property].Rooms} · ${5 * (CountyPortfolio.Offers[property].Rooms / 2)}");
                HotelAction(1, 141, width, scale, "Prepare available rooms");
                HotelAction(2, 177, width, scale, "Check in waiting guests");
                if (hotel.propertyId == PropertyId.BusinessHotel)
                {
                    HotelAction(4, 219, width, scale, $"Prepare breakfast ×25 · $75 · {hotel.breakfast} ready");
                    HotelAction(3, 255, width, scale, "Serve breakfast to checked-in guests");
                    HotelAction(5, 291, width, scale, hotel.groupDay > 0 ? $"Conference reserved · Day {hotel.groupDay + 1} · Hosted {hotel.eventsHosted}"
                        : $"Reserve tomorrow · 12 rooms · Hosted {hotel.eventsHosted}", hotel.groupDay == 0);
                }
                else if (hotel.propertyId == PropertyId.BoutiqueHotel)
                {
                    HotelAction(4, 219, width, scale, $"Refit suites · $320 · Quality level {hotel.suiteLevel}/3", hotel.suiteLevel < 3);
                    HotelAction(3, 255, width, scale, $"Fulfill personalized requests · {hotel.requests} waiting");
                }
                else if (hotel.propertyId == PropertyId.LuxuryHotel)
                {
                    HotelAction(3, 218, width, scale, $"Serve VIP guests · {hotel.vip} waiting");
                    for (int department = 0; department < LuxuryDepartments.Length; department++)
                    {
                        bool open = (hotel.luxuryDepartments & (1 << department)) != 0;
                        bool paused = (hotel.pausedDepartments & (1 << department)) != 0;
                        var rect = new Rect((department % 2) * (half + 6 * scale), (258 + (department / 2) * 36) * scale, half, 30 * scale);
                        if (DrawButton(rect, open ? $"{(paused ? "Reopen" : "Pause")} {LuxuryDepartments[department].ToLowerInvariant()}"
                            : $"{LuxuryDepartments[department]} · ${230 + department * 45}"))
                            ApplyHotelAction((open ? 10 : 4) + department);
                    }
                }
                HotelAction(22, 383, width, scale, $"Deliver luggage · {hotel.luggage} waiting", hotel.luggage > 0);
                HotelAction(23, 417, width, scale, $"Serve guest requests · {hotel.serviceRequests} waiting", hotel.serviceRequests > 0);
                HotelAction(24, 451, width, scale, $"Wash linen · {hotel.dirtyLinen} dirty", hotel.dirtyLinen > 0);
                HotelAction(21, 485, width, scale, $"Service equipment · {hotel.condition}% condition · $24", hotel.condition < 90);
                HotelAction(20, 519, width, scale, hotel.incident != PropertyIncident.None
                    ? $"Respond to incident · {hotel.incident}" : $"Resolve noise complaints · {hotel.noiseReports} reported",
                    hotel.incident != PropertyIncident.None || hotel.noiseReports > 0);
            }
            GUI.EndScrollView();
        }

        private void HotelAction(int action, float y, float width, float scale, string text, bool enabled = true)
        {
            bool before = GUI.enabled;
            GUI.enabled &= enabled;
            if (DrawButton(new Rect(0, y * scale, width, 30 * scale), text))
                ApplyHotelAction(action);
            GUI.enabled = before;
        }

        private void ApplyHotelAction(int action)
        {
            if (Campaign.RunHotelAction(countyProperty, action))
                Campaign.Save();
        }

        private void DrawPortfolioInbox(Rect viewport, float scale)
        {
            if (countyBodyStyle == null)
                PrepareCountyStyles(scale);
            else
                countyBodyStyle.fontSize = Mathf.RoundToInt(12 * scale);
            var notes = Campaign.Inbox;
            if (notes.Count == 0)
            {
                GUI.Label(new Rect(viewport.x, viewport.y, viewport.width, 50 * scale),
                    "Staff updates and daily financial statements appear here.", countyBodyStyle);
                return;
            }
            float width = viewport.width - 20 * scale;
            inboxScroll = GUI.BeginScrollView(viewport, inboxScroll,
                new Rect(0, 0, width, notes.Count * 73 * scale));
            for (int row = 0; row < notes.Count; row++)
            {
                CountyPortfolio.UpdateNote note = notes[notes.Count - 1 - row];
                float y = row * 73 * scale;
                GUI.Label(new Rect(0, y, width, 25 * scale),
                    $"Day {note.day + 1} · {CountyPortfolio.PropertyName(note.propertyId)}{(note.statement && note.text != null ? " · Accountant" : "")}", detail);
                if (note.statement)
                {
                    GUI.Label(new Rect(0, y + 24 * scale, width, 22 * scale),
                        $"Revenue ${note.revenue:N0} · Wages ${note.payroll:N0} · Bills ${note.bills:N0}", countyBodyStyle);
                    GUI.Label(new Rect(0, y + 45 * scale, width, 22 * scale),
                        $"Expenses ${note.expenses:N0} · Net ${note.revenue - note.payroll - note.bills - note.expenses:N0} · Cash ${note.closingCash:N0}", countyBodyStyle);
                }
                else
                    GUI.Label(new Rect(0, y + 24 * scale, width, 40 * scale), note.text, countyBodyStyle);
            }
            GUI.EndScrollView();
        }
    }
}
