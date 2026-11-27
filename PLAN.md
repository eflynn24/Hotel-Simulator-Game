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

## Multiplayer and management roadmap

Implement and verify one milestone at a time. The current milestone is
the shared Reservations tab in the reception computer. The first online
requests can be confirmed or closed before arrival. The next milestones
are physical supply delivery, a ringing reception phone for reservations,
cancellations, refunds and overbooking, and a shared day/night cycle.
Desktop hosts accept browser players over WebSocket, including from the
local HTTP copy.

1. **LAN co-op** — Host or join a motel by local address, move together, and
   share the existing office, guest escorts, and room occupancy. Verify joins,
   late joins, and disconnects before expanding the simulation.
2. **Reception and reservations** — Start with stays, check-in and checkout,
   room assignments, and keys. Add nightly rates and security deposits,
   reservations, luggage pickup from cars, then cancellations, refunds, and
   overbooking. Start with in-game online bookings before choosing any
   external service.
3. **Housekeeping** — Make beds and change linens, vacuum and mop floors,
   disinfect surfaces, clean bathrooms and toilets, collect garbage and
   forgotten belongings, and wash, dry, and fold laundry.
4. **Maintenance and repairs** — Fix plumbing and clogged toilets, electrical
   systems and lighting, air conditioners and televisions. Address water
   damage and mold, and replace equipment that cannot be repaired.
5. **Inventory and supplies** — Track towels, bedding, cleaning supplies, and
   toiletries. Compare supplier price and quality, place orders, receive and
   unload deliveries, and stock vending machines.
6. **Finances** — Add money and an accounting ledger. Pay utilities, internet,
   employee wages, insurance, and operating expenses. Add loans, demand-based
   pricing based on occupancy, season, and events, and reinvestment in expansion.
7. **Day and night** — Add a shared clock and vary customers and incidents at
   night.
8. **Food service** — Add breakfast, room service, bars, and restaurants.
9. **Guest requests** — Add requests for towels, pillows, repairs, and special
   services.

### Current service prototype

The current build adds a shared cash balance, a nightly charge, and the
first luggage run. Each guest parks in an assigned bay; after escorting
them to a room, collect their suitcase from that car and carry it to the
room's rack. The prototype tracks these transactions for the current
play session. The four ground-floor rooms and the reception are walkable.
