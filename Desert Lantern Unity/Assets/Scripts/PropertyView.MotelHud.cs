using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private enum HudIcon { None, Map, Phone, Notebook, Menu, Calendar, Cash, Room, Warning, Play }

        private static readonly Color HudInk = new Color32(43, 32, 23, 242);
        private static readonly Color HudEdge = new Color32(158, 119, 65, 240);
        private static readonly Color HudText = new Color32(253, 233, 195, 255);
        private static readonly Color HudIconColor = new Color32(233, 183, 103, 255);
        private static readonly byte[,] HudGlyphs =
        {
            { 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0x21, 0x73, 0xBF, 0xAD, 0xAD, 0xAD, 0xED, 0xC0 },
            { 0x30, 0x78, 0x70, 0x60, 0x32, 0x1F, 0x0F, 0x03 },
            { 0x7E, 0x42, 0xDE, 0x42, 0xDE, 0x42, 0xDE, 0x7E },
            { 0, 0xFF, 0, 0xFF, 0, 0xFF, 0, 0 },
            { 0x24, 0xFF, 0x81, 0xFF, 0x81, 0x95, 0x81, 0xFF },
            { 0x3C, 0x7E, 0xDB, 0xC7, 0xE3, 0xDB, 0x7E, 0x3C },
            { 0, 0x80, 0xB0, 0xB6, 0xFF, 0xFF, 0x81, 0 },
            { 0x18, 0x18, 0x3C, 0x24, 0x66, 0x7E, 0xDB, 0xFF },
            { 0x40, 0x60, 0x70, 0x78, 0x78, 0x70, 0x60, 0x40 }
        };

        private GUIStyle hudLabel;
        private Rect hudActionPrompt;

        private void DrawHudIcon(Rect bounds, HudIcon icon, float scale)
        {
            if (icon == HudIcon.None || Event.current.type != EventType.Repaint)
                return;
            float unit = Mathf.Max(1, Mathf.Round(1.65f * scale));
            float x = Mathf.Round(bounds.x);
            float y = Mathf.Round(bounds.center.y - 4 * unit);
            for (int row = 0; row < 8; row++)
                for (int column = 0; column < 8; column++)
                    if ((HudGlyphs[(int)icon, row] & (128 >> column)) != 0)
                        Fill(new Rect(x + column * unit, y + row * unit, unit, unit), HudIconColor);
        }

        private void DrawHudText(Rect rect, string caption, float scale, HudIcon icon = HudIcon.None)
        {
            hudLabel ??= new GUIStyle(GUIStyle.none)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            hudLabel.normal.textColor = HudText;
            hudLabel.fontSize = Mathf.RoundToInt((largeText ? 16 : 13) * scale);
            float padding = (icon == HudIcon.None ? 10 : 31) * scale;
            DrawHudIcon(new Rect(rect.x + 9 * scale, rect.y, 17 * scale, rect.height), icon, scale);
            DrawFittedText(new Rect(rect.x + padding, rect.y, rect.width - padding - 7 * scale, rect.height), caption, hudLabel);
        }

        private bool MotelHudButton(Rect rect, string caption, float scale, HudIcon icon = HudIcon.None)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Color surface = hover ? new Color32(78, 55, 33, 255) : HudInk;
            DeskFrame(rect, surface, HudEdge, 7 * scale, Mathf.Max(1, scale));
            DrawHudText(rect, caption, scale, icon);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void DrawMotelLedger(Rect rect, MotelService service, float scale)
        {
            DeskFrame(rect, HudInk, HudEdge, 7 * scale, Mathf.Max(1, scale));
            float hourWidth = rect.width * .36f;
            float cashWidth = rect.width * .24f;
            DrawHudText(new Rect(rect.x, rect.y, hourWidth, rect.height),
                MotelService.DisplayTime(service.ClockMinute), scale, HudIcon.Calendar);
            DrawHudText(new Rect(rect.x + hourWidth, rect.y, cashWidth, rect.height),
                $"${service.Cash:N0}", scale, HudIcon.Cash);
            DrawHudText(new Rect(rect.x + hourWidth + cashWidth, rect.y, rect.width - hourWidth - cashWidth, rect.height),
                $"{service.ActiveRoomCount} / {service.RoomCapacity} occupied", scale, HudIcon.Room);
        }
    }
}
