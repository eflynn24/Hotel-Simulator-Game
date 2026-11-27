# LAN co-op

The desktop build supports up to four players on one local network. One player
hosts and the others join by IPv4 address. Leaving as the host ends the session.
Browser players can join a WebSocket host. Desktop-only sessions use UDP.

## Host and join

1. Start the desktop game or enter Play Mode from the county map.
2. On the host, select **LAN co-op** in the upper-left corner of the County Map,
   then **Host motel**. The default is UDP
   port `7777`. The host view shows an address to share with the other players.
3. On each joining machine, select **LAN co-op**, enter the host's address and
   port, and choose **Join motel**. A successful connection opens the motel
   automatically.
4. Move with the usual controls. Any player at the front office can check in
   the waiting guest. That player's route guides the guest. Everyone sees the
   same occupancy count. If the guide disconnects, the guest returns to the
   office and another player can take over.

Allow the game through the host's firewall on the local network if prompted.
For a first test on one computer, use `127.0.0.1` in a second instance.
For two separate computers, use the address of the host's LAN adapter. A
native desktop player avoids inbound routing limitations of WSL's default
NAT mode.

## Join from a browser

1. On the host's desktop build, open **County Map → LAN co-op**, check
   **Allow browser players (WebSocket)**, then choose **Host motel**. The default
   WebSocket port is TCP `7778`.
2. Serve the Web build over HTTP on the local network. From the Unity project
   directory, run `UNITY_WEB_BIND=0.0.0.0 docker compose up -d`. Players on
   another device can open `http://HOST_IP:8081` if the host exposes the HTTP
   and WebSocket ports on its LAN interface.
3. In the browser, select **County Map → LAN co-op**, enter the host's
   IPv4 address and TCP port, then choose **Join motel**. Use `127.0.0.1`
   when the browser and host are on the same computer. Desktop players can
   also join that session with **Allow browser players (WebSocket)** checked.

For a host on Linux without a game window, run the desktop executable
from its extracted directory:

```sh
./HotelSimulator.x86_64 -batchmode -nographics -motelBrowserHost 7778
```

The host occupies one of the four slots, including when it runs without a window.
If the host is in WSL NAT mode, use the native desktop host for other devices
or configure the WSL networking so the TCP ports are reachable from the LAN.
Use the HTTP copy for browser LAN. The HTTPS GitHub Pages site cannot use this
plain WebSocket endpoint.

## Build

In Unity 6000.0.23f1, switch the active platform to desktop, then choose
**Desert Lantern → Build LAN Player**. Output is in `Builds/LAN/`. Keep the
entire output folder together when copying it to other players.

## Replication

- `ProjectSettings/ProjectVersion.txt` declares `m_EditorVersion: 6000.0.23f1`.
- `Packages/manifest.json` declares `"com.unity.netcode.gameobjects": "2.7.0"`.
- `Packages/packages-lock.json` resolves `com.unity.transport` to `"version": "2.6.0"`.

The game uses GameObjects and a listen server, with direct Unity Transport
connections. The host allocates four player slots and owns avatar movement,
collision, active escorts, room occupancy, cash, door state, suitcase
ownership, and supply stock. `LanWorldState` is a registered
NetworkObject. Its player list and motel NetworkVariable are the initial
state for late joiners and current state for connected clients.

`InputInterval` sends input at 30 Hz and `InputTimeout` stops remote keyboard
movement after 300 ms without input. `SnapshotInterval` is 15 Hz. Clients
smooth incoming poses locally, and derive animation from replicated speed.
The host validates destination bounds and reception distance on action RPCs.
The connection uses UDP directly, without account or cloud service setup.

References

- [Unity Netcode for GameObjects 2.7](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/index.html)
- [Connection approval](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/basics/connection-approval.html)
- [Unity Transport 2.6](https://docs.unity3d.com/Packages/com.unity.transport@2.6/manual/index.html)

## Verification

The LAN integration was exercised with separate Linux player processes over
the WSL network adapter. A joining client moved to reception, escorted a
guest, and both players reported occupancy of one room. The host removed the
leaving client, a later client received the occupied room on join, and that
client returned to the county map when the host stopped. The same sequence
passed with 27–57 ms of artificial delay per direction and 2% packet loss on
the initial client's connection.

Both Windows and Linux desktop player builds were exported. The network
tests used one machine; a test between separate physical devices has not
been run.

The service loop was also exercised across separate player processes.
Both peers saw the room charge raise cash from $230 to $295 and the luggage
delivery raise it to $305. The host rejected a distant pickup and repeated delivery. A client then
bought four towels for $18 at the reception computer. Both players saw the
new $287 balance and four towels in stock. A late joiner received that
balance, stock, and occupied room. Protocol version 3 includes service state
and supply inventory, so everyone in a session needs the same build.

Two Chromium clients joined the same Linux WebSocket host from the local HTTP
County Map. Both showed the three connected avatars, movement from one browser
appeared in the other, and both returned to the County Map after host shutdown.
This test ran on one machine.
