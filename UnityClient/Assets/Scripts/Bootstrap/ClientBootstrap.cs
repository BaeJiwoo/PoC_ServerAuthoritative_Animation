using System;
using ServerAuthoritativeAnimation.Client.Networking;
using ServerAuthoritativeAnimation.Client.Presentation;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Bootstrap
{
    public static class ClientBootstrap
    {
        private const string DefaultServerUrl = "ws://127.0.0.1:5000/ws";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntime()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ServerWebSocketClient>() != null)
            {
                return;
            }

            var runtime = new GameObject("ServerAuthoritativeClient");
            var view = runtime.AddComponent<SnapshotView>();
            var networkClient = runtime.AddComponent<ServerWebSocketClient>();
            networkClient.Configure(ResolveServerUrl(), view);

            EnsureCamera(view);
            EnsureLight();
            EnsureGround();
        }

        private static string ResolveServerUrl()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], "--server-url", StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            var environmentUrl = Environment.GetEnvironmentVariable("SAA_SERVER_URL");
            return string.IsNullOrWhiteSpace(environmentUrl) ? DefaultServerUrl : environmentUrl;
        }

        private static void EnsureCamera(SnapshotView view)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 42f;
            camera.transform.position = new Vector3(6f, 5f, -7f);
            var follower = camera.GetComponent<SnapshotCameraFollow>();
            if (follower == null)
            {
                follower = camera.gameObject.AddComponent<SnapshotCameraFollow>();
            }

            follower.Target = view.RootPose;
        }

        private static void EnsureLight()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
        }

        private static void EnsureGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var renderer = ground.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader);
            material.color = new Color(0.18f, 0.2f, 0.22f);
            renderer.sharedMaterial = material;
        }
    }

    public sealed class SnapshotCameraFollow : MonoBehaviour
    {
        private static readonly Vector3 Offset = new Vector3(3.6f, 2.6f, -4.4f);

        public Transform Target { get; set; }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            transform.position = Target.position + Offset;
            transform.LookAt(Target.position + Vector3.up * 1.2f);
        }
    }
}

