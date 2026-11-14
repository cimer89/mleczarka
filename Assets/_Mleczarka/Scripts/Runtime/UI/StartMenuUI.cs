using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Mleczarka.UI.UiKit;

namespace Mleczarka.UI
{
    /// <summary>
    /// Menu startowe i pauza: Kontynuuj, Nowa gra (misja albo tryb swobodny, z kopią starego zapisu), Sterowanie, Wyjście.
    /// Pojawia się po uruchomieniu gry; w trakcie gry otwiera je Escape (gdy żaden panel nie jest otwarty).
    /// Dodaje się samo do każdej sceny z GameController — nie trzeba zmieniać sceny.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StartMenuUI : MonoBehaviour
    {
        GameController G => GameController.Instance;
        VisualElement _root, _overlay;
        bool _open, _blockedByMe, _firstShow = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Attach();
        static void Attach()
        {
            if (!Object.FindFirstObjectByType<GameController>() || Object.FindFirstObjectByType<StartMenuUI>()) return;
            var any = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).FirstOrDefault(d => d.panelSettings);
            if (!any) return;
            var go = new GameObject("Menu gry (UI)");
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = any.panelSettings; doc.sortingOrder = 50;
            go.AddComponent<StartMenuUI>();
        }

        void OnEnable() { _root = GetComponent<UIDocument>().rootVisualElement; _root.Clear(); _root.pickingMode = PickingMode.Ignore; }

