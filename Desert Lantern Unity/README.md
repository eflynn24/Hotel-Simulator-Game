# Desert Lantern Motel in Unity

This Unity 6 prototype starts over a 3D view of the county. Select
**Desert Lantern Motel** to take control of the caretaker in third person.

- Walk with **WASD** or **arrow keys**, hold **Shift** to run, and drag with
  the **right mouse button** to orbit the camera. You can also click a spot
  in the parking lot to walk to it.
- Visit the **Front Office**, which you can walk inside. Press **E** at the
  entrance or use the office marker to check in the waiting guest. Walk to
  the highlighted door and give your guest time to follow.
- Inspect a nearby door with **E** between escorts. **Menu** or **Escape**
  opens the display settings. **Back to map** returns to the county view.

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
their skeletons while a capsule controller handles collisions. Room
interiors are the next planned milestone.

[MakeHuman asset license](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.md)
and [Ara 3D Gen exporter](https://github.com/TuriaArt/3d-gen-TU).

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

See [Unity’s web deployment guide](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html)
for details on build compression and server headers.
