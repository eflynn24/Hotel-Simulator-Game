using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertLantern
{
    public sealed class PropertyView : MonoBehaviour
    {
        private static readonly Color Accent = new Color32(255, 209, 134, 255);
        private static readonly Color Ink = new Color32(36, 24, 16, 255);
        private static readonly string[] Guests = { "Guest-Mara", "Guest-Eli", "Guest-June", "Guest-Vic" };
        private const string BrightnessKey = "display.brightness";
        private const string LargeTextKey = "display.largeText";
        private const int NoRoom = -1;
        private const float Reach = 1.38f;
        private static int occupiedRooms;

        [SerializeField] private bool isMap;

        private MotelWorld world;
        private Camera view;
        private MotelWalker caretaker;
        private MotelWalker guest;
        private readonly Queue<Vector3> walkRoute = new Queue<Vector3>();
        private readonly Queue<Vector3> guestRoute = new Queue<Vector3>();
        private Vector3 lastBreadcrumb;
        private Vector3 previousMouse;
        private float yaw;
        private float pitch = 19;
        private float nextGuestDelay;
        private bool escorting;
        private bool showSettings;
        private int inspectedRoom = NoRoom;
        private float brightness;
        private bool largeText;
        private GUIStyle title, detail, label, button;

        private bool AtOffice => MotelWalker.Distance(caretaker.Position, MotelWorld.OfficeApproach) < 1.6f;
        private bool GuestWaiting => guest != null && !escorting;
        private bool Modal => showSettings || inspectedRoom != NoRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => occupiedRooms = 0;

        private void Awake()
        {
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), .6f, 1f);
            largeText = PlayerPrefs.GetInt(LargeTextKey, 0) != 0;
            world = new MotelWorld(transform, isMap);
            view = new GameObject(isMap ? "County Camera" : "Follow Camera").AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.66f, .71f, .72f);
            view.fieldOfView = 62;
            view.nearClipPlane = .09f;
            view.farClipPlane = 360;
            if (isMap)
            {
                var viewpoint = new Vector3(38, 82, -105);
                view.transform.SetPositionAndRotation(viewpoint,
                    Quaternion.LookRotation(new Vector3(0, 1, -2) - viewpoint));
                return;
            }

            caretaker = new MotelWalker(transform, "Caretaker", MotelWorld.Spawn);
            SpawnGuest();
            PlaceCamera(1);
        }

        private void Update()
        {
            if (isMap)
                return;

            float delta = Time.deltaTime;
            Vector3 mouse = Input.mousePosition;
            if (!Modal)
            {
                if (Input.GetMouseButton(1) && !Input.GetMouseButtonDown(1))
                {
                    Vector3 offset = mouse - previousMouse;
                    yaw += offset.x * .23f;
                    pitch = Mathf.Clamp(pitch - offset.y * .18f, 12, 61);
                }
                UpdateWalk(delta);
                UpdateGuest(delta);
                if (Input.GetKeyDown(KeyCode.E))
                    Interact();
            }
            previousMouse = mouse;
        }

        private void LateUpdate()
        {
            if (!isMap && caretaker != null)
                PlaceCamera(1 - Mathf.Exp(-11f * Time.deltaTime));
        }

        private void PlaceCamera(float blend)
        {
            Vector3 focus = caretaker.Position + Vector3.up * 1.42f;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
            Vector3 direction = orbit * Vector3.back;
            float distance = 5f;
            if (Physics.SphereCast(focus, .20f, direction, out RaycastHit hit, distance, 1 << MotelWorld.SolidLayer))
                distance = Mathf.Max(.42f, hit.distance - .12f);
            Transform cameraTransform = view.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, focus + direction * distance, blend);
            cameraTransform.rotation = orbit;
        }

        private void UpdateWalk(float delta)
        {
            var keys = new Vector2(Axis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow),
                Axis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow));
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = sprint ? MotelWalker.RunSpeed : MotelWalker.WalkSpeed;
            Vector3 movement = Vector3.zero;
            if (keys != Vector2.zero)
            {
                walkRoute.Clear();
                keys = Vector2.ClampMagnitude(keys, 1);
                movement = Quaternion.Euler(0, yaw, 0) * new Vector3(keys.x, 0, keys.y) * speed;
            }
            else if (walkRoute.TryPeek(out Vector3 target))
            {
                if (MotelWalker.Distance(caretaker.Position, target) < .16f)
                    walkRoute.Dequeue();
                if (walkRoute.TryPeek(out target))
                    movement = MotelWalker.Toward(caretaker.Position, target, speed);
            }
            caretaker.Move(movement, delta);
        }

        private static int Axis(KeyCode positive, KeyCode alternatePositive, KeyCode negative, KeyCode alternateNegative)
        {
            int value = Input.GetKey(positive) || Input.GetKey(alternatePositive) ? 1 : 0;
            return value - (Input.GetKey(negative) || Input.GetKey(alternateNegative) ? 1 : 0);
        }

        private void SpawnGuest()
        {
            if (occupiedRooms >= Guests.Length)
                return;
            guest = new MotelWalker(transform, Guests[occupiedRooms], MotelWorld.GuestSpawn);
            Physics.IgnoreCollision(caretaker.Capsule, guest.Capsule);
        }

        private void StartEscort()
        {
            walkRoute.Clear();
            guestRoute.Clear();
            lastBreadcrumb = caretaker.Position;
            guestRoute.Enqueue(new Vector3(MotelWorld.OfficeApproach.x, 0, 4.55f));
            guestRoute.Enqueue(lastBreadcrumb);
            escorting = true;
        }

        private void UpdateGuest(float delta)
        {
            if (nextGuestDelay > 0)
            {
                nextGuestDelay -= delta;
                if (nextGuestDelay <= 0)
                    SpawnGuest();
            }
            if (guest == null)
                return;

            if (!escorting)
            {
                guest.Move(Vector3.zero, delta);
                return;
            }

            Vector3 current = caretaker.Position;
            Vector3 assigned = MotelWorld.RoomApproach(occupiedRooms);
            bool atRoom = MotelWalker.Distance(current, assigned) < Reach;
            if (MotelWalker.Distance(lastBreadcrumb, current) > .52f)
            {
                lastBreadcrumb = current;
                guestRoute.Enqueue(current);
            }

            if (guestRoute.TryPeek(out Vector3 head) && MotelWalker.Distance(guest.Position, head) < .28f)
                guestRoute.Dequeue();

            Vector3 doorstep = assigned + new Vector3(.46f, 0, .21f);
            Vector3 desired = Vector3.zero;
            if (guestRoute.TryPeek(out head) && (guestRoute.Count > 2 || atRoom))
                desired = MotelWalker.Toward(guest.Position, head, 4.4f);
            else if (atRoom)
                desired = MotelWalker.Toward(guest.Position, doorstep, 2.5f);

            guest.Move(desired, delta);
            if (atRoom && guestRoute.Count == 0 && MotelWalker.Distance(guest.Position, doorstep) < .32f)
            {
                guest.Dispose();
                guest = null;
                escorting = false;
                occupiedRooms++;
                nextGuestDelay = 1.15f;
            }
        }

        private int NearRoom()
        {
            for (int i = 0; i < Guests.Length; i++)
                if (MotelWalker.Distance(caretaker.Position, MotelWorld.RoomApproach(i)) < Reach)
                    return i;
            return NoRoom;
        }

        private void Interact()
        {
            if (GuestWaiting && AtOffice)
            {
                StartEscort();
                return;
            }

            int room = NearRoom();
            if (!escorting && room != NoRoom)
            {
                walkRoute.Clear();
                inspectedRoom = room;
                SetActorPause(true);
            }
        }

        private void SetActorPause(bool paused)
        {
            caretaker?.SetPaused(paused);
            guest?.SetPaused(paused);
        }

        private void SetDestination(Vector3 destination)
        {
            walkRoute.Clear();
            bool inside = InsideOffice(caretaker.Position);
            bool destinationInside = InsideOffice(destination);
            Vector3 threshold = MotelWorld.OfficeApproach;
            if (inside && !destinationInside)
            {
                walkRoute.Enqueue(new Vector3(threshold.x, 0, 4.55f));
                walkRoute.Enqueue(threshold);
            }

            Vector3 start = inside && !destinationInside ? threshold : caretaker.Position;
            Vector3 end = !inside && destinationInside ? threshold : destination;
            bool leftToRooms = start.x < -7.5f && end.x > -7.05f && end.z > 3.65f;
            bool roomsToLeft = end.x < -7.5f && start.x > -7.05f && start.z > 3.65f;
            if (leftToRooms || roomsToLeft)
                walkRoute.Enqueue(new Vector3(-6.86f, 0, 3.1f));

            if (!inside && destinationInside)
            {
                walkRoute.Enqueue(threshold);
                walkRoute.Enqueue(new Vector3(threshold.x, 0, 4.55f));
            }
            destination.x = Mathf.Clamp(destination.x, -16.2f, 16.2f);
            destination.z = Mathf.Clamp(destination.z, -13.2f, 9.6f);
            walkRoute.Enqueue(destination);
        }

        private static bool InsideOffice(Vector3 p) => p.x > -11.95f && p.x < -7.44f && p.z > 4.08f && p.z < 8.58f;

        private void HandleWorldClick()
        {
            Event current = Event.current;
            if (!GUI.enabled || current.type != EventType.MouseDown || current.button != 0)
                return;
            Vector2 p = current.mousePosition;
            var ray = view.ScreenPointToRay(new Vector3(p.x, Screen.height - p.y, 0));
            if (!Physics.Raycast(ray, out RaycastHit hit, 150,
                (1 << MotelWorld.GroundLayer) | (1 << MotelWorld.SolidLayer)))
                return;

            if (hit.collider.gameObject.layer == MotelWorld.GroundLayer)
            {
                SetDestination(hit.point);
            }
            else if (hit.point.x > -12.1f && hit.point.x < -7.2f && hit.point.z > 3.85f)
            {
                if (GuestWaiting && AtOffice)
                    StartEscort();
                else
                    SetDestination(MotelWorld.OfficeApproach);
            }
            else
            {
                for (int room = 0; room < Guests.Length; room++)
                {
                    if (Mathf.Abs(hit.point.x - MotelWorld.RoomX[room]) > .73f || Mathf.Abs(hit.point.z - 6.38f) > .27f)
                        continue;
                    SelectDoor(room);
                    break;
                }
            }
            current.Use();
        }

        private void SelectDoor(int room)
        {
            if (!escorting && NearRoom() == room)
                Interact();
            else
                SetDestination(MotelWorld.RoomApproach(room));
        }

        private void OnGUI()
        {
            float width = Screen.width;
            float height = Screen.height;
            float scale = Mathf.Clamp(Mathf.Min(width / 800f, height / 540f), .6f, 2);
            var screen = new Rect(0, 0, width, height);
            PrepareStyles(scale);
            Fill(screen, new Color(0, 0, 0, 1 - brightness));

            Event input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            {
                if (inspectedRoom != NoRoom)
                {
                    inspectedRoom = NoRoom;
                    SetActorPause(false);
                }
                else
                    SetSettingsVisible(!showSettings);
                input.Use();
            }

            float margin = 24 * scale;
            GUI.enabled = !Modal;
            if (DrawButton(new Rect(width - margin - 96 * scale, margin, 96 * scale, 44 * scale), "Menu"))
                SetSettingsVisible(true);

            if (isMap)
                DrawMap(screen, margin, scale);
            else
                DrawExterior(screen, margin, scale);

            GUI.enabled = true;
            if (showSettings)
                DrawSettings(screen, scale);
            else if (inspectedRoom != NoRoom)
                DrawRoomDetails(screen, scale);
        }

        private void DrawMap(Rect screen, float margin, float scale)
        {
            Rect header = new Rect(margin, margin + 51 * scale, Mathf.Min(383 * scale, screen.width - 2 * margin), 96 * scale);
            Fill(header, Ink);
            GUI.Label(new Rect(header.x + 12 * scale, header.y + 5 * scale, header.width, 54 * scale), "County Map", title);
            GUI.Label(new Rect(header.x + 12 * scale, header.y + 58 * scale, header.width, 28 * scale), "Select the motel to visit the property.", detail);

            if (DrawWorldButton(new Vector3(1.6f, 6, -1), "Desert Lantern Motel", 226 * scale, scale))
                SceneManager.LoadScene("MotelExterior");
        }

        private void DrawExterior(Rect screen, float margin, float scale)
        {
            if (caretaker == null)
                return;
            if (DrawButton(new Rect(margin, margin, 148 * scale, 44 * scale), "Back to map"))
                SceneManager.LoadScene("CountyMap");

            if (GuestWaiting && DrawWorldButton(MotelWorld.OfficeSign, "Front Office", 116 * scale, scale))
            {
                if (AtOffice)
                    StartEscort();
                else
                    SetDestination(MotelWorld.OfficeApproach);
            }
            if (escorting && DrawWorldButton(MotelWorld.RoomSign(occupiedRooms), $"Room {occupiedRooms + 1}", 87 * scale, scale))
                SelectDoor(occupiedRooms);

            int room = NearRoom();
            string objective = escorting
                ? (room == occupiedRooms ? "Wait at the door for your guest" : $"Escort your guest to Room {occupiedRooms + 1}")
                : occupiedRooms >= Guests.Length ? "All guests are checked in"
                : AtOffice && GuestWaiting ? "E or click Front Office to check in your guest"
                : room != NoRoom ? $"E to inspect Room {room + 1}"
                : "Meet your next guest at the Front Office";
            float width = Mathf.Min(610 * scale, screen.width - 2 * margin);
            Rect hud = new Rect(margin, screen.height - margin - 61 * scale, width, 61 * scale);
            Fill(hud, Ink);
            GUI.Label(new Rect(hud.x + 10 * scale, hud.y + 4 * scale, hud.width - 20 * scale, 27 * scale), objective, detail);
            GUI.Label(new Rect(hud.x + 10 * scale, hud.y + 31 * scale, hud.width - 20 * scale, 25 * scale),
                $"{occupiedRooms}/{Guests.Length} occupied  ·  WASD or click  ·  Shift to run  ·  Right drag to look", label);
            if (!hud.Contains(Event.current.mousePosition))
                HandleWorldClick();
        }

        private bool DrawWorldButton(Vector3 position, string text, float width, float scale)
        {
            Vector3 projected = view.WorldToScreenPoint(position);
            if (projected.z <= 0)
                return false;
            float y = Screen.height - projected.y;
            if (projected.x < width / 2 || projected.x > Screen.width - width / 2 ||
                y < 15 * scale || y > Screen.height - 45 * scale)
                return false;
            return DrawButton(new Rect(projected.x - width / 2, y - 16 * scale, width, 32 * scale), text);
        }

        private void OnDestroy()
        {
            caretaker?.Dispose();
            guest?.Dispose();
            world?.Dispose();
            if (view != null)
                Destroy(view.gameObject);
        }

        private void SetSettingsVisible(bool visible)
        {
            showSettings = visible;
            SetActorPause(visible);
            if (!visible)
                PlayerPrefs.Save();
        }

        private Rect DrawOverlay(Rect screen, float height, float scale)
        {
            Fill(screen, new Color(0, 0, 0, 0.55f));
            float width = Mathf.Min(410 * scale, screen.width - 32 * scale);
            Rect panel = new Rect((screen.width - width) / 2, (screen.height - height) / 2,
                width, height);
            Fill(panel, Ink);
            Fill(new Rect(panel.x, panel.y, width, 2 * scale), Accent);
            return panel;
        }

        private void DrawSettings(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 248 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 12 * scale, width, 50 * scale), "Settings", title);
            GUI.Label(new Rect(left, panel.y + 69 * scale, width, 28 * scale),
                $"Scene brightness  {Mathf.RoundToInt(brightness * 100)}%", detail);

            float next = GUI.HorizontalSlider(
                new Rect(left, panel.y + 104 * scale, width, 24 * scale), brightness, 0.6f, 1f);
            if (!Mathf.Approximately(next, brightness))
            {
                brightness = next;
                PlayerPrefs.SetFloat(BrightnessKey, brightness);
            }

            if (DrawButton(new Rect(left, panel.y + 136 * scale, width, 40 * scale),
                largeText ? "Text size: Large" : "Text size: Standard"))
            {
                largeText = !largeText;
                PlayerPrefs.SetInt(LargeTextKey, largeText ? 1 : 0);
            }

            if (DrawButton(new Rect(left, panel.y + 192 * scale, width, 40 * scale), "Resume"))
                SetSettingsVisible(false);
        }

        private void DrawRoomDetails(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 166 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 12 * scale, width, 50 * scale),
                $"Room {inspectedRoom + 1}", title);
            GUI.Label(new Rect(left, panel.y + 66 * scale, width, 30 * scale),
                inspectedRoom < occupiedRooms ? "Occupied by a checked-in guest." : "Vacant and ready for check-in.", detail);
            if (DrawButton(new Rect(left, panel.y + 110 * scale, width, 40 * scale), "Back to exterior"))
            {
                inspectedRoom = NoRoom;
                SetActorPause(false);
            }
        }

        private void PrepareStyles(float scale)
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                detail = new GUIStyle(GUI.skin.label);
                label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                title.normal.textColor = Color.white;
                detail.normal.textColor = Color.white;
                label.normal.textColor = Accent;
            }

            title.fontSize = Mathf.RoundToInt((largeText ? 40 : 36) * scale);
            detail.fontSize = Mathf.RoundToInt((largeText ? 19 : 16) * scale);
            label.fontSize = Mathf.RoundToInt(14 * scale);
            button.fontSize = Mathf.RoundToInt(17 * scale);
        }

        private bool DrawButton(Rect bounds, string text)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Accent;
            bool clicked = GUI.Button(bounds, text, button);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
