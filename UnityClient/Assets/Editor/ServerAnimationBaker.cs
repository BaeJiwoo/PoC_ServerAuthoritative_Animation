using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServerAuthoritativeAnimation.Client.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace ServerAuthoritativeAnimation.Client.Editor
{
    /// <summary>Offline authoring only. Runtime clients contain no Animator or animation graph.</summary>
    public static class ServerAnimationBaker
    {
        public const string ModelPath = "Assets/Imported/Bob/Bob_Rigged.fbx";
        public const string WalkPath = "Assets/Imported/Bob/Bob_Walk_Forward.fbx";
        public const string PrefabPath = "Assets/Resources/ServerCharacter.prefab";
        public const int SampleRate = 60;

        [MenuItem("POC/Bake Bob WalkForward for Server")]
        public static void BakeBob()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var clip = AssetDatabase.LoadAllAssetsAtPath(WalkPath).OfType<AnimationClip>()
                .FirstOrDefault(value => !value.name.StartsWith("__preview__"));
            if (model == null || clip == null)
                throw new InvalidOperationException("Bob model or walking FBX was not imported.");
            Bake(model, clip);
        }

        public static void Bake(GameObject model, AnimationClip clip)
        {
            if (EditorApplication.isPlaying || clip.length <= 0f)
                throw new InvalidOperationException("Bake a non-empty clip outside Play Mode.");

            var wrapper = new GameObject("ServerCharacter");
            var instance = Object.Instantiate(model, wrapper.transform, false);
            instance.name = "Bob_Visual";
            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (clip.humanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
            {
                Object.DestroyImmediate(wrapper);
                throw new InvalidOperationException("Humanoid clips require the model's valid Humanoid Avatar.");
            }

            PlayableGraph graph = default;
            try
            {
                var nodes = instance.GetComponentsInChildren<Transform>(true);
                var definitions = nodes.Select(node => new BoneDefinition
                {
                    path = AnimationUtility.CalculateTransformPath(node, wrapper.transform),
                    parent = Array.IndexOf(nodes, node.parent)
                }).ToArray();
                if (definitions.Select(b => b.path).Distinct().Count() != nodes.Length)
                    throw new InvalidOperationException("Bone paths must be unique.");

                var initial = Capture(nodes);
                // A single static standing pose: lower bind-pose arms without creating an Idle clip.
                Straighten(nodes, "LeftArm", "LeftForeArm");
                Straighten(nodes, "LeftForeArm", "LeftHand");
                Straighten(nodes, "RightArm", "RightForeArm");
                Straighten(nodes, "RightForeArm", "RightHand");
                var standing = Capture(nodes);
                Restore(nodes, initial);

                graph = PlayableGraph.Create("Offline FBX pose bake");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Offline sample", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                var frameCount = Mathf.Max(2, Mathf.CeilToInt(clip.length * SampleRate));
                var frames = new Frame[frameCount];
                ReferenceSample reference = null;
                for (var frame = 0; frame < frameCount; frame++)
                {
                    Restore(nodes, initial);
                    playable.SetTime(frame * (double)clip.length / frameCount);
                    graph.Evaluate(0f);
                    // World travel is exclusively controlled by the .NET movement simulation.
                    instance.transform.localPosition = initial[0].position;
                    instance.transform.localRotation = initial[0].rotation;
                    frames[frame] = new Frame { pose = Capture(nodes) };
                    if (frame == frameCount / 4)
                        reference = new ReferenceSample
                        {
                            phase = (float)frame / frameCount,
                            bones = nodes.Select((node, index) => new ReferenceBone
                            {
                                path = definitions[index].path,
                                position = node.position,
                                rotation = node.rotation
                            }).ToArray()
                        };
                }
                graph.Destroy();

                var clipData = new ClipData
                {
                    name = model.name + "/WalkForward",
                    duration = clip.length,
                    bones = definitions,
                    referencePose = standing,
                    frames = frames,
                    hitboxes = BuildHitboxes(nodes)
                };
                clipData.id = Hash128.Compute(JsonUtility.ToJson(clipData)).ToString();
                var projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
                var outputPath = Path.Combine(projectRoot, "assets", "animations", "BobWalkForward.json");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, JsonUtility.ToJson(clipData));
                File.WriteAllText(Path.ChangeExtension(outputPath, ".reference.json"), JsonUtility.ToJson(reference));

                Restore(nodes, standing);
                foreach (var component in instance.GetComponentsInChildren<Animator>(true))
                    Object.DestroyImmediate(component);
                foreach (var component in instance.GetComponentsInChildren<Animation>(true))
                    Object.DestroyImmediate(component);

                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.Refresh();
                const string materialPath = "Assets/Resources/BobWhite.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard")) { color = Color.white };
                    material.SetFloat("_Glossiness", 0.2f);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skinned)
                        skinned.updateWhenOffscreen = true;
                }
                wrapper.AddComponent<BakedCharacterIdentity>().skeletonId = clipData.id;
                PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("BAKED_ANIMATION name=" + clipData.name + " duration=" + clip.length +
                    " bones=" + nodes.Length + " frames=" + frameCount + " hitboxes=" + clipData.hitboxes.Length +
                    " id=" + clipData.id + " output=" + outputPath);
                LogMotionRange(nodes, frames);
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(wrapper);
            }
        }

        private static LocalPose[] Capture(Transform[] nodes)
        {
            return nodes.Select(node =>
            {
                var scale = node.localScale;
                if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > 0.0001f ||
                    Mathf.Abs(scale.x - scale.z) > 0.0001f)
                    throw new InvalidOperationException("Non-uniform bone scale is unsupported: " + node.name);
                return new LocalPose
                {
                    position = node.localPosition,
                    rotation = node.localRotation.normalized,
                    scale = scale
                };
            }).ToArray();
        }

        private static void Restore(Transform[] nodes, LocalPose[] pose)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i].localPosition = pose[i].position;
                nodes[i].localRotation = pose[i].rotation;
                nodes[i].localScale = pose[i].scale;
            }
        }

        private static int Find(Transform[] nodes, string suffix)
        {
            var index = Array.FindIndex(nodes, node =>
                string.Equals(node.name.Split(':').Last(), suffix, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new InvalidOperationException("Missing Mixamo bone: " + suffix);
            return index;
        }

        private static void Straighten(Transform[] nodes, string parentName, string childName)
        {
            var parent = nodes[Find(nodes, parentName)];
            var child = nodes[Find(nodes, childName)];
            parent.rotation = Quaternion.FromToRotation(child.position - parent.position, Vector3.down) * parent.rotation;
        }

        private static HitboxBinding[] BuildHitboxes(Transform[] nodes)
        {
            return new[]
            {
                new HitboxBinding { name = "Head", startBone = Find(nodes, "Head"),
                    offset = new Vector3(0, 0.18f, 0), size = new Vector3(0.42f, 0.40f, 0.40f) },
                Segment(nodes, "Torso", "Hips", "Neck", 0.34f, 0.24f),
                Segment(nodes, "LeftArm", "LeftArm", "LeftForeArm", 0.13f, 0.13f),
                Segment(nodes, "LeftForearm", "LeftForeArm", "LeftHand", 0.11f, 0.11f),
                Segment(nodes, "RightArm", "RightArm", "RightForeArm", 0.13f, 0.13f),
                Segment(nodes, "RightForearm", "RightForeArm", "RightHand", 0.11f, 0.11f),
                Segment(nodes, "LeftLeg", "LeftUpLeg", "LeftLeg", 0.18f, 0.18f),
                Segment(nodes, "LeftShin", "LeftLeg", "LeftFoot", 0.15f, 0.15f),
                Segment(nodes, "RightLeg", "RightUpLeg", "RightLeg", 0.18f, 0.18f),
                Segment(nodes, "RightShin", "RightLeg", "RightFoot", 0.15f, 0.15f)
            };
        }

        private static HitboxBinding Segment(Transform[] nodes, string name, string start, string end,
            float width, float depth)
        {
            return new HitboxBinding { name = name, startBone = Find(nodes, start), endBone = Find(nodes, end),
                width = width, depth = depth };
        }

        private static void LogMotionRange(Transform[] nodes, Frame[] frames)
        {
            foreach (var name in new[] { "Hips", "LeftArm", "LeftForeArm", "LeftLeg", "RightLeg" })
            {
                var index = Find(nodes, name);
                var rotation = frames[0].pose[index].rotation;
                var range = frames.Max(frame => Quaternion.Angle(rotation, frame.pose[index].rotation));
                Debug.Log("BAKED_MOTION " + name + " rotationRangeDegrees=" + range);
                if (name == "LeftLeg" && range < 1f)
                    throw new InvalidOperationException("The clip did not animate the leg. Check the Avatar/import settings.");
            }
        }

        [Serializable] private sealed class ClipData
        {
            public int schemaVersion = 1;
            public string id = "";
            public string name;
            public float duration;
            public BoneDefinition[] bones;
            public LocalPose[] referencePose;
            public Frame[] frames;
            public HitboxBinding[] hitboxes;
        }
        [Serializable] private sealed class BoneDefinition { public string path; public int parent; }
        [Serializable] private sealed class LocalPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable] private sealed class Frame { public LocalPose[] pose; }
        [Serializable] private sealed class HitboxBinding
        {
            public string name;
            public int startBone;
            public int endBone = -1;
            public float width = 0.15f;
            public float depth = 0.15f;
            public Vector3 offset;
            public Vector3 size;
        }
        [Serializable] private sealed class ReferenceSample { public float phase; public ReferenceBone[] bones; }
        [Serializable] private sealed class ReferenceBone
        {
            public string path;
            public Vector3 position;
            public Quaternion rotation;
        }
    }
}
