# Motel simulator development plan

1. Build a playable daily loop. Check reservations and walk-ins into ready rooms, close the day, collect room revenue, and prepare rooms for the next guests.
2. Add operational choices. Balance cleaning, repairs, pest control, incident paperwork, and advertising against cash and daily work hours.
3. Connect progression to operations. Earn guest satisfaction, renovate rooms, and qualify for a luxury resort rating.
4. In a later iteration, expand guest stories, multi-night bookings, staffing, and resort amenities. Use playtesting to tune demand, prices, and workload.

The first version is a single-player terminal game with a local save. Run `python3 Game` from this folder. Use `python3 Game --new` to start a fresh game without loading the save. Saving afterward replaces the existing save.

## Browser prototype on WSL

From this folder, run `docker compose up --build -d` and open
[http://localhost:8080](http://localhost:8080). Select the motel marker on the
county map to visit the exterior. The browser prototype covers these two
scenes. The terminal version still runs with `python3 Game`.

Use `MOTEL_PORT=8081 docker compose up --build -d` if port 8080 is occupied.
Stop the container with `docker compose down`.

## Unity prototype

Open [Desert Lantern Unity](Desert%20Lantern%20Unity/README.md) in Unity 6.
The first Unity milestone ports the county map and the motel exterior as two
playable scenes. Build the front desk and port the management loop after
the exterior navigation is tested in the editor.
