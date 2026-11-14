using System;
using Mleczarka.Coop;

public static class CooperativeChecks
{
    static int count;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        count++;
    }
    public static string Run()
    {
        count = 0;
        var state = new CooperativeState();
        var coop = new Cooperative(state);
        Check(coop.Join("a").ok && coop.Join("b").ok, "członkostwo");
        Check(!coop.Join("a").ok && !coop.Join(" ").ok, "duplikat i puste id");
        long wallet = 10000;
        Check(!coop.Contribute("obcy", 100, ref wallet).ok && wallet == 10000, "obcy nie wpłaca");
        Check(!coop.Contribute("a", -100, ref wallet).ok && state.balance == 0, "ujemna wpłata");
        Check(!coop.Contribute("a", 10001, ref wallet).ok && wallet == 10000, "brak środków");
        Check(coop.Contribute("a", 6000, ref wallet).ok && wallet == 4000 && state.balance == 6000, "bilans wpłaty");
        Check(!coop.Propose("obcy", "p", "Linia", 1000, 0, 10, 1).ok, "obcy nie proponuje");
        Check(coop.Propose("a", "p", "Linia", 3000, 0, 10, 2).ok, "wniosek");
        Check(!coop.Propose("a", "p", "Drugi", 3000, 0, 10, 2).ok, "unikalne id inwestycji");
        Check(coop.Join("c").ok && !coop.Vote("c", "p", true, 1).ok, "zamrożony elektorat");
        Check(coop.Vote("a", "p", true, 1).ok && !coop.Vote("a", "p", true, 2).ok, "jeden głos");
        Check(!coop.Resolve("p", 9).ok && state.balance == 6000, "termin");
        Check(coop.Vote("b", "p", true, 9).ok && !coop.Vote("b", "p", true, 10).ok, "zamknięcie głosowania");
        Check(coop.Resolve("p", 10).ok && state.balance == 3000 && state.investments[0].status == ProposalStatus.Accepted, "inwestycja");
        Check(!coop.Resolve("p", 11).ok && state.balance == 3000, "brak podwójnego wydatku");
        coop.Propose("a", "tie", "Remis", 1000, 10, 20, 2);
        coop.Vote("a", "tie", true, 11); coop.Vote("b", "tie", false, 11); coop.Resolve("tie", 20);
        Check(state.investments[1].status == ProposalStatus.Rejected && state.balance == 3000, "remis");
        coop.Propose("a", "quorum", "Kworum", 1000, 20, 30, 2);
        coop.Vote("a", "quorum", true, 21); coop.Resolve("quorum", 30);
        Check(state.investments[2].status == ProposalStatus.Rejected && state.balance == 3000, "brak kworum");
        coop.Propose("a", "poor", "Droga linia", 4000, 30, 40, 1);
        coop.Vote("a", "poor", true, 31); coop.Resolve("poor", 40);
        Check(state.investments[3].status == ProposalStatus.Rejected && state.balance == 3000, "brak budżetu");
        state.balance = Int64.MaxValue;
        Check(!coop.Contribute("a", 1, ref wallet).ok && wallet == 4000, "przepełnienie bez utraty pieniędzy");
        bool rejected = false;
        try { new Cooperative(new CooperativeState { version = 2 }); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "odrzucenie przyszłej wersji");
        return count + " OK, 0 błędów";
    }
}
