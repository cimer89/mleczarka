using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Mleczarka.UI.UiKit;

namespace Mleczarka.UI
{
    /// <summary>
    /// Minimapa (prawy górny róg, północ u góry), wskazówka celu z odległością i duża mapa gminy pod klawiszem M.
    /// Gospodarstwa: zielone = twoje umowy, czerwone = Mleczpol, białe = wolne.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MapUI : MonoBehaviour
    {
        public float miniRadiusMeters = 380f;
        const float MiniSize = 280f;
        VisualElement _root, _mini, _miniMap, _miniArrow, _big, _bigMap, _bigArrow;
        Label _target;
        readonly List<(VisualElement el, VisualElement big, WorldPlace place)> _markers = new List<(VisualElement, VisualElement, WorldPlace)>();
        VisualElement _targetDot, _bigTargetDot;
        float _bigSize;

        MapInfo M => MapInfo.Instance;
        static MapUI _instance;
        /// <summary>Czy duża mapa (M) jest otwarta — inne panele nie reagują wtedy na Escape.</summary>
        public static bool BigMapOpen => _instance && _instance._big != null && _instance._big.style.display == DisplayStyle.Flex;
        GameController G => GameController.Instance;

        void Start()
        {
            _instance = this;
            if (!M || !M.mapTexture) { enabled = false; return; }
            _root = GetComponent<UIDocument>().rootVisualElement; _root.Clear(); _root.pickingMode = PickingMode.Ignore;

            // minimapa
            var frame = Card(Panel, 20, 6, 6); frame.style.position = Position.Absolute; frame.style.right = 20; frame.style.top = 20; frame.pickingMode = PickingMode.Ignore;
            _mini = new VisualElement(); _mini.style.width = MiniSize; _mini.style.height = MiniSize; _mini.style.overflow = Overflow.Hidden; Round(_mini, 16);
            _mini.pickingMode = PickingMode.Ignore;
            _miniMap = new VisualElement(); _miniMap.style.position = Position.Absolute; _miniMap.style.backgroundImage = new StyleBackground(M.mapTexture); _miniMap.pickingMode = PickingMode.Ignore;
            _mini.Add(_miniMap);
            _miniArrow = Arrow(22); _mini.Add(_miniArrow);
            _miniArrow.style.left = MiniSize / 2 - 11; _miniArrow.style.top = MiniSize / 2 - 11;
            frame.Add(_mini);
            var north = L("N", 13, Brand, true); north.style.position = Position.Absolute; north.style.top = 8; north.style.left = MiniSize / 2; frame.Add(north);
            _target = L("", 15, Ink, true); _target.style.marginTop = 4; _target.style.unityTextAlign = TextAnchor.MiddleCenter; _target.style.width = MiniSize; _target.style.whiteSpace = WhiteSpace.Normal;
            frame.Add(_target);
            _root.Add(frame);

            // duża mapa
            _big = new VisualElement(); _big.style.position = Position.Absolute; _big.style.left = 0; _big.style.right = 0; _big.style.top = 0; _big.style.bottom = 0;
            _big.style.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 0.72f); _big.style.alignItems = Align.Center; _big.style.justifyContent = Justify.Center; _big.style.display = DisplayStyle.None;
            _bigSize = 960f;
            var bigFrame = Card(Panel, 22, 10, 10);
            var title = Row(); title.style.justifyContent = Justify.SpaceBetween; title.style.marginBottom = 6;
            title.Add(L("Gmina Zabielsk — mapa", 24, Brand, true));
            title.Add(L("M — zamknij", 15, Muted, true));
            bigFrame.Add(title);
            _bigMap = new VisualElement(); _bigMap.style.width = _bigSize; _bigMap.style.height = _bigSize; _bigMap.style.backgroundImage = new StyleBackground(M.mapTexture); Round(_bigMap, 12); _bigMap.style.overflow = Overflow.Hidden;
            bigFrame.Add(_bigMap);
            var legend = Row(); legend.style.marginTop = 6; legend.style.flexWrap = Wrap.Wrap;
            legend.Add(Pill("● twoje gospodarstwa", Good, Color.white)); legend.Add(Pill("● Mleczpol", Bad, Color.white)); legend.Add(Pill("● wolne", Chip, Ink));
            legend.Add(Pill("■ mleczarnia", Brand, Color.white)); legend.Add(Pill("■ rynek", new Color(0.95f, 0.6f, 0.1f), Color.white)); legend.Add(Pill("★ cel", new Color(0.95f, 0.75f, 0.1f), Ink));
            bigFrame.Add(legend);
            _big.Add(bigFrame);
            foreach (var lab in M.labels)
            {
                var u = (lab.pos - M.worldMin) / M.worldSize;
                var l = L(lab.name, lab.kind == 1 ? 20 : lab.kind == 4 ? 13 : 16, lab.kind == 3 ? Bad : lab.kind == 4 ? Muted : Ink, true);
                l.style.position = Position.Absolute; l.style.left = u.x * _bigSize - 40; l.style.top = (1 - u.y) * _bigSize - 30;
                l.style.backgroundColor = new Color(1, 1, 1, 0.75f); Round(l, 6); Pad(l, 1, 5);
                _bigMap.Add(l);
            }
            _bigArrow = Arrow(26); _bigMap.Add(_bigArrow);
            _root.Add(_big);

            foreach (var p in WorldPlace.All)
            {
                bool farm = p.kind == PlaceKind.Gospodarstwo;
                var m = Dot(farm ? 12 : 16, !farm); _mini.Insert(_mini.childCount - 1, m);
                var b = Dot(farm ? 14 : 18, !farm); _bigMap.Add(b);
                _markers.Add((m, b, p));
            }
            _targetDot = Star(26); _mini.Insert(_mini.childCount - 1, _targetDot);
            _bigTargetDot = Star(30); _bigMap.Add(_bigTargetDot);
        }

