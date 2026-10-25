using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AUAnticheat.anticheat
{
    // An explainable, bounded correlation tool, NOT sender authentication.
    // No dependency on punishment, networking, persistent IDs, or cheat scores.
    internal sealed class SequenceEvidence
    {
        private sealed class Observation
        {
            internal double At;
            internal int Owner;
            internal uint Object;
            internal string Family, Action;
        }

        private readonly Func<double> clock;
        private readonly List<Observation> observations = new();
        private readonly Dictionary<int, double> grace = new();
        private double globalGrace, lastTime = double.NegativeInfinity;
        internal const int Capacity = 256;
        internal const double Window = 45;
        internal SequenceEvidence(Func<double> clock = null)
        {
            this.clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        private double Now()
        {
            double now = clock();
            if(!double.IsFinite(now)) return double.NaN;
            if(now < lastTime) Clear(); // Never merge evidence across a clock discontinuity.
            lastTime = now;
            observations.RemoveAll(e => now - e.At > Window);
            foreach(int id in grace.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) grace.Remove(id);
            return now;
        }

        internal int Count { get { Now(); return observations.Count; } }
        internal void Clear()
        {
            observations.Clear(); grace.Clear(); globalGrace = 0; lastTime = double.NegativeInfinity;
        }

        internal void Forget(int owner)
        {
            observations.RemoveAll(e => e.Owner == owner);
            grace.Remove(owner);
        }

        internal void Pause(int? owner, double seconds)
        {
            double now = Now();
            if(!double.IsFinite(now) || !double.IsFinite(seconds) || seconds <= 0) return;
            double until = now + Math.Min(seconds, 30);
            if(owner.HasValue)
            {
                observations.RemoveAll(e => e.Owner == owner.Value);
                // Bound state even if bogus owners are supplied. Ignore excess new owners.
                if(grace.ContainsKey(owner.Value) || grace.Count < 64)
                    grace[owner.Value] = Math.Max(grace.GetValueOrDefault(owner.Value), until);
            }
            else
            {
                observations.Clear(); globalGrace = Math.Max(globalGrace, until);
            }
        }

        internal void Observe(int? owner, uint? objectId, int? claim, bool conflict, string action)
        {
            double now = Now();
            if(!double.IsFinite(now) || now < globalGrace || !owner.HasValue || owner < 0 || !objectId.HasValue ||
                conflict || (claim.HasValue && claim != owner) || grace.ContainsKey(owner.Value)) return;
            string family = Family(action);
            if(family == null) return; // Never derive identities from shared targets, names, or unknown RPCs.
            // Different checks and duplicate packets from one short burst are not independent evidence.
            if(observations.Any(e => e.Owner == owner && e.Object == objectId && e.Family == family && now - e.At < 2)) return;
            if(observations.Count >= Capacity) observations.RemoveAt(0);
            observations.Add(new Observation { At = now, Owner = owner.Value, Object = objectId.Value, Family = family, Action = action });
        }

        private static string Family(string action) => action switch
        {
            "EnterVent" or "ExitVent" or "SnapTo" or "ClimbLadder" or "UsePlatform" => "movement",
            "CheckMurder" or "MurderPlayer" or "CheckProtect" or "ProtectPlayer" or "SetRole" or
                "Shapeshift" or "CheckShapeshift" or "CheckVanish" or "StartVanish" or "CheckAppear" or "StartAppear" => "role/combat",
            "ReportDeadBody" or "StartMeeting" or "CastVote" or "ClearVote" or "VotingComplete" => "meeting",
            "CompleteTask" or "PlayAnimation" or "SetScanner" => "task/animation",
            "SetName" or "CheckName" or "SetColor" or "CheckColor" or "SetLevel" or
                "SetHatStr" or "SetSkinStr" or "SetPetStr" or "SetVisorStr" or "SetNamePlateStr" => "appearance/identity",
            _ => null
        };

        internal string Summary(Func<int, string> name = null)
        {
            Now();
            var output = new StringBuilder();
            var groups = observations.GroupBy(e => (e.Owner, e.Object))
                .Where(g => g.Count() >= 3 && g.Select(e => e.Family).Distinct().Count() >= 2 && g.Last().At - g.First().At >= 4)
                .OrderByDescending(g => g.Last().At).Take(8);
            foreach(var group in groups)
            {
                string label = name?.Invoke(group.Key.Owner);
                output.AppendLine($"Review messages referencing {label ?? "player"} (lobby ID {group.Key.Owner}, object {group.Key.Object}).");
                output.AppendLine($"{group.Count()} spaced observations across {group.Select(e => e.Family).Distinct().Count()} action types in the last 45 seconds.");
                output.AppendLine("Recent sequence: " + string.Join(" -> ", group.TakeLast(4).Select(e => e.Action)) + ".");
                output.AppendLine("Sender UNKNOWN. This player may be the victim of forged messages. No points or automatic removal from this pattern.");
            }
            return output.Length == 0 ? "No sustained mixed-action patterns outside normal transition grace periods." : output.ToString();
        }
    }
}
