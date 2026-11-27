# Hotel Simulator · Desert Lantern Motel

Run a rundown roadside motel. In the current 3D prototype, meet guests at the
front office, show them to one of four rooms, and explore the property in third
person.

## Play locally

Clone the repository and run the included Unity Web build with Docker Compose.

```sh
git clone https://github.com/eflynn24/Hotel-Simulator.git
cd Hotel-Simulator
docker compose up --build -d
```

Open [http://localhost:8080](http://localhost:8080). Stop the server with
`docker compose down`. To use another port in Bash, run
`MOTEL_PORT=8090 docker compose up --build -d`.

If you already have Python 3, you can serve the same build without Docker.

```sh
python3 -m http.server 8080 --bind 127.0.0.1 --directory docs
```

On Windows, use `py -3` in place of `python3` if needed.

## Controls

- Select **Desert Lantern Motel** on the county map.
- Use **WASD** or the **arrow keys** to walk, or click the pavement.
- Hold **Shift** to run and **right-drag** to orbit the camera.
- Approach the **Front Office** and press **E** to check in a guest. Lead
  them to the highlighted door and wait for them to catch up.
- Open **Menu** or press **Esc** for display settings.

## Open or rebuild in Unity

Add the [Desert Lantern Unity](Desert%20Lantern%20Unity) folder to Unity Hub.
The project uses Unity **6000.0.23f1**. In the Editor, choose
**Desert Lantern → Play County Map**.

Install the **Web Build Support** module to rebuild. Choose
**Desert Lantern → Build Local Web Player** and copy the resulting contents of
`Desert Lantern Unity/Builds/Web/` into `docs/` before rebuilding the Docker image.
Keep `docs/.nojekyll` for GitHub Pages.

See the [Unity project guide](Desert%20Lantern%20Unity/README.md) for development
instructions and character asset credits. The original terminal and HTML
prototypes are kept in `Game` and `dist/`.