        static VisualElement Arrow(float s)
        {
            var a = new Label("▲"); a.style.position = Position.Absolute; a.style.width = s; a.style.height = s; a.style.fontSize = s; a.style.color = new Color(0.95f, 0.2f, 0.15f);
            a.style.unityTextAlign = TextAnchor.MiddleCenter; a.style.unityFontStyleAndWeight = FontStyle.Bold; a.pickingMode = PickingMode.Ignore;
            a.style.unityTextOutlineColor = Color.white; a.style.unityTextOutlineWidth = 1.5f;
            return a;
        }
        static VisualElement Dot(float s, bool square)
        {
            var d = new VisualElement(); d.style.position = Position.Absolute; d.style.width = s; d.style.height = s; Round(d, square ? 3 : s / 2);
            d.style.borderTopWidth = d.style.borderBottomWidth = d.style.borderLeftWidth = d.style.borderRightWidth = 2;
            d.style.borderTopColor = d.style.borderBottomColor = d.style.borderLeftColor = d.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            d.pickingMode = PickingMode.Ignore; return d;
        }
        static VisualElement Star(float s)
        {
            var l = new Label("★"); l.style.position = Position.Absolute; l.style.width = s; l.style.height = s; l.style.fontSize = s; l.style.color = new Color(1f, 0.82f, 0.1f);
            l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.unityTextOutlineColor = new Color(0.2f, 0.15f, 0.05f); l.style.unityTextOutlineWidth = 1.2f; l.pickingMode = PickingMode.Ignore;
            return l;
        }

        Color MarkerColor(WorldPlace p)
        {
            if (p.kind == PlaceKind.Mleczarnia) return Brand;
            if (p.kind == PlaceKind.Rynek) return new Color(0.95f, 0.6f, 0.1f);
            if (!G || p.farmId < 0 || p.farmId >= G.Sim.S.farms.Count) return Color.white;
            int o = G.Sim.S.farms[p.farmId].owner;
            return o == GameController.Me ? Good : o == 1 ? Bad : Color.white;
        }

