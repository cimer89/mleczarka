using Mleczarka.Vehicles;
using Unity.Cinemachine;
using System.Linq;
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
        float _speed, _airTime, _zoomFoot = 1f, _zoomVehicle = 1f;
        bool _jumping;
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
            // kółko myszy: przybliżanie / oddalanie (osobno pieszo i w pojeździe)
            var m = Mouse.current;
            if (m != null && !InputBlocked)
            {
                float wheel = m.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    float step = Mathf.Sign(wheel) * 0.12f;
                    if (_vehicle) _zoomVehicle = Mathf.Clamp(_zoomVehicle * (1f - step), 0.45f, 2.6f);
                    else _zoomFoot = Mathf.Clamp(_zoomFoot * (1f - step), 0.3f, 2.6f);
                }
            }
            float want = _vehicle ? cameraRadiusInVehicle * _zoomVehicle : cameraRadiusOnFoot * _zoomFoot;
            orbital.Radius = Mathf.Lerp(orbital.Radius, want, Time.deltaTime * 6f);
        }

        void Walk()
        {
            if (OnBike) { Ride(); return; }
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

            // isGrounded CharacterControllera migocze na nierównym terenie i przy schodzeniu ze zbocza —
            // krótki margines (0,15 s) zamiast surowej wartości, inaczej postać "pływa" w pozie skoku po ziemi.
            bool grounded = _cc.isGrounded;
            _airTime = grounded ? 0f : _airTime + Time.deltaTime;
            if (grounded && _velocity.y < 0f) { _velocity.y = -2f; _jumping = false; }
            bool nearGround = _airTime < 0.15f && !_jumping;
            if (nearGround && !OnBike && _jump.WasPressedThisFrame())
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumping = true; _airTime = 0.15f;
                if (_anim) { _anim.ResetTrigger(JumpId); _anim.SetTrigger(JumpId); }
            }
            // przy zejściu ze zbocza docisk do ziemi, żeby nie odrywać się na każdym garbie
            if (!grounded && !_jumping && _airTime < 0.15f && _velocity.y < 0f) _velocity.y = Mathf.Min(_velocity.y, -6f);
            _velocity.y += gravity * Time.deltaTime;

            Vector3 horizontal = (dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward * 0f) * _speed;
            _cc.Move((horizontal + Vector3.up * _velocity.y) * Time.deltaTime);
            if (_cc.isGrounded) _airTime = 0f;

            if (_anim)
            {
                _anim.SetFloat(SpeedId, OnBike ? 0f : _speed, 0.1f, Time.deltaTime);
                _anim.SetBool(GroundedId, _cc.isGrounded || (_airTime < 0.15f && !_jumping));
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

        /// <summary>Kariera: zwraca komunikat, gdy pojazdu nie wolno jeszcze używać (null = można).</summary>
        public static System.Func<WheeledVehicle, string> CanEnter;
        public static event System.Action<string> Locked;
        /// <summary>Rower (dom gracza): odległość do zaparkowanego roweru i wsiadanie / zsiadanie — E działa jak B.</summary>
        public static System.Func<float> BikeDistance;
        public static System.Action BikeToggle;

        // ---------- rower (kariera) ----------
        public bool OnBike { get; private set; }
        public float CurrentSpeed => _speed;
        public float bikeSpeed = 6.5f, bikeSprint = 9f;
        Transform _bike, _model; Vector3 _modelPos; Quaternion _modelRot = Quaternion.identity;
        static readonly int BikeId = Animator.StringToHash("Bike");
        bool _animHasBike;

        /// <summary>Wsiada na rower (model roweru przypina się pod postacią) albo z niego zsiada.</summary>
        public void SetBike(bool on, Transform bikeModel = null)
        {
            if (_vehicle) return;
            if (bikeModel) _bike = bikeModel;
            OnBike = on && _bike;
            if (_bike) _bike.gameObject.SetActive(OnBike);
            if (!_model && _anim) { _model = _anim.transform; _modelPos = _model.localPosition; _modelRot = _model.localRotation; }
            if (_anim) _animHasBike = System.Array.Exists(_anim.parameters, p => p.nameHash == BikeId);
            if (_anim && _animHasBike) _anim.SetBool(BikeId, OnBike);
            if (!OnBike) ResetBikePose();
            if (_model) _model.localPosition = _modelPos + (OnBike ? Vector3.up * bikeSeatLift + Vector3.forward * bikeSeatForward : Vector3.zero);
            if (OnBike && _bike) { _bike.localRotation = Quaternion.identity; _steerParts = null; bikeSeatFix = Vector3.zero; CacheSteer(); _bike.localRotation = Quaternion.Euler(0, BikeYawOffset, 0); }
            if (_anim)
            {
                var ik = _anim.GetComponent<BikeRiderIK>(); if (!ik) ik = _anim.gameObject.AddComponent<BikeRiderIK>();
                ik.Setup(this, OnBike ? _bike : null);
            }
        }
        public float bikeSeatLift = 0.42f, bikeSeatForward = 0.16f;

        // ---------- jazda rowerem: kierownica, przechył, rozpędzanie, hamowanie ----------
        [Header("Rower")]
        public float bikeAccel = 2.2f, bikeBrake = 6f, bikeWheelbase = 1.08f;
        const float bikeMaxSteer = 30f, bikeMaxLean = 20f;
        /// <summary>Pochylenie tułowia w przód, żeby dłonie sięgały kierownicy (poza z biblioteki siedzi prosto jak w aucie).</summary>
        const float bikeRiderPitch = 16f;
        float _steer, _lean, _crankAngle, _wheelAngle, _brakeHold;
        Transform _bikeFront, _bikeRear, _bikeCrank; Quaternion _rearRot, _crankRot;
        Transform[] _steerParts; Vector3[] _steerPos; Quaternion[] _steerRot;
        public Transform BikeCrankTransform => _bikeCrank;
        public Transform BikeFrontTransform => _bikeFront;
        public float BikeSteer => _steer;
        public float BikeLean => _lean;
        /// <summary>Model roweru ma przód (przednie koło) po stronie -Z — wtedy obracamy go o 180°, żeby jechał przodem.</summary>
        public float BikeYawOffset { get; private set; }
        /// <summary>Korekta położenia rowerzysty względem siodełka (liczona przez BikeRiderIK z kości miednicy).</summary>
        [System.NonSerialized] public Vector3 bikeSeatFix;
        public float BikeCrank => _crankAngle;

        void Ride()
        {
            float dt = Time.deltaTime;
            Vector2 input = _move.ReadValue<Vector2>();
            bool sprint = _sprint.IsPressed();
            float max = sprint ? bikeSprint : bikeSpeed;
            // W — pedałowanie (rozpędzanie), S — hamulec, a na postoju powolne cofanie (prowadzenie roweru)
            if (input.y > 0.1f) _speed = Mathf.MoveTowards(_speed, max * input.y, bikeAccel * (sprint ? 1.35f : 1f) * dt);
            else if (input.y < -0.1f)
            {
                // hamulec; po zatrzymaniu i dalszym trzymaniu S (0,4 s) — powolne cofanie, jak prowadzenie roweru
                if (_speed > 0.05f) { _speed = Mathf.MoveTowards(_speed, 0f, bikeBrake * dt); _brakeHold = 0f; }
                else { _brakeHold += dt; if (_brakeHold > 0.4f) _speed = Mathf.MoveTowards(_speed, -0.9f, 1.5f * dt); else _speed = 0f; }
            }
            else _speed = Mathf.MoveTowards(_speed, 0f, (0.35f + 0.04f * _speed * _speed) * dt); // toczenie się i opór
            if (_speed > max) _speed = Mathf.MoveTowards(_speed, max, bikeBrake * 0.5f * dt);

            // A/D — kierownica: przy większej prędkości mniejszy maksymalny skręt
            float maxSteer = Mathf.Lerp(bikeMaxSteer, 6f, Mathf.InverseLerp(1.5f, 7f, Mathf.Abs(_speed)));
            _steer = Mathf.MoveTowards(_steer, input.x * maxSteer, 120f * dt);
            float yawRate = _speed / bikeWheelbase * Mathf.Tan(_steer * Mathf.Deg2Rad); // rad/s
            transform.rotation *= Quaternion.Euler(0, yawRate * Mathf.Rad2Deg * dt, 0);

            // przechył w zakręcie: tan(kąt) = v·ω / g
            float wantLean = -Mathf.Atan(_speed * yawRate / 9.81f) * Mathf.Rad2Deg;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(wantLean, -bikeMaxLean, bikeMaxLean), 1f - Mathf.Exp(-5f * dt));
            _wheelAngle += _speed / 0.34f * Mathf.Rad2Deg * dt;
            if (input.y > 0.1f) _crankAngle += Mathf.Max(_speed, 1.2f) / 0.34f * Mathf.Rad2Deg * 0.45f * dt; // pedałuje tylko przy gazie

            bool grounded = _cc.isGrounded;
            _airTime = grounded ? 0f : _airTime + dt;
            if (grounded && _velocity.y < 0f) _velocity.y = -2f;
            if (!grounded && _airTime < 0.15f && _velocity.y < 0f) _velocity.y = Mathf.Min(_velocity.y, -6f);
            _velocity.y += gravity * dt;
            var before = transform.position;
            _cc.Move((transform.forward * _speed + Vector3.up * _velocity.y) * dt);
            // uderzenie w przeszkodę wyhamowuje
            var moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (Mathf.Abs(_speed) > 0.5f && moved < Mathf.Abs(_speed) * dt * 0.3f) _speed *= 0.5f;

            if (_anim) { _anim.SetFloat(SpeedId, 0f, 0.1f, dt); _anim.SetBool(GroundedId, true); }
            BikeVisuals();
        }

        void BikeVisuals()
        {
            if (!_bike) return;
            // przechył całego zestawu (rower + rowerzysta) wokół styku kół z ziemią
            var lean = Quaternion.AngleAxis(_lean, Vector3.forward);
            _bike.localRotation = lean * Quaternion.Euler(0, BikeYawOffset, 0);
            if (_model) { _model.localPosition = lean * (_modelPos + Vector3.up * bikeSeatLift + Vector3.forward * bikeSeatForward + bikeSeatFix); _model.localRotation = lean * Quaternion.Euler(bikeRiderPitch, 0, 0) * _modelRot; }
            // kierownica z przednim kołem i koszykiem obraca się wokół pionu przez piastę przedniego koła
            if (_steerParts == null) CacheSteer();
            if (_steerParts == null || !_bikeFront) return;
            var axisPoint = _bike.InverseTransformPoint(_bikeFront.position);
            var rot = Quaternion.AngleAxis(_steer * 0.7f, Vector3.up);
            for (int i = 0; i < _steerParts.Length; i++)
            {
                var t = _steerParts[i]; if (!t) continue;
                // pozycje w układzie roweru; części mogą mieć innego rodzica, więc liczymy przez świat
                var basePos = _steerPos[i];
                t.position = _bike.TransformPoint(axisPoint + rot * (basePos - axisPoint));
                t.rotation = _bike.rotation * rot * _steerRot[i] * (t == _bikeFront ? Quaternion.AngleAxis(_wheelAngle, Vector3.right) : Quaternion.identity);
            }
            if (_bikeRear) _bikeRear.localRotation = _rearRot * Quaternion.AngleAxis(_wheelAngle, Vector3.right);
            if (_bikeCrank) _bikeCrank.localRotation = _crankRot * Quaternion.AngleAxis(_crankAngle, Vector3.right);
        }

        /// <summary>
        /// Przód roweru wyznaczany z modelu: strona z koszykiem/kierownicą (a nie nazwa koła — w modelu koła mogą mieć zamienione nazwy).
        /// Zwraca kierunek w świecie od tyłu do przodu oraz przypisuje przednie i tylne koło.
        /// </summary>
        public static Vector3 BikeFront(Transform bike, out Transform front, out Transform rear)
        {
            front = rear = null; Transform w1 = null, w2 = null;
            Vector3? basket = null, rack = null;
            foreach (var t in bike.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Kolo")) { if (!w1) w1 = t; else if (!w2) w2 = t; }
                var r = t.GetComponent<Renderer>();
                if (t.name.Contains("Koszyk") && r) basket = r.bounds.center;
                if ((t.name.Contains("Bagaznik") || t.name.Contains("Siodlo")) && r && rack == null) rack = r.bounds.center;
            }
            if (!w1 || !w2) return bike.forward;
            Vector3 anchor = basket ?? (rack.HasValue ? 2 * ((w1.position + w2.position) / 2) - rack.Value : w1.position);
            if (Vector3.Distance(w1.position, anchor) <= Vector3.Distance(w2.position, anchor)) { front = w1; rear = w2; } else { front = w2; rear = w1; }
            return Vector3.ProjectOnPlane(front.position - rear.position, Vector3.up).normalized;
        }

        void CacheSteer()
        {
            var parts = new System.Collections.Generic.List<Transform>();
            BikeFront(_bike, out _bikeFront, out _bikeRear);
            if (_bikeRear) _rearRot = _bikeRear.localRotation;
            if (_bikeFront) parts.Add(_bikeFront);
            foreach (var t in _bike.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("Korba_ObrotX")) { _bikeCrank = t; _crankRot = t.localRotation; }
                else if ((t.name.Contains("Przod") || t.name.Contains("Koszyk")) && !t.name.Contains("Kolo")) parts.Add(t);
            }
            if (!_bikeFront) return;
            _steerParts = parts.ToArray();
            BikeYawOffset = _bike.InverseTransformPoint(_bikeFront.position).z < _bike.InverseTransformPoint(_bikeRear.position).z ? 180f : 0f;
            _steerPos = parts.Select(t => _bike.InverseTransformPoint(t.position)).ToArray();
            _steerRot = parts.Select(t => Quaternion.Inverse(_bike.rotation) * t.rotation).ToArray();
        }

        void ResetBikePose()
        {
            if (_steerParts != null && _bike)
                for (int i = 0; i < _steerParts.Length; i++) if (_steerParts[i]) { _steerParts[i].position = _bike.TransformPoint(_steerPos[i]); _steerParts[i].rotation = _bike.rotation * _steerRot[i]; }
            if (_bikeRear) _bikeRear.localRotation = _rearRot;
            if (_bikeCrank) _bikeCrank.localRotation = _crankRot;
            if (_bike) _bike.localRotation = Quaternion.Euler(0, BikeYawOffset, 0);
            if (_model) _model.localRotation = _modelRot;
            _steer = 0; _lean = 0; _speed = 0;
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

            if (OnBike) { BikeToggle?.Invoke(); return; }
            float bikeD = BikeDistance != null ? BikeDistance() : float.MaxValue;
            WheeledVehicle best = null;
            float bestD = enterDistance;
            foreach (var v in FindObjectsByType<WheeledVehicle>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(v.transform.position, transform.position);
                if (d < bestD) { best = v; bestD = d; }
            }
            if (bikeD < 2.5f && (!best || bikeD < bestD) && BikeToggle != null) { BikeToggle(); return; }
            if (!best) return;
            var lockMsg = CanEnter?.Invoke(best);
            if (!string.IsNullOrEmpty(lockMsg)) { Locked?.Invoke(lockMsg); return; }
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
