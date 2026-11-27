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

## Join from GitHub Pages

On the motel's Wi-Fi network, open the
[GitHub Pages game](https://eflynn24.github.io/Hotel-Simulator-Game/) in current
Chrome and click **Join LAN**. The published build uses the configured
Windows Wi-Fi address and TCP port `7778`. The headless host starts when
its Windows user signs in and leaves all four slots available to players. Allow local
network access if Chrome asks. If you previously denied it, change the
site's permission in your browser settings and try again.

The published address is `PublishedLanAddress` in
`Assets/Scripts/PropertyView.Lan.cs`. If Windows gets a different Wi-Fi
address, update that setting and republish the Web build, or use
**LAN settings** to enter the current address. A router DHCP reservation
can keep the address stable.

For a different host, use **LAN settings** and enter its private IPv4
address and TCP port. For browsers that require an encrypted connection
from HTTPS, enable **Secure WebSocket (WSS)** and enter the host's DNS
name and TLS proxy port (default `443`). The browser must trust the
certificate. Local network permission can still be required.

For WSS, use a DNS name that points to the proxy on your LAN and a TLS
certificate for that name. The proxy must forward WebSocket upgrades at
`/` to the Unity browser host. For example, with Caddy on the same machine
as the Unity host and a certificate trusted by the players' browsers:

```caddyfile
motel.example.net {
    tls /path/to/fullchain.pem /path/to/private-key.pem
    reverse_proxy 127.0.0.1:7778
}
```

Keep the host and proxy reachable from the LAN. GitHub Pages does not
run the host process.

## Join from a local HTTP copy

On the motel's Wi-Fi, open `http://192.168.4.43:8082/` and choose
**Join LAN** on the County Map. This serves the game from the Windows host
and joins the same multiplayer session as GitHub Pages. Players can use
this link if their browser blocks requests from GitHub Pages to the LAN.
The Windows web server and motel host start at Windows sign-in.

To serve a local HTTP copy on a different host:

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

A desktop host occupies one player slot. A headless host runs the session without an avatar and leaves all four slots open.
If the host is in WSL NAT mode, use the native desktop host for other devices
or configure the WSL networking so the TCP ports are reachable from the LAN.
The local HTTP copy also supports direct WebSocket connections on browsers
that disallow it from GitHub Pages.

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
ownership, supply orders, crate ownership, and supply stock. `LanWorldState` is a registered
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
balance, stock, and occupied room. Protocol version 8 includes shared housekeeping tasks, laundry,
room availability, and supply inventory alongside the clock, stays, and guests, so everyone in a session needs the same build.

Two Chromium clients joined the same Linux WebSocket host from the local HTTP
County Map. Both showed the three connected avatars, movement from one browser
appeared in the other, and both returned to the County Map after host shutdown.
This test ran on one machine.

Chromium also joined the local host directly from the live GitHub Pages origin
using both loopback and private IPv4 after local network permission was granted.
