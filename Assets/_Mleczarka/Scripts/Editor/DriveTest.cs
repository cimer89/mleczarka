using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Gameplay;
using Mleczarka.Vehicles;
using Mleczarka.World;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Jazda testowa w trybie gry: cysterna jedzie sama wybraną drogą (autopilot trzyma się osi drogi i ogranicza prędkość na zakrętach).
    /// Mierzy czas i średnią prędkość, wywrotki i utknięcia — sprawdzenie, czy przejazdy trwają tyle, ile zakłada mapa.
    /// </summary>
    public static class DriveTest
    {
        static MapInfo.RoadPath _road; static WheeledVehicle _veh; static double _t0, _last; static int _idx; static float _dist, _maxTilt, _maxSpeed;
        static Vector3 _prev; static StringBuilder _log; static int _stuck; static bool _reverse;

        [MenuItem("Mleczarka/Testy/Jazda testowa: Mleczarnia → Olszanka", priority = 53)]
        static void RunA() => Run("Mleczarnia – Olszanka", false);
        [MenuItem("Mleczarka/Testy/Jazda testowa: Zabielsk → Zarzecze (most)", priority = 54)]
        static void RunB() => Run("Zabielsk – Zarzecze", false);

        [MenuItem("Mleczarka/Testy/Zrzut widoku gry", priority = 55)]
        static void GameShot()
        {
            Directory.CreateDirectory("Screenshots");
            string n = "Screenshots/gra_" + System.DateTime.Now.ToString("HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(n);
            Debug.Log("[Mleczarka] Zrzut widoku gry: " + n);
        }

        static void Run(string road, bool reverse)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Mleczarka] Uruchom tryb gry w scenie Gmina_Podlasie."); return; }
            var gc = GameController.Instance; var map = MapInfo.Instance;
            if (!gc || !map) { Debug.LogWarning("[Mleczarka] Brak GameController albo MapInfo."); return; }
            _road = map.roads.FirstOrDefault(r => r.name == road); if (_road == null) { Debug.LogWarning("Brak drogi " + road); return; }
            _reverse = reverse;
            _veh = GameObject.Find("Cysterna gracza").GetComponent<WheeledVehicle>();
            var pts = Pts();
            var dir = (pts[3] - pts[0]); dir.y = 0;
            var rb = _veh.GetComponent<Rigidbody>();
            _veh.transform.SetPositionAndRotation(pts[0] + Vector3.up * 1.2f, Quaternion.LookRotation(dir.normalized));
            rb.position = _veh.transform.position; rb.rotation = _veh.transform.rotation; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            if (gc.player.CurrentVehicle) gc.player.EnterOrExit();
            gc.player.transform.position = _veh.transform.position + _veh.transform.right * -2f;
            gc.player.EnterOrExit();
            _idx = 0; _dist = 0; _maxTilt = 0; _maxSpeed = 0; _stuck = 0; _prev = _veh.transform.position;
            _t0 = EditorApplication.timeSinceStartup; _last = _t0;
            _log = new StringBuilder($"[Mleczarka] Jazda testowa: {road} ({Length(pts) / 1000f:0.00} km), cysterna, autopilot do 70 km/h\n");
            gc.player.autopilot = Steer;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static Vector3[] Pts() => _reverse ? _road.points.Reverse().ToArray() : _road.points;
        static float Length(Vector3[] p) { float s = 0; for (int i = 1; i < p.Length; i++) s += Vector3.Distance(p[i - 1], p[i]); return s; }

        static Vector3 Steer()
        {
            var pts = Pts(); var t = _veh.transform; var pos = t.position;
            // najbliższy punkt przed nami
            for (int i = _idx; i < Mathf.Min(pts.Length, _idx + 12); i++) if ((pts[i] - pos).sqrMagnitude < (pts[_idx] - pos).sqrMagnitude) _idx = i;
            float speed = _veh.SpeedKmh;
            int look = Mathf.Clamp(_idx + 2 + Mathf.RoundToInt(speed / 25f), 0, pts.Length - 1);
            var target = pts[look]; var to = target - pos; to.y = 0;
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(t.forward, Vector3.up), to, Vector3.up);
            // ograniczenie prędkości według krzywizny 40 m przed nami
            int ahead = Mathf.Min(pts.Length - 1, _idx + 5);
            var a = pts[Mathf.Min(pts.Length - 1, _idx + 1)] - pts[_idx]; var b = pts[ahead] - pts[Mathf.Max(0, ahead - 1)];
            float bend = Vector3.Angle(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
            float limit = Mathf.Lerp(70f, 32f, Mathf.InverseLerp(5f, 45f, bend));
            float throttle = speed < limit ? 1f : speed > limit + 6f ? -0.6f : 0.15f;
            return new Vector3(throttle, Mathf.Clamp(angle / 28f, -1f, 1f), 0f);
        }

        static void Tick()
        {
            if (!Application.isPlaying || !_veh) { Stop("przerwano (koniec trybu gry)"); return; }
            double now = EditorApplication.timeSinceStartup, el = now - _t0;
            var pos = _veh.transform.position; _dist += Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(_prev.x, 0, _prev.z)); _prev = pos;
            _maxTilt = Mathf.Max(_maxTilt, Vector3.Angle(_veh.transform.up, Vector3.up)); _maxSpeed = Mathf.Max(_maxSpeed, _veh.SpeedKmh);
            if (now - _last >= 10) { _last = now; _log.AppendLine($"  {el,5:0} s: {_dist:0} m, {_veh.SpeedKmh:0} km/h, punkt {_idx}/{_road.points.Length}"); if (_veh.SpeedKmh < 3) _stuck++; else _stuck = 0; }
            if (_maxTilt > 70f) { Stop("WYWROTKA"); return; }
            if (_stuck >= 2) { Stop("UTKNĘŁA"); return; }
            if (_idx >= _road.points.Length - 2) { Stop("dojechała"); return; }
            if (el > 600) Stop("limit 10 min");
        }

        static void Stop(string why)
        {
            EditorApplication.update -= Tick;
            var gc = GameController.Instance; if (gc && gc.player) gc.player.autopilot = null;
            double el = EditorApplication.timeSinceStartup - _t0;
            _log.AppendLine($"Wynik: {why}. Czas {el:0} s ({el / 60:0.0} min), przejechane {_dist:0} m, średnio {_dist / Mathf.Max(1f, (float)el) * 3.6f:0} km/h, maks. {_maxSpeed:0} km/h, maks. przechył {_maxTilt:0}°.");
            Debug.Log(_log.ToString());
            Directory.CreateDirectory("Screenshots");
            File.AppendAllText("Screenshots/jazda_test.txt", _log + "\n");
        }
    }
}
