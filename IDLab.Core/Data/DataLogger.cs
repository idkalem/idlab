using System;
using System.Collections.Generic;

namespace IDLab.Core.Data
{
    /// <summary>A live sensor stream (e.g. "Thermometer #3 — Temperature (°C)").</summary>
    public sealed class DataChannel
    {
        public string Id { get; }
        public string Label { get; }
        public string Unit { get; }
        public int OwnerInstanceId { get; }
        public DataSeries Series { get; }
        internal Func<double> Sampler { get; }

        /// <summary>Suggested fixed axis range (the graph auto-expands beyond it).</summary>
        public double SuggestedMin { get; set; }
        public double SuggestedMax { get; set; }

        public DataChannel(string id, string label, string unit, int ownerInstanceId, Func<double> sampler, int capacity)
        {
            Id = id;
            Label = label;
            Unit = unit;
            OwnerInstanceId = ownerInstanceId;
            Sampler = sampler;
            Series = new DataSeries(capacity);
        }
    }

    /// <summary>Anything that publishes channels to the logger when spawned (probes, meters, burners...).</summary>
    public interface IDataSource
    {
        void RegisterChannels(DataLogger logger);
    }

    /// <summary>Samples every channel at a fixed simulated-time interval.</summary>
    public sealed class DataLogger
    {
        private readonly List<DataChannel> _channels = new List<DataChannel>();
        private double _nextSampleTime;

        public double SampleIntervalS { get; set; } = 0.25;
        public int DefaultCapacity { get; set; } = 4096; // ≈ 17 min at 4 Hz

        public IReadOnlyList<DataChannel> Channels => _channels;

        public event Action<DataChannel> ChannelAdded;
        public event Action<DataChannel> ChannelRemoved;

        public DataChannel AddChannel(string id, string label, string unit, int ownerInstanceId, Func<double> sampler)
        {
            var channel = new DataChannel(id, label, unit, ownerInstanceId, sampler, DefaultCapacity);
            _channels.Add(channel);
            ChannelAdded?.Invoke(channel);
            return channel;
        }

        public void RemoveChannelsOwnedBy(int instanceId)
        {
            for (int i = _channels.Count - 1; i >= 0; i--)
            {
                if (_channels[i].OwnerInstanceId == instanceId)
                {
                    var removed = _channels[i];
                    _channels.RemoveAt(i);
                    ChannelRemoved?.Invoke(removed);
                }
            }
        }

        public DataChannel Find(string id)
        {
            foreach (var c in _channels)
            {
                if (c.Id == id)
                {
                    return c;
                }
            }

            return null;
        }

        public void Tick(double time)
        {
            if (time + 1e-9 < _nextSampleTime)
            {
                return;
            }

            foreach (var channel in _channels)
            {
                double value = channel.Sampler();
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    channel.Series.Add(time, value);
                }
            }

            _nextSampleTime = time + SampleIntervalS;
        }

        public void Clear()
        {
            foreach (var c in _channels)
            {
                c.Series.Clear();
            }

            _nextSampleTime = 0;
        }
    }
}
