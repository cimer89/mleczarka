using System;
using System.Collections.Generic;

namespace Mleczarka.Coop
{
    [Serializable]
    public sealed class Member
    {
        public string playerId;
        public long contribution;
    }

    public enum ProposalStatus { Open, Accepted, Rejected }

    [Serializable]
    public sealed class Ballot
    {
        public string playerId;
        public bool approve;
    }

    [Serializable]
    public sealed class Investment
    {
        public string id, title;
        public long cost, closesAt;
        public int quorum;
        public ProposalStatus status;
        // Zamrożony skład zapobiega zmianie wyniku przez dopisywanie członków.
        public List<string> electorate = new List<string>();
        public List<Ballot> ballots = new List<Ballot>();
    }

    [Serializable]
    public sealed class CooperativeState
    {
        public int version = 1;
        // Wszystkie kwoty w groszach, czas w taktach symulacji.
        public long balance;
        public List<Member> members = new List<Member>();
        public List<Investment> investments = new List<Investment>();
    }

    public sealed class Decision
    {
        public readonly bool ok;
        public readonly string message;
        public Decision(bool success, string text) { ok = success; message = text; }
    }

    /// <summary>Lokalne reguły spółdzielni. Jeden członek = jeden głos.
    /// Wywoływać szeregowo na autorytatywnej symulacji. Brak sieci i operacji plikowych.</summary>
    public sealed class Cooperative
    {
        public readonly CooperativeState State;
        public Cooperative(CooperativeState state)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (state.version != 1 || state.balance < 0 || state.members == null || state.investments == null)
                throw new ArgumentException("Nieprawidłowy stan spółdzielni.");
            State = state;
        }

        static Decision Fail(string text) { return new Decision(false, text); }
        static Decision Ok() { return new Decision(true, "OK"); }
        Member FindMember(string id) { return State.members.Find(m => m.playerId == id); }

        public Decision Join(string playerId)
        {
            if (String.IsNullOrWhiteSpace(playerId) || FindMember(playerId) != null)
                return Fail("Nieprawidłowy lub istniejący członek.");
            State.members.Add(new Member { playerId = playerId });
            return Ok();
        }

        /// <summary>Adapter przekazuje portfel gracza przez ref. Walidacja poprzedza obie zmiany.</summary>
        public Decision Contribute(string playerId, long amount, ref long wallet)
        {
            var member = FindMember(playerId);
            if (member == null || amount <= 0 || wallet < amount)
                return Fail("Brak członkostwa, środków lub nieprawidłowa kwota.");
            if (State.balance > Int64.MaxValue - amount || member.contribution > Int64.MaxValue - amount)
                return Fail("Przekroczono zakres kwoty.");
            wallet -= amount;
            State.balance += amount;
            member.contribution += amount;
            return Ok();
        }

        public Decision Propose(string playerId, string id, string title, long cost, long now, long closesAt, int quorum)
        {
            if (FindMember(playerId) == null || String.IsNullOrWhiteSpace(id) || String.IsNullOrWhiteSpace(title)
                || State.investments.Exists(p => p.id == id) || cost <= 0 || now < 0 || closesAt <= now
                || quorum < 1 || quorum > State.members.Count)
                return Fail("Nieprawidłowy wniosek lub brak uprawnień.");
            var proposal = new Investment { id = id, title = title, cost = cost, closesAt = closesAt, quorum = quorum };
            foreach (var member in State.members) proposal.electorate.Add(member.playerId);
            State.investments.Add(proposal);
            return Ok();
        }

        public Decision Vote(string playerId, string proposalId, bool approve, long now)
        {
            var p = State.investments.Find(x => x.id == proposalId);
            if (p == null || p.status != ProposalStatus.Open || now < 0 || now >= p.closesAt
                || !p.electorate.Contains(playerId) || p.ballots.Exists(b => b.playerId == playerId))
                return Fail("Głos niedostępny lub już oddany.");
            p.ballots.Add(new Ballot { playerId = playerId, approve = approve });
            return Ok();
        }

        /// <summary>Wykonuje inwestycję raz, po terminie. Remis i brak środków odrzucają wniosek.
        /// Accepted jest trwałym zleceniem dla prezentacji, identyfikowanym przez id.</summary>
        public Decision Resolve(string proposalId, long now)
        {
            var p = State.investments.Find(x => x.id == proposalId);
            if (p == null || p.status != ProposalStatus.Open || now < p.closesAt)
                return Fail("Wniosek nie jest gotowy do rozliczenia.");
            int yes = p.ballots.FindAll(b => b.approve).Count;
            bool accepted = p.ballots.Count >= p.quorum && yes > p.ballots.Count - yes && State.balance >= p.cost;
            if (accepted) State.balance -= p.cost;
            p.status = accepted ? ProposalStatus.Accepted : ProposalStatus.Rejected;
            return Ok();
        }
    }
}
