using UnityEngine;

namespace Mleczarka.Player
{
    /// <summary>
    /// Rowerzysta: stopy na pedałach (kręcą się z korbą), dłonie na chwytach kierownicy, kolana do przodu.
    /// Działa na humanoidzie z włączonym IK Pass w warstwie bazowej animatora (Rolnik.controller).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class BikeRiderIK : MonoBehaviour
    {
        public float pedalRadius = 0.17f, pedalHalfWidth = 0.13f, footWeight = 1f, handWeight = 0.9f;
        public float gripHeight = 0.56f, gripBack = 0.2f, gripHalfWidth = 0.26f;
        /// <summary>Przesunięcie fazy pedałów względem modelu korby (stopnie) — dopasowanie do ramion korby w modelu.</summary>
        public float pedalPhase = 0f;
        Animator _anim; FarmerController _rider; Transform _bike, _front;
        Vector3 _gripL, _gripR; bool _hasGrips; Transform _saddle;

        /// <summary>Po animacji: miednica rowerzysty dociągana nad siodełko (pozy z biblioteki mają inny punkt siedzenia).</summary>
        void LateUpdate()
        {
            if (!_anim || !_rider || !_rider.OnBike || !_saddle || !_anim.isHuman) return;
            var hips = _anim.GetBoneTransform(HumanBodyBones.Hips); if (!hips) return;
            var r = _saddle.GetComponent<Renderer>();
            var top = r ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z) : _saddle.position;
            var want = top + _bike.up * 0.1f;
            var d = _rider.transform.InverseTransformVector(want - hips.position);
            d.x = 0; // tylko w przód/tył i w pionie
            _rider.bikeSeatFix = Vector3.ClampMagnitude(_rider.bikeSeatFix + d * Mathf.Min(1f, Time.deltaTime * 8f), 1.2f);
        }
        public string Names() { var sb = new System.Text.StringBuilder(); if (_bike) foreach (var t in _bike.GetComponentsInChildren<Transform>(true)) sb.Append(t.name).Append(", "); return sb.ToString(); }

        public void Setup(FarmerController rider, Transform bike)
        {
            _anim = GetComponent<Animator>(); _rider = rider; _bike = bike; _hasGrips = false;
            if (!bike) return;
            // chwyty: górne skrajne punkty widelca z kierownicą (Rower_Przod) — z granic siatki, bez odczytu wierzchołków
            _saddle = null;
            foreach (var t in bike.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Siodlo")) _saddle = t;
            foreach (var mf in bike.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.Contains("Przod") || mf.name.Contains("Kolo") || !mf.sharedMesh) continue;
                _front = mf.transform;
                var b = mf.sharedMesh.bounds;
                _gripL = new Vector3(b.min.x + b.size.x * 0.08f, b.max.y - 0.03f, b.min.z + Mathf.Min(0.08f, b.size.z * 0.3f));
                _gripR = new Vector3(b.max.x - b.size.x * 0.08f, b.max.y - 0.03f, b.min.z + Mathf.Min(0.08f, b.size.z * 0.3f));
                _hasGrips = true;
            }
        }

        [System.NonSerialized] public int ikCalls; [System.NonSerialized] public string ikStatus = "";
        void OnAnimatorIK(int layer)
        {
            ikCalls++;
            ikStatus = $"warstwa {layer}, humanoid {(_anim ? _anim.isHuman : false)}, na rowerze {(_rider ? _rider.OnBike : false)}, rower {(bool)_bike}, korba {(_rider && _rider.BikeCrankTransform)}, chwyty {_hasGrips}";
            if (layer != 0 || !_anim || !_rider || !_rider.OnBike || !_bike) { Clear(); return; }
            var crank = _rider.BikeCrankTransform;
            if (crank)
            {
                float a = (_rider.BikeCrank + pedalPhase) * Mathf.Deg2Rad;
                var rt = _rider.transform; var up = _bike.up;
                var fwd = Vector3.ProjectOnPlane(rt.forward, up).normalized; var right = Vector3.Cross(up, fwd);
                var arm = up * Mathf.Cos(a) + fwd * Mathf.Sin(a);
                Vector3 left = crank.position - right * pedalHalfWidth + arm * pedalRadius;
                Vector3 rightP = crank.position + right * pedalHalfWidth - arm * pedalRadius;
                var footRot = Quaternion.LookRotation(fwd, up);
                Set(AvatarIKGoal.LeftFoot, left + up * 0.04f, footRot, footWeight);
                Set(AvatarIKGoal.RightFoot, rightP + up * 0.04f, footRot, footWeight);
                _anim.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 1f); _anim.SetIKHintPosition(AvatarIKHint.LeftKnee, crank.position - right * 0.18f + fwd * 0.6f + up * 0.5f);
                _anim.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 1f); _anim.SetIKHintPosition(AvatarIKHint.RightKnee, crank.position + right * 0.18f + fwd * 0.6f + up * 0.5f);
            }
            // chwyty kierownicy: nad piastą przedniego koła, lekko w stronę rowerzysty, skręcone z kierownicą
            var hub = _rider.BikeFrontTransform;
            if (hub)
            {
                var rt = _rider.transform; var up = _bike.up;
                var fwd = Vector3.ProjectOnPlane(rt.forward, up).normalized;
                var steer = Quaternion.AngleAxis(_rider.BikeSteer * 0.7f, up);
                var f2 = steer * fwd; var r2 = Vector3.Cross(up, f2);
                var bar = hub.position + up * gripHeight - f2 * gripBack;
                var hr = Quaternion.LookRotation(f2, up);
                Set(AvatarIKGoal.LeftHand, bar - r2 * gripHalfWidth, hr, handWeight);
                Set(AvatarIKGoal.RightHand, bar + r2 * gripHalfWidth, hr, handWeight);
            }
        }

        void Set(AvatarIKGoal g, Vector3 p, Quaternion r, float w)
        {
            _anim.SetIKPositionWeight(g, w); _anim.SetIKPosition(g, p);
            _anim.SetIKRotationWeight(g, w * 0.6f); _anim.SetIKRotation(g, r);
        }

        void Clear()
        {
            if (!_anim) return;
            foreach (AvatarIKGoal g in System.Enum.GetValues(typeof(AvatarIKGoal))) { _anim.SetIKPositionWeight(g, 0); _anim.SetIKRotationWeight(g, 0); }
            _anim.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0); _anim.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0);
        }
    }
}
