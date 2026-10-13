using AUAnticheat.anticheat;

internal static class SequenceEvidenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        double now = 100;
        var model = new SequenceEvidence(() => now);
        void Observe(string action, int owner = 1, uint objectId = 10) => model.Observe(owner, objectId, owner, false, action);
        void Pattern(int owner = 1, uint objectId = 10)
        {
            Observe("SetScanner", owner, objectId); now += 2;
            Observe("CheckMurder", owner, objectId); now += 2;
            Observe("SetScanner", owner, objectId);
        }
        Pattern();
        string summary = model.Summary(_ => "Test player");
        check(summary.Contains("Test player") && summary.Contains("3 spaced") && summary.Contains("SetScanner -> CheckMurder -> SetScanner"), "mixed spaced sequence is explainable and names referenced object");
        check(summary.Contains("Sender UNKNOWN") && summary.Contains("victim") && summary.Contains("No points"), "sequence does not turn object reference into verified sender");
        now += 46;
        check(model.Count == 0 && !model.Summary().Contains("Review messages"), "old evidence expires without requiring another violation");
        for(int i = 0; i < 10000; i++) { Observe("SetScanner"); Observe("CompleteTask"); }
        check(model.Count == 1, "duplicate and sibling checks in a short burst count once per family");
        now += .1; Observe("CheckMurder"); now += .1; Observe("SetColor");
        check(!model.Summary().Contains("Review messages"), "one network catch-up burst cannot create sustained pattern");
        model.Clear();
        for(int i = 0; i < 10; i++) { Observe("SetScanner"); now += 3; }
        check(!model.Summary().Contains("Review messages"), "single family does not imply mixed exploit sequence");
        model.Clear();
        for(int i = 0; i < 6; i++)
        {
            model.Observe(1, 10, 2, false, "SetScanner");
            model.Observe(1, 10, 1, true, "CheckMurder");
            model.Observe(null, null, 1, false, "CheckMurder");
            model.Observe(1, null, 1, false, "CheckMurder");
            model.Observe(1, 10, 1, false, "UnknownRpc");
            now += 3;
        }
        check(model.Count == 0, "conflicts, shared claimed identities and unknown procedures do not rank a player");
        Pattern();
        model.Pause(1, 6); now += 1; Pattern();
        check(model.Count == 0, "vent, death and animation grace clears and suppresses affected player history");
        now += 1; Pattern();
        check(model.Summary().Contains("Review messages"), "fresh sustained sequence possible after grace ends");
        model.Pause(null, 15); Pattern(2, 20);
        check(model.Count == 0, "round and meeting boundaries suppress sequences globally");
        now += 16; Pattern(2, 20); model.Forget(2);
        check(model.Count == 0, "departure removes player evidence before ID reuse");
        Observe("SetScanner", 1, 10); now += 2; Observe("CheckMurder", 1, 11); now += 2; Observe("SetScanner", 1, 10);
        check(!model.Summary().Contains("Review messages"), "different spawned objects are never merged by owner ID alone");
        model.Clear();
        for(int i = 0; i < 2000; i++) Observe("SetScanner", i, (uint)i);
        check(model.Count == SequenceEvidence.Capacity, "flood memory bounded");
        now -= 1;
        check(model.Count == 0, "backwards time invalidates existing evidence");
        now = double.NaN; Observe("SetScanner");
        check(model.Count == 0, "invalid clock cannot add observations");
        now = 1000; Pattern(); model.Clear();
        check(model.Count == 0, "lobby reset clears all sequence state");

        SenderEvidence.Reset();
        var old = SenderEvidence.Begin("PlayerControl", 15, 10, 1, "Target", "SetScanner");
        SenderEvidence.Reset(); SenderEvidence.Record(); old.Dispose();
        check(!SenderEvidence.SequenceSummary().Contains("Review messages"), "stale packet context cannot cross a session reset");
        SenderEvidence.Reset();
    }
}
