using UnityEditor;
using UnityEditor.SceneManagement;

namespace DesertLantern.Editor
{
    internal static class CountyMapPlay
    {
        [MenuItem("Desert Lantern/Play County Map")]
        public static void OpenAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene("Assets/Scenes/CountyMap.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
