#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ServerAuthoritativeAnimation.Client.Editor
{
    public static class PocLauncher
    {
        [MenuItem("POC/Run Server-Authoritative Demo")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
    }
}
#endif
