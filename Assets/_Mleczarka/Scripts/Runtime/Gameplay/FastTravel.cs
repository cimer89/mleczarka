using Mleczarka.Core;
using UnityEngine;

namespace Mleczarka.Gameplay
{
    /// <summary>
    /// Szybki transport z dużej mapy: gracz (pieszo, na rowerze albo w pojeździe) przenosi się do wybranego miejsca,
    /// a czas gry przeskakuje o tyle, ile trwałaby podróż. Wolniejszy środek transportu = większy przeskok
    /// (pieszo &gt; rower &gt; dostawczak/cysterna). W tym czasie świat żyje dalej: gospodarstwa i krowy dają mleko,
    /// garnki się gotują, zamówienia mijają.
    /// </summary>
    public static class FastTravel
    {
        /// <summary>Drogi nie biegną prosto — trasa jest dłuższa od linii prostej.</summary>
        public const float RoadFactor = 1.3f;
        public const float MinDistance = 60f;

        public enum Mode { Pieszo, Rower, Pojazd }

        public static Mode Current(GameController g) =>
            g.player && g.player.CurrentVehicle ? Mode.Pojazd : g.player && g.player.OnBike ? Mode.Rower : Mode.Pieszo;

        /// <summary>Średnia prędkość podróży (m/s): jak w grze — bieg, jazda rowerem, pojazd ok. 70% prędkości maksymalnej.</summary>
        public static float Speed(GameController g)
        {
            var pl = g.player; if (!pl) return 4f;
            switch (Current(g))
            {
                case Mode.Pojazd: return Mathf.Max(8f, pl.CurrentVehicle.maxSpeedKmh / 3.6f * 0.7f);
                case Mode.Rower: return Mathf.Max(3f, pl.bikeSpeed);
                default: return Mathf.Max(2f, pl.runSpeed);
            }
        }

        public static string ModeName(Mode m) => m == Mode.Pojazd ? "pojazdem" : m == Mode.Rower ? "rowerem" : "pieszo";

        public static float Distance(GameController g, WorldPlace p)
        {
            var d = p.transform.position - g.PlayerPosition; d.y = 0; return d.magnitude;
        }

        /// <summary>Czas podróży w sekundach symulacji (1 s = 1 minuta gry).</summary>
        public static double TravelSeconds(GameController g, WorldPlace p) => Distance(g, p) * RoadFactor / Speed(g);

        public static string Duration(double simSeconds)
        {
            double gameMinutes = simSeconds * 1440.0 / GameConfig.DaySeconds;
            int h = (int)(gameMinutes / 60), m = (int)System.Math.Round(gameMinutes - h * 60);
            if (m == 60) { h++; m = 0; }
            return h > 0 ? $"{h} godz. {m} min" : $"{System.Math.Max(1, m)} min";
        }

        public static string CanGo(GameController g, WorldPlace p)
        {
            if (!g || !g.player || !p) return "Brak gracza albo celu.";
            if (Distance(g, p) < MinDistance) return "Jesteś już na miejscu.";
            if (g.Sim.S.players[GameController.Me].carriedOrder >= 0) return "Najpierw odłóż skrzynkę z zamówieniem do dostawczaka.";
            return null;
        }

        public static Result Go(GameController g, WorldPlace p)
        {
            var why = CanGo(g, p); if (why != null) return Result.Fail(why);
            var mode = Current(g); float dist = Distance(g, p); double secs = TravelSeconds(g, p);
            var dest = p.transform.position;
            var from = g.PlayerPosition; var dir = dest - from; dir.y = 0; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (mode == Mode.Pojazd)
            {
                // pojazd staje na drodze najbliżej celu, przodem do niego
                var road = RoadNear(dest, 45f) ?? dest;
                var to = dest - road; to.y = 0; if (to.sqrMagnitude > 1f) rot = Quaternion.LookRotation(to.normalized, Vector3.up);
                g.player.CurrentVehicle.PlaceAt(Ground(road, g.player.CurrentVehicle.transform) + Vector3.up * 1.2f, rot);
            }
            else
            {
                var cc = g.player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                g.player.transform.SetPositionAndRotation(Ground(dest, g.player.transform) + Vector3.up * 0.15f, rot);
                if (cc) cc.enabled = true;
                Physics.SyncTransforms();
            }
            g.SkipTime(secs);
            GameAudio.Play("Szybki_Transport");
            return Result.Success($"Szybki transport {ModeName(mode)} do: {p.displayName} — {dist / 1000f:0.0} km, minęło {Duration(secs)}.");
        }

        static Vector3 Ground(Vector3 p, Transform ignore)
        {
            Vector3 best = p; float bd = float.MaxValue;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 90f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(ignore) && h.distance < bd) { bd = h.distance; best = h.point; }
            return best;
        }

        /// <summary>Najbliższy punkt drogi (odcinki dróg mają kolizje o nazwie „odcinek…”).</summary>
        static Vector3? RoadNear(Vector3 c, float max)
        {
            for (float r = 0f; r <= max; r += 3f)
            {
                Vector3? best = null; float bd = 1e9f;
                for (int a = 0; a < 360; a += r < 1f ? 360 : 15)
                {
                    var q = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    foreach (var h in Physics.RaycastAll(q + Vector3.up * 40f, Vector3.down, 90f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider.name.StartsWith("odcinek")) { var d = h.point - c; d.y = 0; if (d.magnitude < bd) { bd = d.magnitude; best = h.point; } }
                }
                if (best != null) return best;
            }
            return null;
        }
    }
}
