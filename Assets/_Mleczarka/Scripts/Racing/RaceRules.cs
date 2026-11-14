using System;

namespace Mleczarka.Racing
{
    public enum RacePhase { Countdown, Running, Finished }

    /// <summary>Reguły wyścigu bez Unity. Punkty 0..N-1 w kolejności; meta po każdym pełnym obiegu.</summary>
    public sealed class RaceRules
    {
        public readonly int checkpoints, laps;
        public RacePhase Phase { get; private set; }
        public double Countdown { get; private set; }
        public double Elapsed { get; private set; }
        public int CompletedLaps { get; private set; }
        public int NextCheckpoint { get; private set; }

        public RaceRules(int checkpointCount, int lapCount, double countdown)
        {
            if (checkpointCount < 3 || lapCount < 1 || Double.IsNaN(countdown) || Double.IsInfinity(countdown) || countdown < 0)
                throw new ArgumentOutOfRangeException();
            checkpoints = checkpointCount; laps = lapCount; Countdown = countdown;
            Phase = countdown > 0 ? RacePhase.Countdown : RacePhase.Running;
        }

        public void Tick(double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException("seconds");
            if (Phase == RacePhase.Finished) return;
            if (Phase == RacePhase.Countdown)
            {
                double used = Math.Min(Countdown, seconds);
                Countdown -= used; seconds -= used;
                if (Countdown <= 0) Phase = RacePhase.Running;
            }
            if (Phase == RacePhase.Running) Elapsed += seconds;
        }

        public bool PassCheckpoint(int index)
        {
            if (Phase != RacePhase.Running || index != NextCheckpoint) return false;
            NextCheckpoint++;
            if (NextCheckpoint == checkpoints)
            {
                NextCheckpoint = 0;
                CompletedLaps++;
                if (CompletedLaps == laps) Phase = RacePhase.Finished;
            }
            return true;
        }

        public int PassedCount { get { return CompletedLaps * checkpoints + NextCheckpoint; } }
    }
}
