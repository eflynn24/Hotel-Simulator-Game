using System;
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

                PrepareLaunchPage(Path.Combine(output, "index.html"));
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.threadsSupport = threads;
            }
        }

        private static void PrepareLaunchPage(string path)
        {
            // GitHub Pages may serve index.html and the player files from independent caches.
            string revision = Guid.NewGuid().ToString("N");
            string html = File.ReadAllText(path);
            foreach (string name in new[] { "Web.loader.js", "Web.data", "Web.framework.js", "Web.wasm" })
                html = html.Replace($"/{name}\"", $"/{name}?v={revision}\"");

            const string showLaunchFailure = @"function showLaunchFailure(error) {
        console.error(error);
        document.querySelector('#unity-loading-bar').style.display = 'none';
        const banner = document.querySelector('#unity-warning');
        banner.replaceChildren();
        const message = document.createElement('div');
        message.style.cssText = 'background: #251810; color: white; padding: 16px;';
        message.textContent = 'The game could not load. ';
        const retry = document.createElement('button');
        retry.textContent = 'Retry';
        retry.onclick = () => {
          const url = new URL(location.href);
          url.searchParams.set('retry', Date.now());
          location.replace(url);
        };
        message.appendChild(retry);
        banner.appendChild(message);
        banner.style.display = 'block';
      }";
            html = html.Replace("var script = document.createElement(\"script\");",
                showLaunchFailure + "\n      var script = document.createElement(\"script\");");
            html = html.Replace("script.src = loaderUrl;",
                "script.src = loaderUrl;\n      script.onerror = showLaunchFailure;");
            html = html.Replace("alert(message);", "showLaunchFailure(message);");
            File.WriteAllText(path, html);
        }
    }
}