        void Start() { Open(); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !G) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_open) { if (!_firstShow) Close(); }
                else if (G.player && !_busyLastFrame) Open();
            }
        }

        bool _busyLastFrame;
        // stan z poprzedniej klatki: Escape, który właśnie zamknął panel albo mapę, nie otwiera od razu menu
        void LateUpdate() => _busyLastFrame = !_open && G && G.player && (G.player.InputBlocked || MapUI.BigMapOpen);

        void Open()
        {
            _open = true;
            Time.timeScale = 0f;
            if (G && G.player && !G.player.InputBlocked) { G.player.orbitWhenBlocked = false; G.player.InputBlocked = true; _blockedByMe = true; }
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            Main();
        }

        public bool IsOpen => _open;
        public void CloseMenu() { if (_open) Close(); }

        void Close()
        {
            _open = false; _firstShow = false;
            _overlay?.RemoveFromHierarchy(); _overlay = null;
            Time.timeScale = 1f;
            if (_blockedByMe && G && G.player) { G.player.InputBlocked = false; G.player.orbitWhenBlocked = true; }
            _blockedByMe = false;
        }

        VisualElement Shell(string title, string subtitle)
        {
            _overlay?.RemoveFromHierarchy();
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute; _overlay.style.left = 0; _overlay.style.right = 0; _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.06f, 0.09f, 0.12f, _firstShow ? 0.78f : 0.6f);
            _overlay.style.alignItems = Align.Center; _overlay.style.justifyContent = Justify.Center;
            var card = Card(Panel, 26, 24, 30); card.style.width = 560; card.style.borderBottomWidth = 8; card.style.borderBottomColor = Brand;
            var t = L(title, 44, Brand, true); t.style.unityTextAlign = TextAnchor.MiddleCenter; card.Add(t);
            if (!string.IsNullOrEmpty(subtitle)) { var st = L(subtitle, 17, Muted); st.style.unityTextAlign = TextAnchor.MiddleCenter; st.style.whiteSpace = WhiteSpace.Normal; st.style.marginBottom = 14; card.Add(st); }
            _overlay.Add(card); _root.Add(_overlay);
            return card;
        }

        Button Big(VisualElement parent, string text, System.Action a, Color bg, Color fg)
        {
            var b = Btn(text, a, bg, fg, 21, 52); b.style.marginTop = 8; b.style.marginLeft = 0; b.style.marginRight = 0; parent.Add(b); return b;
        }

        string SaveInfo()
        {
            if (!G) return "";
            var S = G.Sim.S; var p = S.players[GameController.Me]; var c = G.Sim.Clock();
            string mode = S.mission.active ? (S.mission.over ? "misja zakończona" : "misja") : "tryb swobodny";
            return $"Zapis: dzień {c.day}, {c.text} · {Money.Zl(p.money)} zł · ★ {p.rep} · {mode}";
        }

        void Main()
        {
            bool hasProgress = G && G.Sim.S.t > 1 && !G.StartedFresh;
            var card = Shell("Mleczarka", _firstShow ? "Gmina Zabielsk · zbieraj mleko, prowadź mleczarnię, dowoź towar na rynek" : "Pauza");
            if (G) { var info = L(SaveInfo(), 15, Ink, true); info.style.unityTextAlign = TextAnchor.MiddleCenter; info.style.marginBottom = 6; card.Add(info); }
            Big(card, _firstShow ? (hasProgress ? "Kontynuuj grę" : "Graj") : "Wróć do gry", Close, Good, Color.white);
            Big(card, "Nowa gra od początku", NewGameChoice, Brand, Color.white);
            Big(card, "Sterowanie", Controls, Chip, Ink);
            if (G && G.Sim.S.players[GameController.Me].plant != null)
            {
                bool off = G.Sim.S.players[GameController.Me].plant.workersOff;
                Big(card, off ? "Pracownicy w hali: wyłączeni (włącz)" : "Pracownicy w hali: włączeni (wyłącz)",
                    () => { G.Send(new SetWorkersOff(GameController.Me, !off)); Main(); }, Chip, Ink);
            }
            Big(card, "Wyjście z gry", Quit, new Color(0.85f, 0.85f, 0.85f), Ink);
            var hint = L(_firstShow ? "" : "Escape — wróć do gry", 13, Muted); hint.style.unityTextAlign = TextAnchor.MiddleCenter; hint.style.marginTop = 10; card.Add(hint);
        }

        void NewGameChoice()
        {
            var card = Shell("Nowa gra", "Zaczynasz od zera: pieniądze, umowy, mleczarnia i pojazdy wracają do stanu początkowego.");
            Big(card, "Misja: pierwszy dzień mleczarni", () => Confirm(GameMode.Misja), Brand, Color.white);
            Big(card, "Tryb swobodny", () => Confirm(GameMode.Swobodna), Good, Color.white);
            Big(card, "Wstecz", Main, Chip, Ink);
        }

        void Confirm(GameMode mode)
        {
            bool exists = G && SaveSystem.Exists(G.saveSlot);
            if (!exists) { G.RestartFromScratch(mode); return; }
            var card = Shell("Zacząć od nowa?", "Obecny zapis nie zostanie skasowany: trafi do kopii z datą w folderze zapisów gry. Gra uruchomi się od początku.");
            var info = L(SaveInfo(), 15, Ink, true); info.style.unityTextAlign = TextAnchor.MiddleCenter; card.Add(info);
            Big(card, "Tak, nowa gra", () => G.RestartFromScratch(mode), Bad, Color.white);
            Big(card, "Nie, wróć", NewGameChoice, Chip, Ink);
        }

        void Controls()
        {
            var card = Shell("Sterowanie", null);
            string[] rows =
            {
                "WASD — chodzenie i jazda · Shift — bieg · Spacja — skok / hamulec ręczny",
                "Mysz — kamera (kliknij, aby złapać kursor; Escape go zwalnia)",
                "E — wsiądź / wysiądź · R — postaw pojazd na koła",
                "F — akcja w miejscu: umowa z gospodarstwem, pompowanie mleka, rozładunek, sprzedaż na rynku",
                "W hali: Q — obsługa maszyny (przytrzymaj przy dłuższych krokach), C — mycie CIP, X — przegląd / naprawa",
                "Przy przyjęciu mleka: 1–8 — produkt, − / + — wielkość partii, Q — start partii",
                "TAB — zarządzanie mleczarnią · O — zamówienia · M — mapa · G — garaż przy pojeździe",
                "Escape — menu i pauza",
            };
            foreach (var r in rows) { var l = L(r, 16, Ink); l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 6; card.Add(l); }
            Big(card, "Wstecz", Main, Chip, Ink);
        }

        void Quit()
        {
            if (G) G.SaveGame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy() { if (_open) Time.timeScale = 1f; }
    }
}
