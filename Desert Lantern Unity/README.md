# Desert Lantern Motel in Unity

This Unity 6 prototype starts over a 3D view of the county. Select
**Desert Lantern Motel** to take control of the caretaker in third person.

- Walk with **WASD** or **arrow keys**, hold **Shift** to run, and drag with
  the **right mouse button** to orbit the camera. You can also click a spot
  in the parking lot to walk to it.
- Visit the **Front Office** to receive bookings and check in guests. Once
  you hand over the key, the guest goes to the assigned room on their own.
- Start with **$230**. Check-in collects the quoted room charge and a
  refundable deposit. Follow the luggage prompt to the guest's car, pick up
  the case with **E**, and set it down inside the assigned room for a tip.
- Open the reception computer with **E**. **Overview** shows arrivals,
  departures, room readiness, guest requests, inventory and finances.
  Click a summary or alert to see its management screen.
- Use the reception computer's **Bookings** tab. Pick a date and room on the
  **Room calendar**, adjust the stay length or special request, then confirm.
  The **Arrivals** and **Departures** lists show each day's activity, and
  selecting a reservation opens its details and edit controls.
- Walk-ins tell you how many nights they need. Confirm the suggested room
  in **Arrivals**, take its key from behind the counter, and give the key to
  the guest. For a telephone booking, answer the desk phone to see the
  caller's dates and accept or decline the suggested room.
- Online bookings confirm automatically and show a notification. Use
  **Online: Auto-confirm** on the calendar to switch to manual approval.
  Review those bookings under **Requests**.
- When a departing guest reaches the desk, use **Departures** to check them
  out. Checkout creates housekeeping work. Hired receptionists can handle
  calls, arrivals, and departures. Guests who leave unattended count as
  missed checkouts.
- A full motel day takes four real minutes at normal speed. Use the HUD's
  **Time x1** button to cycle through 2x, 4x, and 8x. The headless LAN host
  pauses the clock while no players are connected.
- Press **N** to open the **Manager’s notebook** from anywhere at the motel.
  Open a task to see the location, supplies, deadline and next step. Pin
  one to the HUD, toggle its waypoint, or take an eligible task over from
  staff. Release it to allow staff or a coworker to take over. **Upcoming**
  shows scheduled work and **History** shows recent outcomes.
- Order through **Supplies** at reception or from a low-stock task in the
  notebook. Standard orders arrive after **30 motel minutes**, and you can
  place additional orders while earlier ones are pending. Different
  players can carry separate crates at the same time. Pick up a crate
  at the delivery bay with **E** and stock it at the shelves in
  **Housekeeping**, next to reception.
- Accept a guest request in the notebook, collect any needed supplies
  from the housekeeping shelves, then deliver them to the guest. A claim
  reserves the task for the assigned player.
- Hire a bellhop under **Staff** for $55 to collect and deliver luggage.
  Press **I** at an occupied room door to sell TV access or a room-service meal.
  The exterior stairs at the right end of the wing lead to rooms 5–8.
- Open or close a nearby room door with **E**. Press **I** near it to check
  occupancy. **Menu** or **Escape** opens the display settings.
  **Back to map** returns to the county view.

- Checkout returns the room key and opens a housekeeping checklist. Load
  the cart in **Housekeeping**, then use the checklist or press **E** beside
  a task. Room preparation includes linens, bathroom cleaning, floors,
  and restocking towels and toiletries. The checklist shows progress
  and the next confirmed arrival.
- Start the washer in **Housekeeping** with **E**. A load uses one
  detergent and returns up to four used linens and towels to clean stock
  after 90 motel minutes.


1. Add this folder from disk in Unity Hub and open it with Unity 6 (6000.0).
   On Windows, use the equivalent WSL folder shown by running
   `wslpath -w "$PWD"` from this directory.
2. Choose **Desert Lantern > Play County Map** in the Editor menu. You can
   also open `Assets/Scenes/CountyMap.unity` and press **Play**.
3. For a standalone build, open **File > Build Profiles**, select your platform,
   and build. The scene list starts with County Map and includes Motel Exterior.

The buildings, terrain, and characters are 3D. The caretaker is a new
skinned model based on the original concept in `Assets/Art/Caretaker.png`.
The caretaker and guest FBX assets use the CC0 MakeHuman anatomy assets and
were exported with Ara 3D Gen. Unity blends idle, walk, and run clips on
their skeletons while a capsule controller handles collisions. The two-story
wing, porch, reception, four furnished ground-floor rooms, and cars are built
from Unity geometry and procedural materials. The upper floor is exterior
scenery. The reference photos were used for design only.

[MakeHuman asset license](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.md)
and [Ara 3D Gen exporter](https://github.com/TuriaArt/3d-gen-TU).

Design references for walkable rooms and hands-on guest service
[Motel Manager Simulator](https://store.steampowered.com/app/2594540/Motel_Manager_Simulator/)
and [Hotel Renovator](https://www.focus-entmt.com/en/games/hotel-renovator).

## Run a Unity Web build on localhost

1. Install the **Web Build Support** module for your Unity Editor through Unity Hub.
2. Open the project and select **Desert Lantern > Build Local Web Player**.
   This builds the enabled scenes into `Builds/Web` and uses uncompressed
   files and single-threaded execution for the local server.
3. From this Unity project folder in WSL, run `docker compose up -d` and open
   [http://localhost:8081](http://localhost:8081). Stop the Unity web server with
   `docker compose down`.

To build from a terminal with an activated Editor, run the following from this folder.

```bash
Unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget WebGL \
  -executeMethod DesertLantern.Editor.LocalWebBuild.Build -logFile -
```

For browser co-op setup, see [Join from a browser](NETWORKING.md#join-from-github-pages).

See [Unity’s web deployment guide](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html)
for details on build compression and server headers.
