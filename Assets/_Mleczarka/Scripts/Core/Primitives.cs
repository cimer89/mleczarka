using System;
using System.Collections.Generic;

namespace Mleczarka.Core
{
    /// <summary>Wynik komendy: ok + komunikat dla gracza.</summary>
    public readonly struct Result
    {
        public readonly bool Ok; public readonly string Msg;
        Result(bool ok, string msg) { Ok = ok; Msg = msg ?? ""; }
        public static Result Success(string msg = "") => new Result(true, msg);
        public static Result Fail(string msg) => new Result(false, msg);
        public override string ToString() => (Ok ? "OK " : "BŁĄD ") + Msg;
    }

    /// <summary>Zdarzenie dla prezentacji (dźwięk, efekty, komunikaty). Nie jest częścią zapisu.</summary>
    public readonly struct GameEvent
    {
        public readonly string Type; public readonly int Pid; public readonly int Id; public readonly double Value; public readonly string Text;
        public GameEvent(string type, int pid = -1, int id = -1, double value = 0, string text = null) { Type = type; Pid = pid; Id = id; Value = value; Text = text; }
        public override string ToString() => $"{Type}(gracz {Pid}, id {Id}, {Value}, {Text})";
    }

    /// <summary>Gdzie stoi pojazd gracza. Dostarcza świat gry (strefy w scenie); symulacja tylko to sprawdza.</summary>
    public enum PlaceKind { Brak = 0, Gospodarstwo = 1, Mleczarnia = 2, Rynek = 3 }
    public readonly struct Place
    {
        public readonly PlaceKind Kind; public readonly int FarmId;
        public Place(PlaceKind kind, int farmId = -1) { Kind = kind; FarmId = farmId; }
        public static readonly Place None = new Place(PlaceKind.Brak);
    }
    public interface IPlaceProvider { Place PlaceOf(int playerId); }

    /// <summary>Deterministyczny generator (xorshift32); stan w GameState.rng, więc zapis odtwarza losowania.</summary>
    public static class Rng
    {
        public static uint Next(GameState s) { uint x = s.rng; x ^= x << 13; x ^= x >> 17; x ^= x << 5; s.rng = x == 0 ? 1u : x; return s.rng; }
        public static double Value(GameState s) => Next(s) / 4294967296.0;
        public static int Range(GameState s, int a, int b) => a + (int)(Value(s) * (b - a + 1));   // włącznie
        public static T Pick<T>(GameState s, IReadOnlyList<T> list) => list[(int)(Value(s) * list.Count) % list.Count];
    }

    public static class Money
    {
        public static double M2(double x) => Math.Round(x * 100, MidpointRounding.AwayFromZero) / 100;
        public static string Zl(double x) => M2(x).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
