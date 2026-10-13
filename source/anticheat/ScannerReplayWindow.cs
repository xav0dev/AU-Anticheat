using System;
using System.Collections.Generic;
namespace AUAnticheat.anticheat
{
    internal sealed class ScannerReplayWindow
    {
        private sealed class Sample { public byte Sequence; public bool Scanning; public double At; }
        private readonly Dictionary<uint,Sample> samples=new();
        public void Reset() => samples.Clear();
        public bool Accept(uint objectId,bool scanning,byte sequence,double now)
        {
            if(!double.IsFinite(now)) return true;
            bool duplicate=samples.TryGetValue(objectId,out var old) && old.Scanning && scanning &&
                old.Sequence==sequence && now>=old.At && now-old.At<2;
            // Always allow scan-stop cleanup (including death/meeting transitions).
            if(duplicate) return false;
            if(samples.Count>=64 && !samples.ContainsKey(objectId)) samples.Clear();
            samples[objectId]=new Sample { Sequence=sequence,Scanning=scanning,At=now };
            return true;
        }
    }
}
