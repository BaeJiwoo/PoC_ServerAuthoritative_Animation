using System;
using System.Collections.Generic;
using ServerAuthoritativeAnimation.Client.Protocol;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Presentation
{
    /// <summary>
    /// A presentation-only view. It has no Animator and never advances animation locally.
    /// </summary>
    public sealed class SnapshotView : MonoBehaviour
    {
        private static readonly string[] RequiredHitboxes =
        {
            "Head",
            "Torso",
            "LeftArm",
            "RightArm",
            "LeftLeg",
            "RightLeg"
        };

        private static readonly Color[] HitboxColors =
        {
            new Color(1f, 0.82f, 0.25f),
            new Color(0.25f, 0.7f, 1f),
            new Color(0.3f, 1f, 0.55f),
            new Color(0.3f, 1f, 0.55f),
            new Color(1f, 0.4f, 0.4f),
            new Color(1f, 0.4f, 0.4f)
        };

        private readonly Dictionary<string, Transform> hitboxViews =
            new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Transform> modelBones = new Dictionary<string, Transform>();
        private readonly List<Material> extraMaterials = new List<Material>();
        private GameObject characterModel;
        private string skeletonId;
        private string poseStatus = "Waiting for server";
        private bool warnedMismatch;

        private Transform rootPose;
        private Material rootMaterial;
        private Material forwardMaterial;
        private Material[] hitboxMaterials;
        private double serverTime;
        private bool isMoving;
        private float animationPhase;

        public Transform RootPose
        {
            get { return rootPose; }
        }

        public double ServerTime
        {
            get { return serverTime; }
        }

        public bool IsMoving
        {
            get { return isMoving; }
        }

        public float AnimationPhase
        {
            get { return animationPhase; }
        }

        private void Awake()
        {
            BuildVisualization();
        }

        public void Apply(ServerSnapshotMessage snapshot)
        {
            ApplyInterpolated(snapshot, snapshot, 0);
        }

        public void ApplyInterpolated(ServerSnapshotMessage previous, ServerSnapshotMessage snapshot, float t)
        {
            t = Mathf.Clamp01(t);
            // Never blend unrelated rigs or different bone/hitbox layouts.
            if (!Compatible(previous, snapshot)) previous = snapshot;
            serverTime = previous.serverTime + (snapshot.serverTime - previous.serverTime) * t;
            isMoving = t < 1 ? previous.isMoving : snapshot.isMoving;
            var phaseDelta = snapshot.animationPhase - previous.animationPhase;
            if (phaseDelta < 0) phaseDelta += 1;
            animationPhase = Mathf.Repeat(previous.animationPhase + phaseDelta * t, 1);

            rootPose.SetPositionAndRotation(
                Vector3.Lerp(previous.root.position.ToUnityVector(), snapshot.root.position.ToUnityVector(), t),
                Quaternion.Slerp(previous.root.rotation.ToUnityQuaternion(), snapshot.root.rotation.ToUnityQuaternion(), t));
            ApplyBones(previous, snapshot, t);

            foreach (var existing in hitboxViews.Values)
                existing.gameObject.SetActive(false);

            if (snapshot.hitboxes == null)
            {
                return;
            }

            for (var index = 0; index < snapshot.hitboxes.Length; index++)
            {
                var hitbox = snapshot.hitboxes[index];
                var before = previous.hitboxes[index];
                Transform hitboxView;
                if (hitbox == null || string.IsNullOrEmpty(hitbox.name))
                {
                    continue;
                }
                if (!hitboxViews.TryGetValue(hitbox.name, out hitboxView))
                {
                    var color = hitbox.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0
                        ? HitboxColors[2] : HitboxColors[4];
                    var material = CreateMaterial(color);
                    extraMaterials.Add(material);
                    hitboxView = CreateHitbox(hitbox.name, transform.Find("ServerHitboxes"), material, color);
                    hitboxViews.Add(hitbox.name, hitboxView);
                }

                hitboxView.SetPositionAndRotation(
                    Vector3.Lerp(before.center.ToUnityVector(), hitbox.center.ToUnityVector(), t),
                    Quaternion.Slerp(before.rotation.ToUnityQuaternion(), hitbox.rotation.ToUnityQuaternion(), t));

                var size = Vector3.Lerp(before.size.ToUnityVector(), hitbox.size.ToUnityVector(), t);
                hitboxView.localScale = new Vector3(
                    Mathf.Max(0.001f, Mathf.Abs(size.x)),
                    Mathf.Max(0.001f, Mathf.Abs(size.y)),
                    Mathf.Max(0.001f, Mathf.Abs(size.z)));
                hitboxView.gameObject.SetActive(true);
            }
        }

        private static bool Compatible(ServerSnapshotMessage a, ServerSnapshotMessage b)
        {
            if (a.skeletonId != b.skeletonId || a.bones?.Length != b.bones?.Length ||
                a.hitboxes?.Length != b.hitboxes?.Length) return false;
            if (a.bones != null)
                for (var i = 0; i < a.bones.Length; i++)
                    if (a.bones[i] == null || b.bones[i] == null || a.bones[i].path != b.bones[i].path) return false;
            if (a.hitboxes != null)
                for (var i = 0; i < a.hitboxes.Length; i++)
                    if (a.hitboxes[i] == null || b.hitboxes[i] == null || a.hitboxes[i].name != b.hitboxes[i].name) return false;
            return true;
        }

        private void ApplyBones(ServerSnapshotMessage previous, ServerSnapshotMessage snapshot, float t)
        {
            if (characterModel == null) return;
            var valid = snapshot.skeletonId == skeletonId && snapshot.bones != null &&
                        snapshot.bones.Length == modelBones.Count;
            if (valid)
                foreach (var bone in snapshot.bones)
                    if (bone == null || !modelBones.ContainsKey(bone.path)) { valid = false; break; }

            characterModel.SetActive(valid);
            if (!valid)
            {
                poseStatus = "Rig mismatch: bake + restart server";
                if (!warnedMismatch)
                {
                    Debug.LogWarning("Server skeleton differs from ServerCharacter prefab. Re-bake and restart the server.");
                    warnedMismatch = true;
                }
                return;
            }
            for (var i = 0; i < snapshot.bones.Length; i++)
            {
                var bone = snapshot.bones[i];
                var before = previous.bones[i];
                var target = modelBones[bone.path];
                target.localPosition = Vector3.Lerp(before.localPosition.ToUnityVector(), bone.localPosition.ToUnityVector(), t);
                target.localRotation = Quaternion.Slerp(before.localRotation.ToUnityQuaternion(), bone.localRotation.ToUnityQuaternion(), t);
                target.localScale = Vector3.Lerp(before.localScale.ToUnityVector(), bone.localScale.ToUnityVector(), t);
            }
            poseStatus = "Bob | " + snapshot.bones.Length + " server transforms";
            warnedMismatch = false;
        }

        private void MapBones(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
            {
                var path = prefix.Length == 0 ? child.name : prefix + "/" + child.name;
                modelBones.Add(path, child);
                MapBones(child, path);
            }
        }

        private void BuildVisualization()
        {
            rootPose = new GameObject("CharacterRoot").transform;
            rootPose.SetParent(transform, false);
            var prefab = Resources.Load<GameObject>("ServerCharacter");
            if (prefab != null)
            {
                characterModel = Instantiate(prefab, rootPose, false);
                skeletonId = characterModel.GetComponent<BakedCharacterIdentity>().skeletonId;
                MapBones(characterModel.transform, "");
            }

            rootMaterial = CreateMaterial(new Color(1f, 1f, 1f));
            var rootMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rootMarker.name = "RootMarker";
            rootMarker.transform.SetParent(rootPose, false);
            rootMarker.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            rootMarker.transform.localScale = new Vector3(0.22f, 0.04f, 0.22f);
            rootMarker.GetComponent<Renderer>().sharedMaterial = rootMaterial;
            Destroy(rootMarker.GetComponent<Collider>());

            forwardMaterial = CreateLineMaterial(new Color(1f, 0.2f, 0.2f));
            var forwardObject = new GameObject("Forward(+Z)");
            forwardObject.transform.SetParent(rootPose, false);
            var forwardLine = forwardObject.AddComponent<LineRenderer>();
            forwardLine.useWorldSpace = false;
            forwardLine.positionCount = 3;
            forwardLine.SetPosition(0, new Vector3(0f, 0.09f, 0f));
            forwardLine.SetPosition(1, new Vector3(0f, 0.09f, 0.85f));
            forwardLine.SetPosition(2, new Vector3(0f, 0.09f, 1.25f));
            forwardLine.startWidth = 0.055f;
            forwardLine.endWidth = 0.015f;
            forwardLine.sharedMaterial = forwardMaterial;

            var hitboxContainer = new GameObject("ServerHitboxes").transform;
            hitboxContainer.SetParent(transform, false);
            hitboxMaterials = new Material[RequiredHitboxes.Length];

            for (var index = 0; index < RequiredHitboxes.Length; index++)
            {
                hitboxMaterials[index] = CreateMaterial(HitboxColors[index]);
                hitboxViews.Add(RequiredHitboxes[index], CreateHitbox(
                    RequiredHitboxes[index], hitboxContainer, hitboxMaterials[index], HitboxColors[index]));
            }
        }

        private Transform CreateHitbox(string name, Transform parent, Material material, Color color)
        {
            var hitbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hitbox.name = name;
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localScale = Vector3.one * 0.25f;
            hitbox.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(hitbox.GetComponent<Collider>());
            if (characterModel != null)
            {
                hitbox.GetComponent<Renderer>().enabled = false;
                var line = hitbox.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                var corners = new[]
                {
                    new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
                    new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f),
                    new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f),
                    new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
                };
                var edges = new[] { 0,1,2,3,0,4,5,1,5,6,2,6,7,3,7,4 };
                line.positionCount = edges.Length;
                for (var i = 0; i < edges.Length; i++) line.SetPosition(i, corners[edges[i]]);
                line.startWidth = line.endWidth = 0.012f;
                var wireMaterial = CreateLineMaterial(color);
                extraMaterials.Add(wireMaterial);
                line.sharedMaterial = wireMaterial;
            }
            return hitbox.transform;
        }

        private static Material CreateMaterial(Color color)
        {
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
            material.color = color;
            return material;
        }

        private static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12f, 12f, 300f, 118f), "Server snapshot");
            GUI.Label(new Rect(24f, 36f, 220f, 20f), "Moving: " + isMoving);
            GUI.Label(new Rect(24f, 56f, 220f, 20f), "Walk phase: " + animationPhase.ToString("0.000"));
            GUI.Label(new Rect(24f, 78f, 280f, 20f), poseStatus);
            GUI.Label(new Rect(24f, 100f, 280f, 20f), "Interpolation: 100 ms | Lerp / Slerp");
        }

        private void OnDestroy()
        {
            Destroy(rootMaterial);
            Destroy(forwardMaterial);
            foreach (var material in extraMaterials) Destroy(material);
            if (hitboxMaterials == null)
            {
                return;
            }

            for (var index = 0; index < hitboxMaterials.Length; index++)
            {
                Destroy(hitboxMaterials[index]);
            }
        }
    }
}

