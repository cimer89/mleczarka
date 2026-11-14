using Mleczarka.Vehicles;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mleczarka.Player
{
    /// <summary>
    /// Sterowanie postacią w trzeciej osobie: ruch względem kamery, bieg, skok,
    /// obrót kamery (Cinemachine Orbital Follow), wsiadanie do pojazdu (Interact).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FarmerController : MonoBehaviour
    {
        [Header("Wejście")]
        public InputActionAsset actions;

        [Header("Ruch")]
        public float walkSpeed = 2.0f;
        public float runSpeed = 4.2f;
        public float sprintSpeed = 6.5f;
        public float acceleration = 10f;
        public float turnSpeed = 720f;
        public float jumpHeight = 1.0f;
        public float gravity = -20f;

        [Header("Kamera")]
        public CinemachineCamera cinemachineCamera;
        public CinemachineOrbitalFollow orbital;
        public Transform cameraTarget;
        public float lookSensitivityMouse = 0.12f;
        public float lookSensitivityStick = 140f;
        public float cameraRadiusOnFoot = 5.5f;
        public float cameraRadiusInVehicle = 9f;
        public float garageCameraRadius = 11f;

        [Header("Pojazdy")]
        public float enterDistance = 3.5f;

        CharacterController _cc;
        Animator _anim;
        Renderer[] _renderers;
        InputAction _move, _look, _sprint, _jump, _interact;
        Vector3 _velocity;
        float _speed;
        WheeledVehicle _vehicle;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _anim = GetComponentInChildren<Animator>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            var map = actions.FindActionMap("Player", true);
            _move = map.FindAction("Move", true);
            _look = map.FindAction("Look", true);
            _sprint = map.FindAction("Sprint", true);
            _jump = map.FindAction("Jump", true);
            _interact = map.FindAction("Interact", true);
        }

        void OnEnable() => actions.FindActionMap("Player", true).Enable();
        void OnDisable() => actions.FindActionMap("Player", true).Disable();

        /// <summary>Blokada sterowania, gdy otwarty jest ekran (np. garaż). Kamera powoli krąży wokół celu.</summary>
        public bool InputBlocked { get; set; }
        [System.NonSerialized] public bool orbitWhenBlocked = true;
        public WheeledVehicle CurrentVehicle => _vehicle;
        public void FocusCamera(Transform t) => Follow(t);
        public void RestoreCamera() => Follow(_vehicle ? (_vehicle.cameraTarget ? _vehicle.cameraTarget : _vehicle.transform) : cameraTarget);

        void Update()
        {
            if (InputBlocked)
            {
                if (orbital && orbitWhenBlocked)
                {
                    orbital.HorizontalAxis.Value += 12f * Time.deltaTime;
                    orbital.Radius = Mathf.Lerp(orbital.Radius, garageCameraRadius, Time.deltaTime * 3f);
                    orbital.VerticalAxis.Value = Mathf.Lerp(orbital.VerticalAxis.Value, 18f, Time.deltaTime * 2f);
                }
                if (_vehicle) _vehicle.SetInput(0f, 0f, true);
                else if (_anim) _anim.SetFloat(SpeedId, 0f, 0.1f, Time.deltaTime);
                return;
            }
            UpdateCursor();
            UpdateCamera();
            if (_interact.WasPressedThisFrame()) ToggleVehicle();
            if (_vehicle && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) _vehicle.ResetUpright();
            if (_vehicle) DriveVehicle();
            else Walk();
        }

        void UpdateCursor()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
        }

        void UpdateCamera()
        {
            if (!orbital) return;
            Vector2 look = _look.ReadValue<Vector2>();
            bool isMouse = _look.activeControl != null && _look.activeControl.device is Mouse;
            if (isMouse && Cursor.lockState != CursorLockMode.Locked) look = Vector2.zero;
            float k = isMouse ? lookSensitivityMouse : lookSensitivityStick * Time.deltaTime;
            orbital.HorizontalAxis.Value += look.x * k;
            orbital.VerticalAxis.Value = Mathf.Clamp(orbital.VerticalAxis.Value - look.y * k, -5f, 60f);
            orbital.Radius = Mathf.Lerp(orbital.Radius, _vehicle ? cameraRadiusInVehicle : cameraRadiusOnFoot, Time.deltaTime * 3f);
        }

        void Walk()
        {
            Vector2 input = _move.ReadValue<Vector2>();
            Transform cam = Camera.main ? Camera.main.transform : transform;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = Vector3.ClampMagnitude(fwd * input.y + right * input.x, 1f);

            float target = dir.sqrMagnitude < 0.01f ? 0f : (_sprint.IsPressed() ? sprintSpeed : Mathf.Lerp(walkSpeed, runSpeed, dir.magnitude));
            _speed = Mathf.MoveTowards(_speed, target, acceleration * Time.deltaTime);

            if (dir.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            }

            bool grounded = _cc.isGrounded;
            if (grounded && _velocity.y < 0f) _velocity.y = -2f;
            if (grounded && _jump.WasPressedThisFrame())
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (_anim) _anim.SetTrigger(JumpId);
            }
            _velocity.y += gravity * Time.deltaTime;

            Vector3 horizontal = (dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward * 0f) * _speed;
            _cc.Move((horizontal + Vector3.up * _velocity.y) * Time.deltaTime);

            if (_anim)
            {
                _anim.SetFloat(SpeedId, _speed, 0.1f, Time.deltaTime);
                _anim.SetBool(GroundedId, _cc.isGrounded);
            }
        }

        /// <summary>Sterowanie z zewnątrz (testy jazdy, później AI): x = gaz, y = skręt, z = hamulec ręczny.</summary>
        [System.NonSerialized] public System.Func<Vector3> autopilot;

        void DriveVehicle()
        {
            if (autopilot != null) { var a = autopilot(); _vehicle.SetInput(a.x, a.y, a.z > 0.5f); transform.position = _vehicle.transform.position; return; }
            Vector2 input = _move.ReadValue<Vector2>();
            _vehicle.SetInput(input.y, input.x, _jump.IsPressed());
            transform.position = _vehicle.transform.position;
        }

        /// <summary>Wsiądź do najbliższego pojazdu albo wysiądź (klawisz E, przycisk dotykowy, testy).</summary>
        public void EnterOrExit() => ToggleVehicle();

        void ToggleVehicle()
        {
            if (_vehicle)
            {
                _vehicle.HasDriver = false;
                Vector3 p = _vehicle.exitPoint ? _vehicle.exitPoint.position : _vehicle.transform.position + _vehicle.transform.right * -2f;
                _vehicle = null;
                transform.position = p;
                SetVisible(true);
                _cc.enabled = true;
                Follow(cameraTarget);
                return;
            }

            WheeledVehicle best = null;
            float bestD = enterDistance;
            foreach (var v in FindObjectsByType<WheeledVehicle>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(v.transform.position, transform.position);
                if (d < bestD) { best = v; bestD = d; }
            }
            if (!best) return;
            _vehicle = best;
            _vehicle.HasDriver = true;
            _cc.enabled = false;
            _speed = 0f;
            SetVisible(false);
            Follow(_vehicle.cameraTarget ? _vehicle.cameraTarget : _vehicle.transform);
        }

        void Follow(Transform t)
        {
            if (!cinemachineCamera) return;
            cinemachineCamera.Follow = t;
            cinemachineCamera.LookAt = t;
        }

        void SetVisible(bool v)
        {
            foreach (var r in _renderers) if (r) r.enabled = v;
        }
    }
}
