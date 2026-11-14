using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Save;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mleczarka.ProductionFloor
{
    public static class ProductionSceneChecks
    {
        static int count;
        static void Check(bool result, string name)
        { if (!result) throw new Exception("FAIL: " + name); count++; }
        static void Position(GameController gc, Vector3 position)
        {
            var cc = gc.player.GetComponent<CharacterController>(); cc.enabled = false;
            gc.player.transform.position = position + Vector3.up * 0.2f; cc.enabled = true;
        }
        static void Go(GameController gc, ProductionFloor floor, FloorAction action)
        { Position(gc, floor.stations.First(s => s.action == action).transform.position); }

        [MenuItem("Mleczarka/Produkcja/4. Test calej petli hali (Play)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Uruchom Play w scenie Produkcja_Prototyp.");
            var floor = UnityEngine.Object.FindAnyObjectByType<ProductionFloor>();
            var gc = GameController.Instance;
            if (!floor || !gc || gc.player.CurrentVehicle) throw new InvalidOperationException("Test wymaga hali i gracza poza pojazdem.");
            count = 0;
            string snapshot = SaveSystem.ToJson(gc.Sim.S);
            var playerPosition = gc.player.transform.position;
            var vanPosition = floor.van.transform.position; var vanRotation = floor.van.transform.rotation;
            var rb = floor.van.GetComponent<Rigidbody>();
            var speed = rb.linearVelocity; var spin = rb.angularVelocity;
            string report;
            Keyboard testKeyboard = null;
            Keyboard previousKeyboard = Keyboard.current;
            try
            {
                var p = gc.Sim.S.players[0]; p.manualHandling = true; p.tankMilk = 12; p.batches.Clear();
                Array.Clear(p.store, 0, p.store.Length); Array.Clear(p.packingRack, 0, p.packingRack.Length);
                Array.Clear(p.carried, 0, p.carried.Length); Array.Clear(p.vanCargo, 0, p.vanCargo.Length);
                foreach (var o in gc.Sim.S.orders) o.status = OrderStatus.Otwarte;
                floor.SelectProduct(Product.Uht);
                Go(gc, floor, FloorAction.Intake);
                int presses = floor.WorkPressCount;
                testKeyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Q)); InputSystem.Update();
                floor.ProcessWorkInput();
                Check(floor.WorkPressCount == presses + 1, "zdarzenie klawisza Q dociera do akcji pracy");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                Check(p.tankMilk == 8 && p.batches.Count == 1, "zbiornik rozpoczyna partię");
                // Próba obsługi z innego miejsca musi pozostawić partię bez postępu.
                floor.Act(FloorAction.Uht);
                Check(p.batches[0].progress == 0, "wymagana obecność przy maszynie");
                Go(gc, floor, FloorAction.Uht);
                for (int i = 0; i < 30 && p.batches.Any(b => Simulation.StageOf(b).Station == "uht"); i++) floor.Act(FloorAction.Uht);
                Check(Simulation.StageOf(p.batches[0]).Station == "packC", "obsługa linii w świecie");
                Go(gc, floor, FloorAction.PackMilk);
                for (int i = 0; i < 30 && p.batches.Count > 0; i++) floor.Act(FloorAction.PackMilk);
                Check(p.packingRack[0] == 4 && p.store.Sum() == 0, "produkty czekają przy pakowarce");
                floor.Act(FloorAction.PackMilk);
                Check(p.carried[0] == 4 && p.packingRack[0] == 0, "odbiór skrzynki w świecie");
                Go(gc, floor, FloorAction.Warehouse); floor.Act(FloorAction.Warehouse);
                Check(p.store[0] == 4 && p.carried.Sum() == 0, "odłożenie na regał");
                floor.Act(FloorAction.Warehouse);
                Check(p.store[0] == 0 && p.carried[0] == 4 && p.carriedFromStore, "pobranie do wysyłki");
                var restored = SaveSystem.FromJson(SaveSystem.ToJson(gc.Sim.S));
                Check(restored.players[0].carried[0] == 4 && restored.players[0].carriedFromStore, "zapis skrzynki przed załadunkiem");
                Check(!floor.LoadAtVan().Ok && p.carried[0] == 4, "nie można ładować pojazdu z hali");
                Position(gc, floor.van.transform.position + floor.van.transform.right * -2);
                Check(floor.LoadAtVan().Ok && p.vanCargo[0] == 4 && p.carried.Sum() == 0, "załadunek przy pojeździe");
                gc.player.EnterOrExit();
                Check(gc.player.CurrentVehicle == floor.van, "wsiadanie do dostawczaka");
                rb.position = floor.salePoint.position + Vector3.up * 1.2f;
                floor.van.transform.position = rb.position;
                gc.Sim.SetVehicle(0, VehicleKind.Dostawczak);
                double before = p.money; gc.DoContextAction();
                Check(p.vanCargo[0] == 0 && p.money > before, "sprzedaż w punkcie docelowym");
                double after = p.money; gc.DoContextAction();
                Check(p.money == after, "brak drugiej zapłaty");
                report = count + " OK, 0 błędów.\nTest używa rzeczywistych stanowisk, komend, postaci i pojazdu, ale przestawia je między miejscami. Nie zastępuje testu chodzenia i jazdy.\nStan sprzed testu został odtworzony.";
            }
            catch (Exception e) { report = "BŁĄD po " + count + " kontrolach:\n" + e; Debug.LogException(e); }
            finally
            {
                if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
                if (previousKeyboard != null) previousKeyboard.MakeCurrent();
                if (gc.player.CurrentVehicle) gc.player.EnterOrExit();
                floor.van.transform.SetPositionAndRotation(vanPosition, vanRotation);
                rb.position = vanPosition; rb.rotation = vanRotation; rb.linearVelocity = speed; rb.angularVelocity = spin;
                Position(gc, playerPosition - Vector3.up * 0.2f);
                JsonUtility.FromJsonOverwrite(snapshot, gc.Sim.S); Simulation.Migrate(gc.Sim.S);
                gc.Sim.Events.Clear(); gc.SaveGame();
            }
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/produkcja_petla.txt", report);
            Debug.Log("[Mleczarka] Test hali: " + report);
        }
    }
}
