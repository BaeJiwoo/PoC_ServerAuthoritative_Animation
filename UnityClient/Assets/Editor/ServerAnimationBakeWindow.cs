using UnityEditor;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Editor
{
    public sealed class ServerAnimationBakeWindow : EditorWindow
    {
        private GameObject model;
        private AnimationClip walk;

        [MenuItem("POC/Bake Imported Walk Clip...")]
        public static void Open()
        {
            GetWindow<ServerAnimationBakeWindow>("Server Walk Bake");
        }

        private void OnEnable()
        {
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ServerAnimationBaker.ModelPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Select a Mixamo rig and one looping walk clip. Humanoid clips use the model's " +
                "Avatar during this offline bake. This replaces the server walk data and display prefab. " +
                "Restart the server after baking.", MessageType.Info);
            model = (GameObject)EditorGUILayout.ObjectField("Character model", model, typeof(GameObject), false);
            walk = (AnimationClip)EditorGUILayout.ObjectField("WalkForward clip", walk, typeof(AnimationClip), false);
            using (new EditorGUI.DisabledScope(model == null || walk == null || EditorApplication.isPlaying))
                if (GUILayout.Button("Bake full skeleton at 60 Hz"))
                    ServerAnimationBaker.Bake(model, walk);
        }
    }
}
