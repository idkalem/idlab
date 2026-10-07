using System;
using System.Collections.Generic;
using System.Numerics;

namespace IDLab.Core.Events
{
    public enum LabEventType
    {
        Info,
        BoilingStarted,
        Spill,
        Fizz,
        Smoke,
        ToxicGas,
        Explosion,
        Spark,      // circuits: short circuit
        FuseBlown,  // circuits: overcurrent
    }

    /// <summary>Something noteworthy happened at a place on the bench. Drives VFX, sound and toasts.</summary>
    public sealed class LabEvent
    {
        public LabEventType Type { get; }
        public double Severity { get; }
        public string Message { get; }
        public int InstanceId { get; }
        public Vector3 Position { get; }
        public double Time { get; }

        public LabEvent(LabEventType type, double severity, string message, int instanceId, Vector3 position, double time)
        {
            Type = type;
            Severity = severity;
            Message = message;
            InstanceId = instanceId;
            Position = position;
            Time = time;
        }

        public bool IsHazard => Type == LabEventType.Smoke || Type == LabEventType.ToxicGas ||
                                Type == LabEventType.Explosion || Type == LabEventType.Spark ||
                                Type == LabEventType.FuseBlown;

        public override string ToString() => $"[{Time:0.00}s] {Type} ({Severity:0.00}) {Message}";
    }

    /// <summary>Synchronous publish/subscribe bus plus a short history (for the safety log UI).</summary>
    public sealed class LabEventBus
    {
        private readonly List<LabEvent> _history = new List<LabEvent>();

        public int HistoryCapacity { get; set; } = 100;
        public IReadOnlyList<LabEvent> History => _history;

        public event Action<LabEvent> Published;

        public void Publish(LabEvent e)
        {
            _history.Add(e);
            if (_history.Count > HistoryCapacity)
            {
                _history.RemoveAt(0);
            }

            Published?.Invoke(e);
        }
    }
}
