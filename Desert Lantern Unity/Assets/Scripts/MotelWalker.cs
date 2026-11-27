using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace DesertLantern
{
    internal sealed class MotelWalker : IDisposable
    {
        public const float WalkSpeed = 2.15f;
        public const float RunSpeed = 4.25f;
        private const float Acceleration = 9f;
        private readonly Transform root;
        private readonly AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable walk;
        private readonly AnimationClipPlayable run;
        private PlayableGraph graph;
        private Vector3 velocity;
        private float fallSpeed;

        public Vector3 Position => root.position;
        public CharacterController Capsule { get; }

        public MotelWalker(Transform parent, string asset, Vector3 position)
        {
            root = new GameObject(asset).transform;
            root.SetParent(parent, false);
            root.position = position;
            Capsule = root.gameObject.AddComponent<CharacterController>();
            Capsule.height = 1.81f;
            Capsule.radius = .28f;
            Capsule.center = Vector3.up * .905f;
            Capsule.stepOffset = .24f;
            Capsule.minMoveDistance = 0;

            string folder = $"Characters/{asset}/{asset}";
            GameObject prefab = Resources.Load<GameObject>(folder);
            if (prefab == null)
                throw new InvalidOperationException($"Character model is missing at Resources/{folder}.");
            GameObject model = Object.Instantiate(prefab, root);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(folder);
            graph = PlayableGraph.Create(asset + " locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            AnimationClipPlayable idle = Clip(clips, "Idle");
            walk = Clip(clips, "Walk");
            run = Clip(clips, "Run");
            graph.Connect(idle, 0, mixer, 0);
            graph.Connect(walk, 0, mixer, 1);
            graph.Connect(run, 0, mixer, 2);
            mixer.SetInputWeight(0, 1);
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(mixer);
            graph.Play();
        }

        private AnimationClipPlayable Clip(AnimationClip[] clips, string name)
        {
            AnimationClip source = Array.Find(clips, clip =>
                !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                (clip.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                 clip.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase)));
            if (source == null)
                throw new InvalidOperationException($"{root.name} is missing its {name} clip.");
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, source);
            playable.SetApplyFootIK(false);
            return playable;
        }

        public void Move(Vector3 desiredVelocity, float delta)
        {
            if (delta <= 0)
                return;

            velocity = Vector3.MoveTowards(velocity, desiredVelocity, Acceleration * delta);
            Vector3 start = root.position;
            fallSpeed = Capsule.isGrounded ? -2f : Mathf.Max(-15f, fallSpeed - 24f * delta);

            Vector3 motion = velocity * delta;
            Vector3 goal = start + motion;
            goal.x = Mathf.Clamp(goal.x, -16.5f, 16.5f);
            goal.z = Mathf.Clamp(goal.z, -13.5f, 9.8f);
            motion = goal - start;
            motion.y = fallSpeed * delta;
            Capsule.Move(motion);

            velocity = root.position - start;
            velocity.y = 0;
            velocity /= delta;
            float speed = velocity.magnitude;
            if (speed > .045f)
            {
                Quaternion heading = Quaternion.LookRotation(velocity, Vector3.up);
                root.rotation = Quaternion.RotateTowards(root.rotation, heading, 650f * delta);
            }

            float moving = Mathf.Clamp01(speed / WalkSpeed);
            float sprint = Mathf.InverseLerp(WalkSpeed, RunSpeed, speed);
            mixer.SetInputWeight(0, 1 - moving);
            mixer.SetInputWeight(1, moving * (1 - sprint));
            mixer.SetInputWeight(2, moving * sprint);
            walk.SetSpeed(Mathf.Lerp(.78f, 1.35f, moving));
            run.SetSpeed(Mathf.Lerp(.8f, 1.28f, sprint));
        }

        public void SetPaused(bool paused)
        {
            graph.GetRootPlayable(0).SetSpeed(paused ? 0 : 1);
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
            if (root != null)
                Object.Destroy(root.gameObject);
        }

        public static Vector3 Toward(Vector3 from, Vector3 target, float maxSpeed)
        {
            Vector3 offset = target - from;
            offset.y = 0;
            float distance = offset.magnitude;
            if (distance < .045f)
                return Vector3.zero;
            return offset / distance * Mathf.Min(maxSpeed, Mathf.Sqrt(2 * Acceleration * distance));
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            Vector3 difference = a - b;
            difference.y = 0;
            return difference.magnitude;
        }
    }
}
