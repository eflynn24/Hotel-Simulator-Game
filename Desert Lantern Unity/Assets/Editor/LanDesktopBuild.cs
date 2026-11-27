using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DesertLantern.Editor
{
    public static class LanDesktopBuild
    {
        [MenuItem("Desert Lantern/Build LAN Player")]
        public static void Build()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64 &&
                target != BuildTarget.StandaloneOSX)
                throw new BuildFailedException("Switch the active platform to a desktop target first.");

            string file = target == BuildTarget.StandaloneWindows64 ? "HotelSimulator.exe" :
                target == BuildTarget.StandaloneOSX ? "HotelSimulator.app" : "HotelSimulator.x86_64";
            string directory = "Builds/LAN/" + target;
            Directory.CreateDirectory(directory);
            BuildReport result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                target = target,
                locationPathName = Path.Combine(directory, file),
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray()
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Desktop build ended with {result.summary.result}.");
        }
    }
}