        /// <summary>Dokąd jechać: z mlekiem do mleczarni, pustą cysterną do najbliższej swojej farmy z mlekiem, dostawczakiem z towarem na rynek.</summary>
        (WorldPlace place, string why) Target(Vector3 pos)
        {
            if (!G) return (null, null);
            var p = G.Sim.S.players[GameController.Me]; var v = G.player ? G.player.CurrentVehicle : null;
            WorldPlace Find(PlaceKind k) => WorldPlace.All.Where(w => w && w.kind == k).OrderBy(w => (w.transform.position - pos).sqrMagnitude).FirstOrDefault();
            if (!v && CareerSim.Active(p))
            {
                var c = p.career;
                if (c.basket.Sum() > 0) return (Find(PlaceKind.Rynek), "Towar w koszyku — na rynek");
                if (c.canister >= c.canisterCap * 0.6 || (c.canister >= 1 && c.fridge < 3)) return (Find(PlaceKind.Dom), "Mleko do lodówki — do domu");
                if (c.pantry.Sum() > 0) return (Find(PlaceKind.Dom), "Gotowy towar w spiżarce — dom");
                var farm = WorldPlace.All.Where(w => w && w.kind == PlaceKind.Gospodarstwo && w.farmId >= 0 && G.Sim.S.farms[w.farmId].stock >= 0.5)
                    .OrderBy(w => (w.transform.position - pos).sqrMagnitude).FirstOrDefault();
                if (c.fridge < 3 && farm) return (farm, $"Kup mleko: {farm.displayName}");
                return (Find(PlaceKind.Dom), "Do domu — letnia kuchnia");
            }
            if (v && p.vehicle == VehicleKind.Cysterna)
            {
                if (p.tankerMilk >= p.tankerCap * 0.8f) return (Find(PlaceKind.Mleczarnia), "Cysterna pełna — do mleczarni");
                var farm = WorldPlace.All.Where(w => w && w.kind == PlaceKind.Gospodarstwo && w.farmId >= 0 && G.Sim.S.farms[w.farmId].owner == GameController.Me && G.Sim.S.farms[w.farmId].stock >= 3)
                    .OrderBy(w => (w.transform.position - pos).sqrMagnitude).FirstOrDefault();
                if (farm) return (farm, $"Mleko: {farm.displayName}");
                if (p.tankerMilk > 0) return (Find(PlaceKind.Mleczarnia), "Do mleczarni");
            }
            if (v && p.vehicle == VehicleKind.Dostawczak)
            {
                if (p.vanCargo.Sum() > 0) return (Find(PlaceKind.Rynek), "Towar na rynek");
                if (p.store.Sum() > 0) return (Find(PlaceKind.Mleczarnia), "Odbierz towar z mleczarni");
            }
            return (null, null);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) _big.style.display = _big.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) _big.style.display = DisplayStyle.None;
            if (!G) return;

            Vector3 pos = G.PlayerPosition;
            var src = G.player ? (G.player.CurrentVehicle ? G.player.CurrentVehicle.transform : G.player.transform) : null;
            float yaw = src ? Mathf.Atan2(src.forward.x, src.forward.z) * Mathf.Rad2Deg : 0;
            var u = M.ToMap(pos);
            float full = M.worldSize * (MiniSize / 2f) / miniRadiusMeters;
            _miniMap.style.width = full; _miniMap.style.height = full;
            _miniMap.style.left = MiniSize / 2 - u.x * full; _miniMap.style.top = MiniSize / 2 - (1 - u.y) * full;
            _miniArrow.style.rotate = new Rotate(new Angle(yaw, AngleUnit.Degree));

            bool bigOpen = _big.style.display == DisplayStyle.Flex;
            if (bigOpen) { _bigArrow.style.left = u.x * _bigSize - 13; _bigArrow.style.top = (1 - u.y) * _bigSize - 13; _bigArrow.style.rotate = new Rotate(new Angle(yaw, AngleUnit.Degree)); }

            foreach (var (el, big, place) in _markers)
            {
                if (!place) continue;
                var q = M.ToMap(place.transform.position); var col = MarkerColor(place);
                float s = el.resolvedStyle.width > 0 ? el.resolvedStyle.width : 12;
                el.style.left = MiniSize / 2 + (q.x - u.x) * full - s / 2; el.style.top = MiniSize / 2 - (q.y - u.y) * full - s / 2;
                el.style.backgroundColor = col;
                if (bigOpen) { float bs = big.resolvedStyle.width > 0 ? big.resolvedStyle.width : 14; big.style.left = q.x * _bigSize - bs / 2; big.style.top = (1 - q.y) * _bigSize - bs / 2; big.style.backgroundColor = col; }
            }

            var (target, why) = Target(pos);
            _targetDot.style.display = _bigTargetDot.style.display = target ? DisplayStyle.Flex : DisplayStyle.None;
            if (target)
            {
                var tq = M.ToMap(target.transform.position);
                var off = new Vector2((tq.x - u.x) * full, -(tq.y - u.y) * full);
                float lim = MiniSize / 2 - 14; if (off.magnitude > lim) off = off.normalized * lim;   // poza zasięgiem: gwiazdka na brzegu
                _targetDot.style.left = MiniSize / 2 + off.x - 13; _targetDot.style.top = MiniSize / 2 + off.y - 13;
                _bigTargetDot.style.left = tq.x * _bigSize - 15; _bigTargetDot.style.top = (1 - tq.y) * _bigSize - 15;
                var d = target.transform.position - pos; d.y = 0;
                float dist = d.magnitude;
                _target.text = $"{why}\n{(dist >= 1000 ? $"{dist / 1000f:0.0} km" : $"{dist:0} m")} {Compass(d)}";
            }
            else _target.text = G.CurrentPlace() ? G.CurrentPlace().displayName : "";
        }

        static string Compass(Vector3 d)
        {
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            float a = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f + 22.5f) % 360f;
            return "(" + dirs[(int)(a / 45f)] + ")";
        }
    }
}
