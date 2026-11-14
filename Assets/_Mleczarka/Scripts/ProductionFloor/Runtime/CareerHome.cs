using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Dom gracza w trybie kariery: letnia kuchnia ze sprzętem, rower z bańką i koszykiem, panel rozwoju (K).
    /// Q przy sprzęcie — gotowanie (wybór przepisu 1–9, −/+ partia), B — rower, K — zakupy.
    /// Tylko obraz i sterowanie; reguły w CareerSim.
    /// </summary>
    public sealed class CareerHome : MonoBehaviour
    {
        [System.Serializable] public class Station { public string id, title; public Transform anchor, operatorPoint; public GameObject showWhenOwned; public string ownedId; }

        public Station[] stations;
        public Transform bike, bikeParking, canOnBike, basketPoint, pantryJars, steamPoint;
        public Transform potLid, churnPlunger, separatorCrank, pressPlunger, fridgeDoor;
        public Material steamMat, jarMat;

        GameController G => GameController.Instance;
        PlayerState P => G.Sim.S.players[GameController.Me];
        bool Active => G && G.Sim != null && CareerSim.Active(P);

        Station _near;
        int _recipe, _qty = 1;
        float _nextHold, _handsAt = -10;
        bool _shop;
        Vector2 _shopScroll;
        GUIStyle _text, _small, _title;
        StartMenuUI _menu;
        Transform _bikeFront, _bikeRear, _bikeCrank;
        readonly List<Transform> _jars = new List<Transform>();
        readonly List<(Transform t, float age)> _puffs = new List<(Transform, float)>();
        Vector3 _lidPos, _churnPos, _pressPos; Quaternion _doorRot;
        readonly List<GameObject> _basketItems = new List<GameObject>();

        void Start()
        {
            if (!G) { enabled = false; return; }
            if (!Active)
            {
                // poza karierą dom jest tylko dekoracją; rower stoi zaparkowany
                return;
            }
            if (bike)
                foreach (var t in bike.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith("Kolo_Przod_ObrotX")) _bikeFront = t;
                    else if (t.name.EndsWith("Kolo_Tyl_ObrotX")) _bikeRear = t;
                    else if (t.name.EndsWith("Korba_ObrotX")) _bikeCrank = t;
                }
            if (pantryJars) foreach (Transform t in pantryJars.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Sloik_")) _jars.Add(t);
            _jars.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (potLid) _lidPos = potLid.localPosition;
            if (churnPlunger) _churnPos = churnPlunger.localPosition;
            if (pressPlunger) _pressPos = pressPlunger.localPosition;
            if (fridgeDoor) _doorRot = fridgeDoor.localRotation;
        }

        bool Blocked()
        {
            if (!G.player || G.player.CurrentVehicle) return true;
            if (!_menu) _menu = FindFirstObjectByType<StartMenuUI>();
            return (_menu && _menu.IsOpen) || (G.player.InputBlocked && !_shop);
        }

        static readonly string[] AllStations = { "kuchenka", "lodowka", "stol", "maselnica", "wirowka", "prasa", "polka" };
        IEnumerable<Recipe> RecipesAt(string station) => CareerData.Recipes.Where(r => r.Station == station);

        void Update()
        {
            if (!Active) return;
            var kb = Keyboard.current;
            // sprzęt widoczny po zakupie
            foreach (var s in stations) if (s.showWhenOwned) s.showWhenOwned.SetActive(string.IsNullOrEmpty(s.ownedId) || P.career.owned.Contains(s.ownedId));
            _near = null;
            if (G.player && !G.player.CurrentVehicle && !G.player.OnBike)
            {
                float best = 1.4f; var pp = G.player.transform.position;
                foreach (var s in stations)
                {
                    if (!s.operatorPoint || (s.showWhenOwned && !s.showWhenOwned.activeSelf)) continue;
                    var d = s.operatorPoint.position - pp; d.y = 0;
                    if (d.magnitude < best && Mathf.Abs(s.operatorPoint.position.y - pp.y) < 1.5f) { best = d.magnitude; _near = s; }
                }
            }
            if (kb != null)
            {
                if (kb.kKey.wasPressedThisFrame && G.player && !(_menu && _menu.IsOpen)) ToggleShop(!_shop);
                if (_shop && kb.escapeKey.wasPressedThisFrame) ToggleShop(false);
                if (!_shop && kb.bKey.wasPressedThisFrame && G.player && !G.player.CurrentVehicle) ToggleBike();
                if (!_shop && !Blocked() && _near != null) HandleKeys(kb);
            }
            Animate();
        }

        public void ToggleShop(bool open)
        {
            _shop = open;
            if (G.player) { G.player.orbitWhenBlocked = false; G.player.InputBlocked = open; if (!open) G.player.orbitWhenBlocked = true; }
            if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        public void ToggleBike()
        {
            if (!bike) return;
            if (G.player.OnBike)
            {
                G.player.SetBike(false);
                bike.SetParent(null, true);
                var t = G.player.transform;
                bike.position = t.position + t.right * 0.8f; bike.rotation = t.rotation;
                bike.gameObject.SetActive(true);
                return;
            }
            float d = Vector3.Distance(bike.position, G.player.transform.position);
            if (d > 3f) { G.Say($"Rower stoi {d:0} m stąd — podejdź do niego (B).", true); return; }
            G.player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(bike.forward, Vector3.up));
            bike.SetParent(G.player.transform, false);
            bike.localPosition = Vector3.zero; bike.localRotation = Quaternion.identity;
            G.player.SetBike(true, bike);
            G.Say("Na rowerze: W/S — jazda, Shift — szybciej, B — zsiądź.");
        }

        void HandleKeys(Keyboard kb)
        {
            var list = RecipesAt(_near.id).ToList();
            for (int i = 0; i < Mathf.Min(9, list.Count); i++) if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) _recipe = i;
            _recipe = Mathf.Clamp(_recipe, 0, Mathf.Max(0, list.Count - 1));
            int max = CareerSim.MaxBatch(P);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) _qty = Mathf.Max(1, _qty - 1);
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) _qty = Mathf.Min(max, _qty + 1);
            _qty = Mathf.Clamp(_qty, 1, max);
            bool q = kb.qKey.wasPressedThisFrame, hold = kb.qKey.isPressed && Time.time >= _nextHold;
            if (q || hold) Primary(q);
        }

        void Primary(bool pressed)
        {
            if (_near.id == "polka") { if (pressed) G.Send(new LoadBasket(GameController.Me)); return; }
            var job = CareerSim.JobAt(P, _near.id);
            if (job == null)
            {
                if (!pressed) return;
                var list = RecipesAt(_near.id).ToList(); if (list.Count == 0) return;
                G.Send(new StartKitchen(GameController.Me, list[_recipe].Id, _qty));
                _nextHold = Time.time + 0.5f; return;
            }
            var d = CareerData.R(job.recipe).Steps[job.step];
            if (d.Kind == StepKind.Auto) { if (pressed) G.Say($"{d.Label} — poczekaj chwilę."); return; }
            if (!pressed && d.Kind != StepKind.Hold) return;
            var r = G.SendHold(new KitchenAct(GameController.Me, _near.id));
            if (r.Ok) { _handsAt = Time.time; Face(_near); }
            _nextHold = Time.time + 0.5f;
        }

        void Face(Station s)
        {
            if (!s.anchor) return;
            var to = s.anchor.position - G.player.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f) G.player.transform.rotation = Quaternion.LookRotation(to.normalized);
        }

        // ---------------- animacje ----------------

        bool Working(string station)
        {
            var j = CareerSim.JobAt(P, station); if (j == null || j.Done) return false;
            var d = CareerData.R(j.recipe).Steps[j.step];
            return d.Kind == StepKind.Auto || (Time.time - _handsAt < 0.8f && _near != null && _near.id == station);
        }

        void Animate()
        {
            float t = Time.time, dt = Time.deltaTime; var c = P.career;
            if (potLid) potLid.localPosition = _lidPos + (Working("kuchenka") ? Vector3.up * (0.006f + 0.006f * Mathf.Sin(t * 22)) : Vector3.zero);
            if (steamPoint && Working("kuchenka") && Random.value < dt * 8) Puff(steamPoint.position);
            if (churnPlunger) churnPlunger.localPosition = _churnPos + (Working("maselnica") ? Vector3.up * (0.12f * Mathf.Sin(t * 9)) : Vector3.zero);
            if (separatorCrank && Working("wirowka")) separatorCrank.localRotation *= Quaternion.AngleAxis(360 * dt, Vector3.right);
            if (pressPlunger) pressPlunger.localPosition = Vector3.Lerp(pressPlunger.localPosition, _pressPos + (Working("prasa") ? Vector3.down * 0.12f : Vector3.zero), dt * 3);
            if (fridgeDoor)
            {
                bool open = _near != null && _near.id == "lodowka";
                fridgeDoor.localRotation = Quaternion.Slerp(fridgeDoor.localRotation, _doorRot * Quaternion.AngleAxis(open ? -95 : 0, Vector3.up), dt * 5);
            }
            for (int i = 0; i < _jars.Count; i++) _jars[i].gameObject.SetActive(i < c.pantry.Sum());
            if (G.player && G.player.OnBike)
            {
                float v = G.player.CurrentSpeed;
                float spin = v / 0.34f * Mathf.Rad2Deg * dt;
                if (_bikeFront) _bikeFront.localRotation *= Quaternion.AngleAxis(spin, Vector3.right);
                if (_bikeRear) _bikeRear.localRotation *= Quaternion.AngleAxis(spin, Vector3.right);
                if (_bikeCrank) _bikeCrank.localRotation *= Quaternion.AngleAxis(spin * 0.45f, Vector3.right);
            }
            if (canOnBike) canOnBike.gameObject.SetActive(true);
            SyncBasket();
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var (p, age) = _puffs[i]; age += dt;
                if (!p || age > 1.8f) { if (p) Destroy(p.gameObject); _puffs.RemoveAt(i); continue; }
                p.position += Vector3.up * 0.45f * dt; p.localScale *= 1 + dt * 0.7f; _puffs[i] = (p, age);
            }
        }

        void Puff(Vector3 at)
        {
            if (_puffs.Count > 30 || !steamMat) return;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = steamMat; g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.position = at + Random.insideUnitSphere * 0.05f; g.transform.localScale = Vector3.one * 0.12f;
            _puffs.Add((g.transform, 0));
        }

        void SyncBasket()
        {
            if (!basketPoint) return;
            int n = Mathf.Min(8, P.career.basket.Sum());
            while (_basketItems.Count < n)
            {
                int i = _basketItems.Count;
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(basketPoint, false); g.transform.localScale = new Vector3(0.09f, 0.055f, 0.09f);
                g.transform.localPosition = new Vector3((i % 4 - 1.5f) * 0.07f, 0.06f + (i / 4) * 0.11f, 0);
                if (jarMat) g.GetComponent<Renderer>().sharedMaterial = jarMat;
                _basketItems.Add(g);
            }
            while (_basketItems.Count > n) { Destroy(_basketItems[_basketItems.Count - 1]); _basketItems.RemoveAt(_basketItems.Count - 1); }
        }

        // ---------------- interfejs ----------------

        static void Fill(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        void Styles()
        {
            if (_text != null) return;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };
            _small = new GUIStyle(_text) { fontSize = 15 };
            _title = new GUIStyle(_text) { fontSize = 24, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (!Active || !G.player) return;
            if (_menu && _menu.IsOpen) return;
            float scale = Mathf.Clamp(Screen.width / 1500f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Styles();
            float SW = Screen.width / scale, SH = Screen.height / scale;
            StatusBar(SW);
            if (_shop) { ShopPanel(SW, SH); return; }
            if (_near != null && !G.player.InputBlocked) StationPanel(SH);
        }

        void StatusBar(float SW)
        {
            var c = P.career;
            string s = $"Bańka {c.canister:0}/{c.canisterCap:0} l   ·   Lodówka {c.fridge:0}/{c.fridgeCap:0} l   ·   Śmietanka {c.cream:0.0} l   ·   Spiżarka {c.pantry.Sum()}   ·   Koszyk {c.basket.Sum()}/{c.basketCap}" +
                       (G.player.OnBike ? "   ·   <color=#7fe0a0>na rowerze</color>" : "") + "   ·   <color=#9fb>K — rozwój</color>";
            float w = Mathf.Min(980, SW - 40), x = (SW - w) / 2, y = 92;
            Fill(new Rect(x, y, w, 34), new Color(0.03f, 0.1f, 0.13f, 0.85f)); Fill(new Rect(x, y, 5, 34), new Color(0.95f, 0.75f, 0.3f));
            var st = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(x, y, w, 34), s, st);
        }

        void StationPanel(float SH)
        {
            var s = _near; var job = s.id == "polka" ? null : CareerSim.JobAt(P, s.id);
            float W = 600, H = 360, x = 16, y = Mathf.Max(140, SH - 150 - H), lx = x + 16, inner = W - 32;
            Fill(new Rect(x, y, W, H), new Color(0.035f, 0.12f, 0.15f, 0.95f)); Fill(new Rect(x, y, 4, H), new Color(0.95f, 0.75f, 0.3f));
            float yy = y + 10;
            GUI.Label(new Rect(lx, yy, inner, 32), s.title, _title); yy += 36;
            var c = P.career;
            if (s.id == "polka")
            {
                GUI.Label(new Rect(lx, yy, inner, 24), "Spiżarka:", _text); yy += 26;
                for (int i = 0; i < GameData.ProductCount; i++) if (c.pantry[i] > 0) { GUI.Label(new Rect(lx, yy, inner, 22), $"• {GameData.P((Product)i).Name}: {c.pantry[i]} szt. (na rynku ok. {Money.Zl(CareerSim.StallPrice(P, (Product)i))} zł)", _small); yy += 22; }
                if (c.pantry.Sum() == 0) { GUI.Label(new Rect(lx, yy, inner, 22), "pusto", _small); yy += 22; }
                yy += 8; GUI.Label(new Rect(lx, yy, inner, 44), "<color=#9bb>Q albo F — zapakuj do koszyka na rowerze. Sprzedaż: F na rynku.</color>", _small);
                if (GUI.Button(new Rect(lx, y + H - 48, 260, 34), "Do koszyka (Q)")) G.Send(new LoadBasket(GameController.Me));
                return;
            }
            if (job != null)
            {
                var r = CareerData.R(job.recipe);
                GUI.Label(new Rect(lx, yy, inner, 24), $"{r.Name} ×{job.qty}", _text); yy += 26;
                for (int i = 0; i < r.Steps.Length; i++)
                {
                    var d = r.Steps[i];
                    string mark = i < job.step ? "<color=#7fe0a0>✔</color>" : i == job.step ? "<color=#ffd166>▶</color>" : "·";
                    string how = d.Kind == StepKind.Tap ? "Q" : d.Kind == StepKind.Hold ? "przytrzymaj Q" : "samo";
                    GUI.Label(new Rect(lx, yy, inner, 22), $"{mark} {d.Label} <color=#9fb>({how})</color>", _small); yy += 22;
                }
                if (!job.Done)
                {
                    var d = r.Steps[job.step]; double need = CareerSim.Need(d, job.qty);
                    float f = d.Kind == StepKind.Tap ? 0 : (float)(job.progress / need);
                    Fill(new Rect(lx, yy + 6, inner, 8), new Color(0.16f, 0.28f, 0.3f)); Fill(new Rect(lx, yy + 6, inner * Mathf.Clamp01(f), 8), new Color(0.95f, 0.75f, 0.3f));
                    yy += 22;
                    if (d.Kind == StepKind.Auto) { GUI.Label(new Rect(lx, yy, inner, 22), $"jeszcze ok. {Mathf.CeilToInt((float)(need - job.progress))} s — możesz w tym czasie robić coś innego", _small); yy += 22; }
                }
                GUI.Label(new Rect(lx, yy + 4, inner, 60), "<color=#9bb>" + r.Info + "</color>", _small);
                if (GUI.Button(new Rect(lx, y + H - 48, 220, 34), "Obsłuż (Q)")) Primary(true);
                return;
            }
            var list = RecipesAt(s.id).ToList();
            GUI.Label(new Rect(lx, yy, inner, 22), $"Lodówka: {c.fridge:0} l mleka · {c.cream:0.0} l śmietanki · partia do {CareerSim.MaxBatch(P)} szt.", _small); yy += 26;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                string need = r.Product < 0 ? $"{r.Milk:0} l mleka → {r.CreamOut:0.0} l śmietanki" : (r.Milk > 0 ? $"{r.Milk:0} l mleka" : $"{r.Cream:0.0} l śmietanki") + $" · {Money.Zl(CareerData.Price((Product)r.Product))} zł/szt.";
                bool locked = r.Needs != null && !c.owned.Contains(r.Needs);
                var prev = GUI.backgroundColor; if (i == _recipe) GUI.backgroundColor = new Color(0.95f, 0.75f, 0.3f);
                if (GUI.Button(new Rect(lx, yy, inner, 40), $"{i + 1}. {r.Name}" + (locked ? "  (zablokowane)" : "") + $"   <size=13>{need}</size>", new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, fontSize = 16 })) _recipe = i;
                GUI.backgroundColor = prev; yy += 44;
            }
            if (list.Count > 0)
            {
                var sel = list[Mathf.Clamp(_recipe, 0, list.Count - 1)];
                string block = CareerSim.StartBlock(P, sel, _qty);
                if (GUI.Button(new Rect(lx, y + H - 48, 40, 34), "−")) _qty = Mathf.Max(1, _qty - 1);
                GUI.Label(new Rect(lx + 48, y + H - 44, 160, 30), $"Partia: {_qty}", _text);
                if (GUI.Button(new Rect(lx + 170, y + H - 48, 40, 34), "+")) _qty = Mathf.Min(CareerSim.MaxBatch(P), _qty + 1);
                if (GUI.Button(new Rect(lx + inner - 220, y + H - 48, 220, 34), "Zacznij (Q)")) Primary(true);
                if (block != "") GUI.Label(new Rect(lx, y + H - 76, inner, 24), "<color=#ffb070>" + block + "</color>", _small);
            }
        }

        void ShopPanel(float SW, float SH)
        {
            float W = Mathf.Min(760, SW - 40), H = Mathf.Min(620, SH - 160), x = (SW - W) / 2, y = 140;
            Fill(new Rect(x, y, W, H), new Color(0.035f, 0.12f, 0.15f, 0.97f)); Fill(new Rect(x, y, W, 5), new Color(0.95f, 0.75f, 0.3f));
            var c = P.career;
            GUI.Label(new Rect(x + 18, y + 12, W - 36, 32), "Rozwój — od kuchni do mleczarni", _title);
            GUI.Label(new Rect(x + 18, y + 46, W - 36, 24), $"Masz {Money.Zl(P.money)} zł · sprzedano na straganie: {c.unitsSold} szt. za {Money.Zl(c.sold)} zł", _small);
            var view = new Rect(x + 12, y + 76, W - 24, H - 130);
            var items = CareerData.Items.Where(i => i.Price > 0).ToArray();
            _shopScroll = GUI.BeginScrollView(view, _shopScroll, new Rect(0, 0, view.width - 20, items.Length * 66));
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i]; bool own = c.owned.Contains(it.Id); bool req = it.Requires == null || c.owned.Contains(it.Requires);
                float yy = i * 66;
                Fill(new Rect(0, yy, view.width - 20, 60), own ? new Color(0.1f, 0.3f, 0.22f, 0.9f) : new Color(0.07f, 0.18f, 0.22f, 0.9f));
                GUI.Label(new Rect(10, yy + 4, view.width - 250, 26), $"<b>{it.Name}</b>  <color=#ffd166>{Money.Zl(it.Price)} zł</color>", _text);
                GUI.Label(new Rect(10, yy + 30, view.width - 250, 28), "<color=#bcd>" + it.Info + (req ? "" : $" Wymaga: {CareerData.Item(it.Requires).Name}.") + "</color>", _small);
                var bt = new Rect(view.width - 220, yy + 12, 190, 36);
                if (own) GUI.Label(bt, "<color=#7fe0a0>✔ masz</color>", _text);
                else
                {
                    GUI.enabled = req && P.money >= it.Price;
                    if (GUI.Button(bt, "Kup")) G.Send(new BuyCareerItem(GameController.Me, it.Id));
                    GUI.enabled = true;
                }
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(x + W - 200, y + H - 46, 180, 34), "Zamknij (K)")) ToggleShop(false);
        }
    }
}
