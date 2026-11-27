using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DesertLantern.Editor
{
    public static class LocalWebBuild
    {
        [MenuItem("Desert Lantern/Build Local Web Player")]
        public static void Build()
        {
            const string output = "Builds/Web";
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                target = BuildTarget.WebGL,
                locationPathName = output,
                scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray()
            };

            var compression = PlayerSettings.WebGL.compressionFormat;
            bool threads = PlayerSettings.WebGL.threadsSupport;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.threadsSupport = false;
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded ||
                    !File.Exists(Path.Combine(output, "index.html")))
                {
                    throw new BuildFailedException($"Local Web build ended with {report.summary.result}.");
                }
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.threadsSupport = threads;
            }
        }
    }
}
