using System.Collections.Generic;
using UnityEngine;

namespace NightWarden.Player
{
    /// <summary>
    /// Realistic bone-chain spring-damper physics solver for the Night Warden's tactical cape.
    /// Provides fluid movement, inertial drag, gravity weighting, wind turbulence,
    /// and body-collision clamping without the instability or overhead of standard Cloth.
    /// </summary>
    public class CapeSway : MonoBehaviour
    {
        [Header("Bone Chain Setup")]
        [Tooltip("Ordered list of cape segment bones from upper back/shoulders down to calf tip.")]
        [SerializeField] private List<Transform> capeSegments = new List<Transform>();

        [Header("Spring & Damping Dynamics")]
        [Range(1f, 40f)]
        [SerializeField] private float stiffness = 16f;

        [Range(0.5f, 20f)]
        [SerializeField] private float damping = 7.5f;

        [Range(0f, 2f)]
        [SerializeField] private float airResistance = 0.85f;

        [Header("Wind & Aerodynamics")]
        [SerializeField] private float windFlutterIntensity = 0.08f;
        [SerializeField] private float windFlutterSpeed = 4.5f;
        [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);

        [Header("Collision Clamping")]
        [Tooltip("Prevents the cape from clipping into the character's back or legs.")]
        [SerializeField] private Transform characterTransform;
        [SerializeField] private float forwardAngleLimit = 25f; // Max swing forward
        [SerializeField] private float backwardAngleLimit = 85f; // Max rise when running fast

        private class SegmentState
        {
            public Transform Transform;
            public Quaternion RestLocalRotation;
            public Vector3 CurrentVelocity;
            public Vector3 CurrentTipPosition;
            public float Length;
        }

        private List<SegmentState> _states = new List<SegmentState>();
        private Vector3 _lastParentPosition;
        private Quaternion _lastParentRotation;

        private void Start()
        {
            if (characterTransform == null)
            {
                characterTransform = transform.root;
            }

            InitializeChain();
            _lastParentPosition = transform.position;
            _lastParentRotation = transform.rotation;
        }

        public void SetCapeSegments(List<Transform> segments)
        {
            capeSegments = segments;
            InitializeChain();
        }

        private void InitializeChain()
        {
            _states.Clear();
            if (capeSegments == null || capeSegments.Count == 0) return;

            for (int i = 0; i < capeSegments.Count; i++)
            {
                var seg = capeSegments[i];
                if (seg == null) continue;

                float length = 0.35f;
                if (i < capeSegments.Count - 1 && capeSegments[i + 1] != null)
                {
                    length = Vector3.Distance(seg.position, capeSegments[i + 1].position);
                }

                _states.Add(new SegmentState
                {
                    Transform = seg,
                    RestLocalRotation = seg.localRotation,
                    CurrentVelocity = Vector3.zero,
                    CurrentTipPosition = seg.position - seg.up * length,
                    Length = Mathf.Max(0.1f, length)
                });
            }
        }

        private void LateUpdate()
        {
            if (_states.Count == 0) return;

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            Vector3 parentVelocity = (transform.position - _lastParentPosition) / dt;

            // Ambient aerodynamic wind flutter
            float windNoise = Mathf.Sin(Time.time * windFlutterSpeed) * Mathf.Cos(Time.time * windFlutterSpeed * 1.3f);
            Vector3 windFlutter = transform.right * (windNoise * windFlutterIntensity);

            for (int i = 0; i < _states.Count; i++)
            {
                var s = _states[i];
                if (s.Transform == null) continue;

                // Segment target orientation pointing downward under gravity + drag
                Vector3 desiredDirection = (gravity.normalized + windFlutter - parentVelocity * (airResistance * 0.04f)).normalized;

                // Spring force toward target direction
                Vector3 currentDirection = -s.Transform.up;
                Vector3 force = (desiredDirection - currentDirection) * stiffness;

                // Damping
                s.CurrentVelocity += force * dt;
                s.CurrentVelocity -= s.CurrentVelocity * (damping * dt);

                // Update direction
                Vector3 newDir = (currentDirection + s.CurrentVelocity * dt).normalized;

                // Convert to rotation relative to parent
                Quaternion targetWorldRot = Quaternion.FromToRotation(Vector3.down, newDir);
                Quaternion localRot = s.Transform.parent != null
                    ? Quaternion.Inverse(s.Transform.parent.rotation) * targetWorldRot
                    : targetWorldRot;

                // Clamp angles to prevent clipping through character legs/torso
                Vector3 euler = localRot.eulerAngles;
                float pitch = NormalizeAngle(euler.x);
                pitch = Mathf.Clamp(pitch, -forwardAngleLimit, backwardAngleLimit);
                euler.x = pitch;

                s.Transform.localRotation = Quaternion.Euler(euler);
            }

            _lastParentPosition = transform.position;
            _lastParentRotation = transform.rotation;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
