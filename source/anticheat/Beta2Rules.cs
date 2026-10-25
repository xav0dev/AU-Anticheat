using System;
using System.Collections.Generic;

namespace AUAnticheat.anticheat
{
    internal static class Beta2Rules
    {
        public static bool EarlyButton(double remaining) => double.IsFinite(remaining) && remaining > 2.5 && remaining <= 600;
        public static int StartTarget(int requested, int capacity, int minimum)
        {
            int upper=Math.Clamp(capacity,4,15), lower=Math.Clamp(minimum,4,upper);
            return Math.Clamp(requested <= 0 ? upper : requested,lower,upper);
        }
        public static bool ExactName(string name, IEnumerable<string> values)
        {
            string normalized;
            try { normalized = ChatContentRules.Normalize(name).Trim(); } catch(ArgumentException) { return false; }
            foreach(string value in values) if(string.Equals(normalized, value?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    // Bounded, monotonic-time window. Counts observations, never identifies a sender.
    internal sealed class EventWindow
    {
        private readonly Queue<float> events = new();
        private float last = -1;
        public void Clear() { events.Clear(); last = -1; }
        public int Add(float now, float window)
        {
            if(!float.IsFinite(now) || !float.IsFinite(window) || window <= 0) return 0;
            if(now < last) Clear();
            last = now;
            while(events.Count > 0 && now - events.Peek() >= window) events.Dequeue();
            while(events.Count >= 128) events.Dequeue();
            events.Enqueue(now); return events.Count;
        }
    }
}
