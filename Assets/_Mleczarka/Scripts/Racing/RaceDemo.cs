using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Mleczarka.Vehicles;

namespace Mleczarka.Racing
{
    /// <summary>Osobna scena testowa wyścigu. Nie odczytuje ani nie nadpisuje zapisu kariery.</summary>
    public sealed class RaceDemo : MonoBehaviour
    {
        public WheeledVehicle playerVehicle, rivalVehicle;
        public Camera raceCamera;
        public Vector3[] route;
        public int laps = 2;
        public float gateRadius = 9f;
        public float aiSpeedKmh = 46f;
        RaceRules playerRace, rivalRace;
        Vector3 lastPlayer, lastRival, playerStart, rivalStart;
        Quaternion startRotation;
        bool winnerKnown, playerWon;
        GUIStyle text;
        GameObject marker;

        void Start()
        {
            if (!playerVehicle || !rivalVehicle || !raceCamera || route == null || route.Length < 3)
            { Debug.LogError("[Mleczarka] Brak konfiguracji wyścigu."); enabled = false; return; }
            playerStart = playerVehicle.transform.position; rivalStart = rivalVehicle.transform.position;
            startRotation = playerVehicle.transform.rotation;
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Następny punkt kontrolny";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = new Vector3(1.5f, 7f, 1.5f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader) { var mat = new Material(shader); mat.color = Color.yellow; marker.GetComponent<Renderer>().material = mat; }
            Restart();
        }

        void Restart()
        {
            playerRace = new RaceRules(route.Length, laps, 3);
            rivalRace = new RaceRules(route.Length, laps, 3);
            ResetVehicle(playerVehicle, playerStart); ResetVehicle(rivalVehicle, rivalStart);
            lastPlayer = playerStart; lastRival = rivalStart; winnerKnown = false; tied = false;
            playerVehicle.HasDriver = rivalVehicle.HasDriver = true;
        }

        void ResetVehicle(WheeledVehicle vehicle, Vector3 position)
        {
            var rb = vehicle.GetComponent<Rigidbody>();
            rb.position = position; rb.rotation = startRotation;
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            vehicle.transform.SetPositionAndRotation(position, startRotation);
            vehicle.SetInput(0, 0, true);
        }

        void Update()
        {
            if (playerRace == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) playerVehicle.ResetUpright();
            marker.SetActive(playerRace.Phase != RacePhase.Finished);
            marker.transform.position = route[playerRace.NextCheckpoint] + Vector3.up * 7f;
        }

        void FixedUpdate()
        {
            if (playerRace == null) return;
            playerRace.Tick(Time.fixedDeltaTime); rivalRace.Tick(Time.fixedDeltaTime);
            if (playerRace.Phase == RacePhase.Running)
            {
                var kb = Keyboard.current; var pad = Gamepad.current;
                float throttle = kb == null ? 0 : ((kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0));
                float steer = kb == null ? 0 : ((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0));
                if (pad != null) { throttle += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue(); steer += pad.leftStick.x.ReadValue(); }
                playerVehicle.SetInput(throttle, steer, (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed));
            }
            else playerVehicle.SetInput(0, 0, true);

            if (rivalRace.Phase == RacePhase.Running)
            {
                var to = route[rivalRace.NextCheckpoint] - rivalVehicle.transform.position; to.y = 0;
                float angle = Vector3.SignedAngle(rivalVehicle.transform.forward, to, Vector3.up);
                float targetSpeed = Mathf.Lerp(aiSpeedKmh, 18f, Mathf.InverseLerp(10, 70, Mathf.Abs(angle)));
                float gas = rivalVehicle.SpeedKmh < targetSpeed ? 1f : rivalVehicle.SpeedKmh > targetSpeed + 4 ? -0.4f : 0.1f;
                rivalVehicle.SetInput(gas, Mathf.Clamp(angle / 28f, -1, 1), false);
            }
            else rivalVehicle.SetInput(0, 0, true);

            bool playerFinished = Advance(playerRace, lastPlayer, playerVehicle.transform.position);
            bool rivalFinished = Advance(rivalRace, lastRival, rivalVehicle.transform.position);
            lastPlayer = playerVehicle.transform.position; lastRival = rivalVehicle.transform.position;
            // Gdy obaj kończą w jednym kroku fizyki, pokazujemy remis zamiast fałszywej wygranej.
            if (!winnerKnown && (playerFinished || rivalFinished))
            { winnerKnown = true; playerWon = playerFinished && !rivalFinished; tied = playerFinished && rivalFinished; }
        }
        bool tied;

        bool Advance(RaceRules rules, Vector3 from, Vector3 to)
        {
            if (rules.Phase != RacePhase.Running) return false;
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 point = route[rules.NextCheckpoint];
                Vector3 delta = to - from;
                float u = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(point - from, delta) / delta.sqrMagnitude) : 0;
                if (Vector3.Distance(point, from + delta * u) > gateRadius) break;
                rules.PassCheckpoint(rules.NextCheckpoint);
                if (rules.Phase == RacePhase.Finished) return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (!raceCamera || !playerVehicle) return;
            var t = playerVehicle.transform;
            var goal = t.position - t.forward * 13 + Vector3.up * 7;
            raceCamera.transform.position = Vector3.Lerp(raceCamera.transform.position, goal, 1 - Mathf.Exp(-5 * Time.deltaTime));
            raceCamera.transform.LookAt(t.position + Vector3.up * 1.5f + t.forward * 4);
        }

        void OnGUI()
        {
            if (playerRace == null) return;
            if (text == null) text = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            GUI.Box(new Rect(16, 16, 580, 230), "");
            string status = playerRace.Phase == RacePhase.Countdown ? "START ZA " + Math.Ceiling(playerRace.Countdown) :
                playerRace.Phase == RacePhase.Finished ? "META · " + (tied ? "Remis" : playerWon ? "Wygrywasz!" : "Wygrywa rywal") : "WYŚCIG";
            int place = playerRace.PassedCount >= rivalRace.PassedCount ? 1 : 2;
            GUI.Label(new Rect(32, 28, 550, 200), status + "\nOkrążenie " + Math.Min(laps, playerRace.CompletedLaps + 1) + "/" + laps +
                " · Czas " + playerRace.Elapsed.ToString("0.0") + " s · Pozycja " + place + "/2" +
                "\nWASD / strzałki · Spacja: hamulec\nR: restart · Backspace: postaw na kołach\nPrototyp: bez nagród i zmian zapisu kariery.", text);
        }
        void OnDestroy()
        {
            if (playerVehicle) { playerVehicle.SetInput(0, 0, true); playerVehicle.HasDriver = false; }
            if (rivalVehicle) { rivalVehicle.SetInput(0, 0, true); rivalVehicle.HasDriver = false; }
        }
    }
}
