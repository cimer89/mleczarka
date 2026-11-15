using Mleczarka.Core;
using Mleczarka.Gameplay;
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

        Vector3 _home, _target; float _until; bool _walking; Animator _anim; Collider[] _own;
        static readonly int WalkId = Animator.StringToHash("Walk");

        void Start()
        {
            _home = transform.position;
            _anim = GetComponentInChildren<Animator>();
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
            if (!_walking)
            {
                if (Time.time < _until) return;
                var off = Random.insideUnitCircle * wanderRadius;
                _target = _home + new Vector3(off.x, 0, off.y);
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
            if (!Near()) return null;
            var p = G.Sim.S.players[GameController.Me]; if (!CareerSim.Active(p)) return null;
            var c = p.career; int l = Mathf.FloorToInt((float)c.cowMilk);
            return l >= 1 ? $"F — wydój swoją krowę, {cowNameAcc} ({l} l mleka za darmo)" : $"{cowName} jest wydojona — da 20 l mleka na dobę, wróć później";
        }

        Result? Action()
        {
            if (!Near()) return null;
            var p = G.Sim.S.players[GameController.Me]; if (!CareerSim.Active(p)) return null;
            return G.Run(new MilkOwnCow(GameController.Me));
        }
    }
}
