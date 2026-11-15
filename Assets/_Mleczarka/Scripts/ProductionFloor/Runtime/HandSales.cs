using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Sprzedaż „z ręki”:
    /// • rynek (kariera) — klienci podchodzą do straganu i mówią, czego chcą; bierzesz sztukę z koszyka roweru (F przy rowerze),
    ///   niesiesz ją w dłoni i podajesz klientowi (F przy kliencie);
    /// • placówki z zamówień (szkoła, sklep, restauracja, przedszkole, kawiarnia, hotel) — pod placówką wyjmujesz skrzynkę
    ///   z zamówieniem z dostawczaka (F przy dostawczaku) i zanosisz odbiorcy przy wejściu (F przy odbiorcy).
    /// Pieniądze i towar zmieniają się wyłącznie przez komendy symulacji; tu jest tylko prezentacja i obsługa klawisza F.
    /// </summary>
    public class HandSales : MonoBehaviour
    {
        [System.Serializable] public class Receiver { public int client; public Transform point; public string name; }

        public GameObject npcPrefab;
        public GameObject cartonModel, cupModel, cheeseModel, cheeseCutModel;
        public Transform[] marketSpots;
        public Receiver[] receivers;
        public float customerReach = 3.6f, bikeReach = 2.6f, vanReach = 4.5f;

        static readonly Product[] CareerGoods = { Product.Twarog, Product.MlekoPast, Product.Jogurt, Product.Kefir, Product.Smietana, Product.Maslo, Product.Ser };

        class Customer
        {
            public GameObject go; public Animator anim; public Product want; public int spot;
            public Vector3 target; public bool leaving, arrived; public float speed = 1.25f, idleUntil;
        }

        readonly List<Customer> _customers = new List<Customer>();
        readonly List<(Receiver r, GameObject npc)> _receiverNpcs = new List<(Receiver, GameObject)>();
        float[] _respawnAt;
        GameObject _handItem; int _handShown = -2;
        GUIStyle _bubble, _bubbleHot;
        CareerHome _home;
        static readonly int SpeedId = Animator.StringToHash("Speed"), GroundedId = Animator.StringToHash("Grounded");
        // kontroler postaci ma stan „w powietrzu” — bez tego NPC stałby w pozie spadania
        static void Grounded(Animator a) { foreach (var p in a.parameters) if (p.nameHash == GroundedId) { a.SetBool(GroundedId, true); return; } }

        GameController G => GameController.Instance;
        PlayerState P => G.Sim.S.players[GameController.Me];

        void Start()
        {
            if (!G) { enabled = false; return; }
            _home = FindFirstObjectByType<CareerHome>();
            _respawnAt = new float[marketSpots != null ? marketSpots.Length : 0];
            for (int i = 0; i < _respawnAt.Length; i++) _respawnAt[i] = Time.time + 1f + i * 2.5f;
            if (receivers != null)
                foreach (var r in receivers)
                {
                    if (!r.point || !npcPrefab) continue;
                    var npc = Spawn(r.point.position, r.point.rotation, 1000 + r.client);
                    npc.name = "Odbiorca: " + r.name;
                    _receiverNpcs.Add((r, npc));
                }
            GameController.HintHook = Hint;
            GameController.ActionHook = Action;
        }

        void OnDestroy()
        {
            if (GameController.HintHook == (System.Func<string>)Hint) GameController.HintHook = null;
            if (GameController.ActionHook == (System.Func<Result?>)Action) GameController.ActionHook = null;
        }

        // ---------- postacie ----------

        GameObject Spawn(Vector3 pos, Quaternion rot, int seed)
        {
            var go = Instantiate(npcPrefab, pos, rot);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rnd = new System.Random(seed);
            go.transform.localScale *= 0.94f + (float)rnd.NextDouble() * 0.1f;
            // różne kolory ubrań, żeby klienci nie byli identyczni
            var block = new MaterialPropertyBlock();
            var tint = Color.HSVToRGB((float)rnd.NextDouble(), 0.25f + (float)rnd.NextDouble() * 0.3f, 0.75f + (float)rnd.NextDouble() * 0.25f);
            // tylko ubranie (materiał „Peasant”), skóra i włosy bez zmian
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.sharedMaterial || !r.sharedMaterial.name.Contains("Peasant")) continue;
                r.GetPropertyBlock(block); block.SetColor("_BaseColor", tint); r.SetPropertyBlock(block);
            }
            var a = go.GetComponentInChildren<Animator>();
            if (a) { a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.applyRootMotion = false; Grounded(a); }
            return go;
        }

        Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Length > 0 ? hits.Where(h => !h.collider.GetComponentInParent<Animator>()).Select(h => h.point).DefaultIfEmpty(p).OrderBy(x => x.y).First() : p;
        }

        void Update()
        {
            if (!G || !G.player) return;
            bool career = CareerSim.Active(P);
            UpdateCustomers(career);
            UpdateHandItem(career);
        }

        void UpdateCustomers(bool career)
        {
            if (marketSpots == null) return;
            for (int i = 0; i < marketSpots.Length; i++)
            {
                bool taken = _customers.Any(c => c.spot == i && !c.leaving);
                if (career && !taken && Time.time >= _respawnAt[i] && npcPrefab && marketSpots[i]) SpawnCustomer(i);
            }
            foreach (var c in _customers.ToList())
            {
                if (!c.go) { _customers.Remove(c); continue; }
                var t = c.go.transform; var to = c.target - t.position; to.y = 0;
                float moving = 0;
                if (to.magnitude > 0.15f)
                {
                    var step = to.normalized * c.speed * Time.deltaTime;
                    t.position = Ground(t.position + step);
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
                    moving = c.speed;
                }
                else if (!c.leaving)
                {
                    c.arrived = true;
                    // czeka twarzą do straganu / gracza, gdy jest blisko
                    var look = G.player.transform.position - t.position; look.y = 0;
                    var face = look.magnitude < 6f ? look : marketSpots[c.spot].forward;
                    if (face.sqrMagnitude > 0.01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(face), Time.deltaTime * 3f);
                }
                else { Destroy(c.go); _customers.Remove(c); continue; }
                if (c.anim) { c.anim.SetFloat(SpeedId, moving, 0.15f, Time.deltaTime); Grounded(c.anim); }
            }
            if (!career) foreach (var c in _customers.Where(c => !c.leaving)) Leave(c);
            foreach (var (r, npc) in _receiverNpcs) { var a = npc ? npc.GetComponentInChildren<Animator>() : null; if (a) Grounded(a); }
        }

        void SpawnCustomer(int spot)
        {
            var s = marketSpots[spot];
            var from = Ground(s.position - s.forward * 10f + s.right * Random.Range(-6f, 6f));
            var go = Spawn(from, Quaternion.LookRotation(s.position - from), Random.Range(0, 100000));
            go.name = "Klient na rynku";
            var c = new Customer { go = go, anim = go.GetComponentInChildren<Animator>(), spot = spot, target = Ground(s.position), want = PickWish() };
            _customers.Add(c);
        }

        Product PickWish()
        {
            var c = P.career;
            // częściej to, co gracz ma w koszyku (i w ręku), ale czasem coś, czego jeszcze nie robi
            var have = CareerGoods.Where(g => c.basket[(int)g] > 0 || c.hand == (int)g).ToList();
            var taken = _customers.Where(x => !x.leaving).Select(x => x.want).ToList();
            var fresh = have.Where(h => !taken.Contains(h)).ToList();
            if (fresh.Count > 0 && Random.value < 0.8f) return fresh[Random.Range(0, fresh.Count)];
            if (have.Count > 0 && Random.value < 0.6f) return have[Random.Range(0, have.Count)];
            var cheap = new[] { Product.Twarog, Product.MlekoPast, Product.Jogurt, Product.Smietana };
            return cheap[Random.Range(0, cheap.Length)];
        }

        void Leave(Customer c)
        {
            c.leaving = true; c.speed = 1.4f;
            var s = marketSpots[c.spot];
            c.target = Ground(s.position - s.forward * 14f + s.right * Random.Range(-8f, 8f));
            _respawnAt[c.spot] = Time.time + Random.Range(6f, 14f);
        }

        // ---------- towar w dłoni ----------

        void UpdateHandItem(bool career)
        {
            int want = career && !G.player.CurrentVehicle && !G.player.OnBike ? P.career.hand : -1;
            if (want != _handShown)
            {
                if (_handItem) Destroy(_handItem);
                _handShown = want;
                if (want >= 0)
                {
                    var src = ModelFor((Product)want);
                    if (src)
                    {
                        _handItem = Instantiate(src);
                        foreach (var col in _handItem.GetComponentsInChildren<Collider>()) Destroy(col);
                        var b = Bounds(_handItem); float k = 0.16f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                        _handItem.transform.localScale *= k;
                    }
                }
            }
            if (!_handItem) return;
            var anim = G.player.GetComponentInChildren<Animator>();
            var hand = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var t = G.player.transform;
            var pos = hand ? hand.position + t.forward * 0.06f : t.position + Vector3.up * 1.0f + t.forward * 0.35f + t.right * 0.2f;
            _handItem.transform.rotation = Quaternion.LookRotation(t.forward);
            var bb = Bounds(_handItem);
            _handItem.transform.position += pos - new Vector3(bb.center.x, bb.min.y, bb.center.z);
        }

        GameObject ModelFor(Product p)
        {
            switch (p)
            {
                case Product.Ser: return cheeseModel ? cheeseModel : cheeseCutModel;
                case Product.Twarog: case Product.Maslo: return cheeseCutModel ? cheeseCutModel : cheeseModel;
                case Product.Jogurt: case Product.Smietana: return cupModel ? cupModel : cartonModel;
                default: return cartonModel;
            }
        }

        static Bounds Bounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.1f);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ---------- F: co jest najbliżej ----------

        bool OnFoot => G.player && !G.player.CurrentVehicle && !G.player.OnBike;
        float Dist(Vector3 a) { var d = a - G.player.transform.position; d.y = 0; return d.magnitude; }

        Customer NearCustomer() => _customers.Where(c => c.go && c.arrived && !c.leaving && Dist(c.go.transform.position) < customerReach)
                                             .OrderBy(c => Dist(c.go.transform.position)).FirstOrDefault();

        bool NearBike => _home && _home.bike && _home.bike.gameObject.activeInHierarchy && Dist(_home.bike.position) < bikeReach;

        Receiver NearReceiver() => receivers?.Where(r => r.point && Dist(r.point.position) < customerReach + 0.4f).OrderBy(r => Dist(r.point.position)).FirstOrDefault();

        WheeledVehicle NearVan()
        {
            foreach (var v in FindObjectsByType<WheeledVehicle>(FindObjectsSortMode.None))
            {
                var asm = v.GetComponent<VehicleAssembler>();
                if (asm && asm.loadout.type == VehicleType.Dostawczak && Dist(v.transform.position) < vanReach) return v;
            }
            return null;
        }

        Product BestFromBasket()
        {
            var c = P.career; var near = NearCustomer() ?? _customers.Where(x => x.go && !x.leaving).OrderBy(x => Dist(x.go.transform.position)).FirstOrDefault();
            if (near != null && c.basket[(int)near.want] > 0) return near.want;
            foreach (var g in _customers.Where(x => !x.leaving).Select(x => x.want)) if (c.basket[(int)g] > 0) return g;
            for (int i = 0; i < GameData.ProductCount; i++) if (c.basket[i] > 0) return (Product)i;
            return Product.Twarog;
        }

        OrderState OrderFor(int client) => G.Sim.ClaimedBy(GameController.Me).FirstOrDefault(o => Simulation.ClientIndex(o.client) == client);

        string Hint()
        {
            if (!OnFoot) return null;
            var p = P; bool career = CareerSim.Active(p);
            // placówki: odbiorca i dostawczak
            var place = G.PlaceOf(GameController.Me);
            if (place.Kind == PlaceKind.Placowka)
            {
                var rec = NearReceiver();
                if (rec != null)
                {
                    if (p.carriedOrder >= 0) return $"F — przekaż zamówienie ({rec.name})";
                    var o = OrderFor(rec.client);
                    return o != null ? $"{rec.name}: czekają na zamówienie — wyjmij skrzynkę z dostawczaka (F przy dostawczaku)" : $"{rec.name}: nie masz przyjętego zamówienia od tej placówki (O — zamówienia)";
                }
                if (NearVan())
                {
                    if (p.carriedOrder >= 0) return "F — odłóż skrzynkę z powrotem do dostawczaka";
                    var o = OrderFor(place.FarmId);
                    if (o != null) return $"F — wyjmij z dostawczaka zamówienie: {o.client}";
                }
            }
            if (!career) return null;
            var cust = NearCustomer();
            if (cust != null)
            {
                string w = GameData.P(cust.want).Name;
                if (p.career.hand == (int)cust.want) return $"F — podaj klientowi: {w} ({Money.Zl(Money.M2(CareerSim.StallPrice(p, cust.want) * 1.1))} zł)";
                if (p.career.hand >= 0) return $"Klient chce: {w}. Masz: {GameData.P((Product)p.career.hand).Name} — odłóż do koszyka (F przy rowerze)";
                return $"Klient chce: {w} — weź z koszyka roweru (F przy rowerze)";
            }
            if (NearBike && place.Kind != PlaceKind.Dom)
            {
                if (p.career.hand >= 0) return $"F — odłóż do koszyka: {GameData.P((Product)p.career.hand).Name}";
                if (p.career.basket.Sum() > 0) return $"F — weź z koszyka: {GameData.P(BestFromBasket()).Name}   ·   E — wsiądź na rower";
            }
            return null;
        }

        Result? Action()
        {
            if (!OnFoot) return null;
            var p = P; var place = G.PlaceOf(GameController.Me);
            if (place.Kind == PlaceKind.Placowka)
            {
                var rec = NearReceiver();
                if (rec != null)
                {
                    if (p.carriedOrder >= 0) return G.Send(new HandOverOrder(GameController.Me));
                    return Result.Fail(OrderFor(rec.client) != null ? "Najpierw wyjmij skrzynkę z dostawczaka (F przy dostawczaku)." : "Nie masz zamówienia od tej placówki.");
                }
                if (NearVan())
                {
                    if (p.carriedOrder >= 0) return G.Send(new ReturnOrderToVan(GameController.Me));
                    var o = OrderFor(place.FarmId);
                    if (o != null) return G.Send(new CarryOrder(GameController.Me, o.id));
                }
            }
            if (!CareerSim.Active(p)) return null;
            var cust = NearCustomer();
            if (cust != null)
            {
                var r = G.Send(new HandSell(GameController.Me, cust.want));
                if (r.Ok) Leave(cust);
                return r;
            }
            if (NearBike && place.Kind != PlaceKind.Dom)
            {
                if (p.career.hand >= 0) return G.Send(new ReturnHand(GameController.Me));
                if (p.career.basket.Sum() > 0) return G.Send(new TakeFromBasket(GameController.Me, BestFromBasket()));
            }
            return null;
        }

        // ---------- dymki ----------

        void OnGUI()
        {
            if (!G || !G.player || !Camera.main) return;
            if (_bubble == null)
            {
                int fs = Mathf.RoundToInt(16 * Mathf.Clamp(Screen.width / 1500f, 0.8f, 1.6f));
                _bubble = new GUIStyle(GUI.skin.box) { fontSize = fs, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 5, 5), richText = true };
                _bubbleHot = new GUIStyle(_bubble) { fontStyle = FontStyle.Bold };
            }
            var near = NearCustomer();
            foreach (var c in _customers)
                if (c.go && c.arrived && !c.leaving && Dist(c.go.transform.position) < 22f)
                    Bubble(c.go.transform.position + Vector3.up * 2.15f, "Poproszę: " + GameData.P(c.want).Name, c == near);
            var p = P;
            foreach (var (r, npc) in _receiverNpcs)
            {
                if (!npc || Dist(npc.transform.position) > 18f) continue;
                var o = OrderFor(r.client);
                if (o != null) Bubble(npc.transform.position + Vector3.up * 2.15f, $"{r.name}: czekamy na " + string.Join(", ", Enumerable.Range(0, o.items.Length).Where(i => o.items[i] > 0).Select(i => $"{o.items[i]}× {GameData.Products[i].Name}")), p.carriedOrder == o.id);
            }
        }

        void Bubble(Vector3 world, string text, bool hot)
        {
            var sp = Camera.main.WorldToScreenPoint(world); if (sp.z < 0.5f) return;
            var st = hot ? _bubbleHot : _bubble; var size = st.CalcSize(new GUIContent(text));
            var rect = new Rect(sp.x - size.x / 2, Screen.height - sp.y - size.y, size.x, size.y);
            var old = GUI.color; if (hot) GUI.color = new Color(1f, 0.95f, 0.6f);
            GUI.Box(rect, text, st); GUI.color = old;
        }
    }
}
