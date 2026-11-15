using UnityEngine;

namespace Mleczarka.Vehicles
{
    /// <summary>
    /// Pojazd na WheelColliderach: napęd, hamulec, skręt, zawieszenie z fizyki Unity.
    /// Wejście podaje kierowca (gracz albo w przyszłości AI / sieć) przez SetInput.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WheeledVehicle : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public WheelCollider collider;
            public Transform visual;
            public bool steer;
            public bool drive;
            [HideInInspector] public Quaternion visualOffset;
        }

        public Wheel[] wheels;
        public Transform cameraTarget;
        public Transform exitPoint;
        public Vector3 centerOfMass = new Vector3(0f, -0.4f, 0.1f);
        public float motorTorque = 1400f;
        public float brakeTorque = 3500f;
        public float maxSteerAngle = 32f;
        public float maxSpeedKmh = 38f;
        [Range(0f, 1f)] public float steerSpeedFalloff = 0.45f;

        public bool HasDriver { get; set; }
        public float SpeedKmh => _rb ? _rb.linearVelocity.magnitude * 3.6f : 0f;

        Rigidbody _rb;
        float _throttle, _steer;
        bool _handbrake;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMass;
            foreach (var w in wheels)
                if (w.visual) w.visualOffset = Quaternion.Inverse(transform.rotation) * w.visual.rotation;
        }

        /// <summary>Stawia pojazd na kołach w tym samym miejscu (po wywrotce albo utknięciu).</summary>
        public void ResetUpright()
        {
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            var pos = transform.position + Vector3.up * 1.5f;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < best) { best = hit.distance; pos = hit.point + Vector3.up * 1.2f; }
            _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
            _rb.position = pos; _rb.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            transform.SetPositionAndRotation(pos, _rb.rotation);
        }

        /// <summary>Przenosi pojazd w inne miejsce (szybki transport): bez prędkości, na kołach.</summary>
        public void PlaceAt(Vector3 pos, Quaternion rot)
        {
            if (!_rb) _rb = GetComponent<Rigidbody>();
            _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
            _rb.position = pos; _rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
        }

        public void SetInput(float throttle, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _handbrake = handbrake;
        }

        void FixedUpdate()
        {
            if (!HasDriver) { _throttle = 0f; _steer = 0f; _handbrake = true; }
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float speed01 = Mathf.Clamp01(Mathf.Abs(forwardSpeed) * 3.6f / maxSpeedKmh);
            float steerAngle = _steer * maxSteerAngle * (1f - steerSpeedFalloff * speed01);

            // Gaz przeciwny do kierunku jazdy = hamowanie, potem bieg wsteczny.
            bool braking = Mathf.Abs(forwardSpeed) > 0.5f && Mathf.Sign(forwardSpeed) != Mathf.Sign(_throttle) && Mathf.Abs(_throttle) > 0.1f;
            float torque = (braking || speed01 >= 1f) ? 0f : _throttle * motorTorque;

            foreach (var w in wheels)
            {
                if (w.steer) w.collider.steerAngle = steerAngle;
                w.collider.motorTorque = w.drive ? torque : 0f;
                float brake = braking ? brakeTorque : 0f;
                if (_handbrake) brake = brakeTorque * (w.drive ? 1f : 0.3f);
                else if (Mathf.Abs(_throttle) < 0.05f) brake = brakeTorque * 0.04f; // opór toczenia
                w.collider.brakeTorque = brake;
            }
        }

        void LateUpdate()
        {
            foreach (var w in wheels)
            {
                if (!w.visual) continue;
                w.collider.GetWorldPose(out var pos, out var rot);
                w.visual.position = pos;
                w.visual.rotation = rot * w.visualOffset;
            }
        }
    }
}
