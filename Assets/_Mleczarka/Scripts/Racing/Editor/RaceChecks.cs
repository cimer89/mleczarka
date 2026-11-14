using System;

namespace Mleczarka.Racing
{
    public static class RaceChecks
    {
        static int count;
        static void Check(bool value, string name)
        { if (!value) throw new Exception("FAIL: " + name); count++; }
        public static string Run()
        {
            count = 0;
            var r = new RaceRules(4, 2, 3);
            Check(!r.PassCheckpoint(0), "nie można ruszyć przed startem");
            r.Tick(2); Check(r.Countdown == 1 && r.Elapsed == 0, "odliczanie bez naliczania czasu");
            r.Tick(1.5); Check(r.Phase == RacePhase.Running && r.Elapsed == 0.5, "nadmiar czasu po starcie");
            Check(!r.PassCheckpoint(2) && r.NextCheckpoint == 0, "zakaz pomijania punktów");
            Check(r.PassCheckpoint(0) && !r.PassCheckpoint(0), "duplikat punktu");
            Check(!r.PassCheckpoint(3), "jazda wstecz nie kończy okrążenia");
            r.PassCheckpoint(1); r.PassCheckpoint(2); r.PassCheckpoint(3);
            Check(r.CompletedLaps == 1 && r.Phase == RacePhase.Running, "pierwsze okrążenie");
            r.Tick(20); Check(r.Elapsed == 20.5, "czas wyścigu");
            for (int i = 0; i < 4; i++) Check(r.PassCheckpoint(i), "drugie okrążenie " + i);
            Check(r.Phase == RacePhase.Finished && r.PassedCount == 8, "meta");
            Check(!r.PassCheckpoint(0), "meta tylko raz");
            r.Tick(100); Check(r.Elapsed == 20.5, "wynik nie rośnie po mecie");
            var instant = new RaceRules(3, 1, 0);
            Check(instant.Phase == RacePhase.Running, "start bez odliczania");
            bool bad = false; try { r.Tick(Double.NaN); } catch (ArgumentOutOfRangeException) { bad = true; }
            Check(bad, "odrzucenie NaN");
            bad = false; try { new RaceRules(2, 1, 0); } catch (ArgumentOutOfRangeException) { bad = true; }
            Check(bad, "minimum punktów");
            var rival = new RaceRules(4, 2, 0);
            Check(rival.CompletedLaps == 0 && rival.Elapsed == 0, "niezależny rywal");
            return count + " OK, 0 błędów";
        }
    }
}
