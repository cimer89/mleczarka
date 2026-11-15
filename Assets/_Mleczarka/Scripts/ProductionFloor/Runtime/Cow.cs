using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Player;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Krowa na pastwisku przy gospodarstwie: stoi (Idle), co jakiś czas przechodzi kilka kroków w obrębie swojego miejsca
    /// i zatrzymuje się przed przeszkodą (płot, budynek, gracz). Własna krowa (own) daje się wydoić — F przy krowie.
    /// </summary>
    public class Cow : MonoBehaviour
    {
        public float wanderRadius = 2.5f, walkSpeed = 0.55f, turnSpeed = 50f;
        public bool own;
        public string cowName = "Krasula", cowNameAcc = "Krasulę";
        public float milkReach = 2.8f;
        /// <summary>Kontroler animacji (stoi / idzie) — na korzeniu prefabu, bo nadpisanie w zagnieżdżonym modelu FBX się nie zapisuje.</summary>
        public RuntimeAnimatorController controller;
        /// <summary>Pastwisko (ogrodzony prostokąt): krowa wybiera cele spaceru tylko w jego środku.</summary>
        public bool hasArea; public Vector3 areaCenter; public Vector2 areaHalf; public float areaYaw;

        Vector3 _home, _target; float _until; bool _walking; Animator _anim; Collider[] _own;
        static readonly int WalkId = Animator.StringToHash("Walk");

        void Start()
        {
            _home = transform.position;
            _anim = GetComponentInChildren<Animator>();
            if (_anim && controller && _anim.runtimeAnimatorController != controller) { _anim.runtimeAnimatorController = controller; _anim.applyRootMotion = false; }
            _own = GetComponentsInChildren<Collider>();
            _until = Time.time + Random.Range(2f, 14f);
            if (_anim) { _anim.cullingMode = AnimatorCullingMode.CullCompletely; _anim.Play(0, 0, Random.value); _anim.speed = Random.Range(0.85f, 1.1f); }
            if (own) { GameController.HintHook += Hint; GameController.ActionHook += Action; }
        }

        void OnDestroy()
        {
            if (own) { GameController.HintHook -= Hint; GameController.ActionHook -= Action; }
        }

        void Update()
        {
            if (_milking) { MilkingUpdate(); return; }
            if (!_walking)
            {
                if (Time.time < _until) return;
                if (hasArea)
                {
                    var q = Quaternion.Euler(0, areaYaw, 0);
                    var loc = new Vector3(Random.Range(-areaHalf.x, areaHalf.x), 0, Random.Range(-areaHalf.y, areaHalf.y));
                    var near = transform.position + Random.insideUnitSphere * 5f;   // krótkie spacery, ale w obrębie płotu
                    var inLoc = Quaternion.Inverse(q) * (near - areaCenter);
                    loc = new Vector3(Mathf.Clamp(inLoc.x, -areaHalf.x, areaHalf.x), 0, Mathf.Clamp(inLoc.z, -areaHalf.y, areaHalf.y));
                    _target = areaCenter + q * loc;
                }
                else
                {
                    var off = Random.insideUnitCircle * wanderRadius;
                    _target = _home + new Vector3(off.x, 0, off.y);
                }
                if (Flat(_target - transform.position) < 0.8f) { _until = Time.time + 3f; return; }
                _walking = true; SetWalk(true); _until = Time.time + 15f;
                return;
            }
            var to = _target - transform.position; to.y = 0;
            if (to.magnitude < 0.25f || Time.time > _until || Blocked()) { Stop(); return; }
            var want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, want) < 30f)
            {
                var p = transform.position + transform.forward * (walkSpeed * Time.deltaTime);
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) p.y = h.point.y;
                transform.position = p;
            }
        }

        void Stop() { _walking = false; SetWalk(false); _until = Time.time + Random.Range(6f, 20f); }
        void SetWalk(bool w) { if (_anim) _anim.SetBool(WalkId, w); }

        /// <summary>Przeszkoda przed pyskiem (płot, ściana, drzewo, gracz) — krowa staje zamiast przez nią przechodzić.</summary>
        bool Blocked()
        {
            var o = transform.position + Vector3.up * 0.9f;
            foreach (var h in Physics.SphereCastAll(o, 0.35f, transform.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is TerrainCollider || h.normal.y > 0.6f) continue;
                bool mine = false; foreach (var c in _own) if (c == h.collider) { mine = true; break; }
                if (!mine) return true;
            }
            return false;
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        // ---------- własna krowa: dojenie ----------
        GameController G => GameController.Instance;
        bool Near()
        {
            if (!G || !G.player || G.player.CurrentVehicle || G.player.OnBike) return false;
            return Flat(G.player.transform.position - transform.position) < milkReach;
        }

        string Hint()
        {
            if (_milking) return $"Doisz krowę {cowNameAcc} — trzymaj F (w wiadrze {_session:0} l, zostało {Mathf.FloorToInt((float)G.Sim.S.players[GameController.Me].career.cowMilk)} l) · puść F, żeby wstać";
            if (!Near()) return null;
            var p = G.Sim.S.players[GameController.Me]; if (!CareerSim.Active(p)) return null;
            var c = p.career; int l = Mathf.FloorToInt((float)c.cowMilk + 0.01f);
            return l >= 1 ? $"Przytrzymaj F — wydój swoją krowę, {cowNameAcc} ({l} l mleka za darmo)" : $"{cowName} jest wydojona — da 20 l mleka na dobę, wróć później";
        }

        Result? Action()
        {
            if (_milking || !Near()) return null;
            var p = G.Sim.S.players[GameController.Me]; if (!CareerSim.Active(p)) return null;
            if (p.career.cowMilk + 0.01 < 1) return G.Run(new MilkOwnCow(GameController.Me, true, 1));   // komunikat „wydojona”
            var c = p.career;
            if (c.fridge > c.fridgeCap - 1 && c.canister > c.canisterCap - 1) return Result.Fail("Lodówka i bańka są pełne — najpierw zrób coś z mleka.");
            StartMilking();
            return Result.Success("");
        }

        // ---------- dojenie: wiadro, przyklęk przy wymionach, przytrzymanie F ----------
        bool _milking; float _milkStart, _nextLiter, _released; double _session; Vector3 _side; Transform _bucket, _bucketMilk; Vector3 _bucketMilkPos;

        /// <summary>Wymiona: pod brzuchem, bliżej tylnych nóg (głowa krowy po stronie +Z).</summary>
        Vector3 Udder => transform.position + Vector3.up * 0.58f - transform.forward * 0.42f;
        public bool Milking => _milking;
        public double SessionLiters => _session;

        void StartMilking()
        {
            var pl = G.player; _milking = true; _session = 0; _milkStart = Time.time; _nextLiter = Time.time + 1.3f; _released = -1;
            _walking = false; SetWalk(false); _until = float.MaxValue;
            // strona krowy bliżej gracza
            var toP = pl.transform.position - transform.position; _side = Vector3.Dot(toP, transform.right) >= 0 ? transform.right : -transform.right;
            var stand = Udder + _side * 0.62f; stand.y = transform.position.y;
            if (Physics.Raycast(stand + Vector3.up * 1.5f, Vector3.down, out var h, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) stand.y = h.point.y;
            var cc = pl.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            pl.transform.SetPositionAndRotation(stand + Vector3.up * 0.05f, Quaternion.LookRotation(-_side));
            if (cc) cc.enabled = true;
            pl.orbitWhenBlocked = false; pl.InputBlocked = true;
            var a = ActionAnim.For(pl);
            if (a)
            {
                a.Play(ActionAnim.Kind.Bierze, 1.0f);   // wyjmuje wiadro
                var props = Mleczarka.Player.ActionProps.I;
                if (props && props.bucket)
                {
                    _bucket = a.Prop("wiadro", props.bucket, () => _milking && Time.time - _milkStart > 0.9f ? BucketGround() : a.RightHand - Vector3.up * 0.38f,
                                     () => Quaternion.LookRotation(-_side), 9999f);
                    _bucketMilk = null;
                    if (_bucket) foreach (var t in _bucket.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Mleko")) { _bucketMilk = t; _bucketMilkPos = t.localPosition; }
                }
            }
        }

        Vector3 BucketGround()
        {
            var p = Udder - _side * 0.05f; p.y = transform.position.y;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            return p;
        }

        void MilkingUpdate()
        {
            var pl = G ? G.player : null; if (!pl) { _milking = false; return; }
            var a = ActionAnim.For(pl);
            bool held = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.isPressed;
            float t = Time.time - _milkStart;
            if (t > 0.9f && a)
            {
                a.Hold(ActionAnim.Kind.Doi);
                // dłonie przy wymionach, na zmianę ciągną w dół
                float k = Time.time * 7f;
                var u = Udder; var fwd = transform.forward * 0.06f;
                a.Hands(() => u + fwd - Vector3.up * (0.06f + 0.05f * Mathf.Max(0, Mathf.Sin(k))) + _side * 0.12f,
                        () => u - fwd - Vector3.up * (0.06f + 0.05f * Mathf.Max(0, Mathf.Sin(k + Mathf.PI))) + _side * 0.12f, 0.3f);
            }
            if (_bucketMilk) _bucketMilk.localPosition = _bucketMilkPos + Vector3.up * (0.2f * Mathf.Clamp01((float)(_session / 20.0)));
            if (held) _released = -1; else if (_released < 0) _released = Time.time;
            if (held && Time.time >= _nextLiter)
            {
                _nextLiter = Time.time + 0.5f;
                var r = G.Run(new MilkOwnCow(GameController.Me, true, 1));
                if (r.Ok) _session += 1; else { EndMilking(r.Msg); return; }
            }
            // puszczone F (krótka chwila na drgnięcie palca) albo krowa wydojona — koniec
            if ((_released > 0 && Time.time - _released > 0.35f && t > 1.2f) || G.Sim.S.players[GameController.Me].career.cowMilk + 0.01 < 1)
                EndMilking(null);
        }

        void EndMilking(string why)
        {
            _milking = false; _until = Time.time + Random.Range(4f, 10f);
            var pl = G.player; var a = ActionAnim.For(pl);
            if (a) { a.Stop(); a.HideProp("wiadro"); }
            if (pl) { pl.InputBlocked = false; pl.orbitWhenBlocked = true; }
            var c = G.Sim.S.players[GameController.Me].career;
            if (_session > 0) G.Say($"Wydojono {_session:0} l od {cowName} — za darmo. Lodówka {CareerSim.L(c.fridge)}/{c.fridgeCap:0} l, bańka {CareerSim.L(c.canister)}/{c.canisterCap:0} l." + (why != null && why.Contains("pełne") ? " " + why : ""));
            else if (!string.IsNullOrEmpty(why)) G.Say(why, true);
        }
    }
}
