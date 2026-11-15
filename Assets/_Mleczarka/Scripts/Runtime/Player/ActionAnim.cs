using System;
using UnityEngine;

namespace Mleczarka.Player
{
    /// <summary>
    /// Animacje czynności rolnika: krótkie klipy z biblioteki UAL (bierze, przelewa, otwiera, podaje, doi w przyklęku)
    /// przez parametr „Act” w Rolnik.controller oraz dłonie prowadzone IK (mieszanie łyżką w garnku, korba, tłok maselnicy, wymiona).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ActionAnim : MonoBehaviour
    {
        public enum Kind { None = 0, Bierze = 1, Przelewa = 2, Otwiera = 3, Doi = 4, Podaje = 5 }

        Animator _a; bool _hasAct; float _until; Kind _kind;
        static readonly int ActId = Animator.StringToHash("Act");
        Func<Vector3> _right, _left; float _ikUntil, _w;

        public static ActionAnim For(FarmerController f)
        {
            if (!f) return null;
            var a = f.GetComponentInChildren<Animator>(); if (!a) return null;
            var x = a.GetComponent<ActionAnim>(); if (!x) x = a.gameObject.AddComponent<ActionAnim>();
            return x;
        }

        void Awake()
        {
            _a = GetComponent<Animator>();
            _hasAct = _a && Array.Exists(_a.parameters, p => p.nameHash == ActId);
        }

        public Kind Current => Time.time < _until ? _kind : Kind.None;
        /// <summary>Czy IK dłoni jest teraz używane (BikeRiderIK wtedy nie zeruje wag).</summary>
        public bool IKActive => _w > 0.01f;

        /// <summary>Krótka czynność (klip) na określony czas; ponowne wywołanie przedłuża.</summary>
        public void Play(Kind k, float seconds)
        {
            if (k == Kind.Przelewa && _kind != Kind.Przelewa) Mleczarka.Gameplay.GameAudio.Play("Przelewanie_Mleka", transform.position + Vector3.up, 0.8f, 1f, Mathf.Max(1.2f, seconds + 0.3f));
            _kind = k; _until = Time.time + seconds;
            if (_hasAct) _a.SetInteger(ActId, (int)k);
        }

        /// <summary>Czynność trwa, dopóki jest wywoływana co klatkę (np. przytrzymanie klawisza).</summary>
        public void Hold(Kind k) => Play(k, 0.3f);

        public void Stop() { _until = 0; if (_hasAct) _a.SetInteger(ActId, 0); }

        /// <summary>Dłonie do punktów (świat) na określony czas — np. kółka łyżką nad garnkiem.</summary>
        public void Hands(Func<Vector3> right, Func<Vector3> left, float seconds)
        {
            _right = right; _left = left; _ikUntil = Time.time + seconds;
        }

        // ---------- rekwizyty (łyżka, wiadro): instancja na klucz, ustawiana co klatkę po animacji i IK ----------
        class PropSlot { public GameObject go; public Func<Vector3> pos; public Func<Quaternion> rot; public float until; }
        readonly System.Collections.Generic.Dictionary<string, PropSlot> _props = new System.Collections.Generic.Dictionary<string, PropSlot>();

        /// <summary>Pokazuje rekwizyt w punkcie liczonym co klatkę (np. przy dłoni) przez określony czas. Zwraca jego transform.</summary>
        public Transform Prop(string key, GameObject prefab, Func<Vector3> pos, Func<Quaternion> rot, float seconds)
        {
            if (!prefab) return null;
            if (!_props.TryGetValue(key, out var s) || !s.go)
            {
                s = new PropSlot { go = Instantiate(prefab) }; s.go.name = prefab.name + " (czynność)";
                foreach (var c in s.go.GetComponentsInChildren<Collider>()) Destroy(c);
                _props[key] = s;
            }
            s.pos = pos; s.rot = rot; s.until = Time.time + seconds; s.go.SetActive(true);
            s.go.transform.SetPositionAndRotation(pos(), rot());
            return s.go.transform;
        }

        public void HideProp(string key) { if (_props.TryGetValue(key, out var s) && s.go) s.go.SetActive(false); }

        public Vector3 RightHand => _a && _a.isHuman && _a.GetBoneTransform(HumanBodyBones.RightHand) ? _a.GetBoneTransform(HumanBodyBones.RightHand).position : transform.position + Vector3.up;
        public Vector3 LeftHand => _a && _a.isHuman && _a.GetBoneTransform(HumanBodyBones.LeftHand) ? _a.GetBoneTransform(HumanBodyBones.LeftHand).position : transform.position + Vector3.up;

        void LateUpdate()
        {
            foreach (var s in _props.Values)
            {
                if (!s.go || !s.go.activeSelf) continue;
                if (Time.time > s.until) { s.go.SetActive(false); continue; }
                s.go.transform.SetPositionAndRotation(s.pos(), s.rot());
            }
        }

        void OnDestroy() { foreach (var s in _props.Values) if (s.go) Destroy(s.go); }

        void Update()
        {
            if (_hasAct && _kind != Kind.None && Time.time >= _until) { _kind = Kind.None; _a.SetInteger(ActId, 0); }
            float want = Time.time < _ikUntil ? 1f : 0f;
            _w = Mathf.MoveTowards(_w, want, Time.deltaTime * 4f);
        }

        void OnAnimatorIK(int layer)
        {
            if (layer != 0 || !_a || _w <= 0.01f) return;
            if (_right != null) { _a.SetIKPositionWeight(AvatarIKGoal.RightHand, _w); _a.SetIKPosition(AvatarIKGoal.RightHand, _right()); }
            if (_left != null) { _a.SetIKPositionWeight(AvatarIKGoal.LeftHand, _w); _a.SetIKPosition(AvatarIKGoal.LeftHand, _left()); }
        }
    }
}
