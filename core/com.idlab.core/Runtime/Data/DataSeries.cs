using System;

namespace IDLab.Core.Data
{
    /// <summary>
    /// Fixed-capacity ring buffer of (time, value) samples. Allocation-free after construction so it can be
    /// plotted every frame on mobile without GC spikes. Index 0 is the oldest retained sample.
    /// </summary>
    public sealed class DataSeries
    {
        private readonly double[] _t;
        private readonly double[] _v;
        private int _start;

        public int Capacity { get; }
        public int Count { get; private set; }

        /// <summary>Increments on every change so views can skip redraws.</summary>
        public int Version { get; private set; }

        public DataSeries(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            _t = new double[capacity];
            _v = new double[capacity];
        }

        public void Add(double time, double value)
        {
            int index;
            if (Count < Capacity)
            {
                index = (_start + Count) % Capacity;
                Count++;
            }
            else
            {
                index = _start;
                _start = (_start + 1) % Capacity;
            }

            _t[index] = time;
            _v[index] = value;
            Version++;
        }

        public double TimeAt(int i) => _t[Physical(i)];
        public double ValueAt(int i) => _v[Physical(i)];

        public double LastTime => Count > 0 ? TimeAt(Count - 1) : 0;
        public double LastValue => Count > 0 ? ValueAt(Count - 1) : double.NaN;

        /// <summary>Min/max of values whose time lies in [fromTime, ∞). Returns false if none.</summary>
        public bool TryGetRange(double fromTime, out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;
            for (int i = 0; i < Count; i++)
            {
                if (TimeAt(i) < fromTime)
                {
                    continue;
                }

                double v = ValueAt(i);
                if (v < min)
                {
                    min = v;
                }

                if (v > max)
                {
                    max = v;
                }
            }

            return min <= max;
        }

        public void Clear()
        {
            Count = 0;
            _start = 0;
            Version++;
        }

        private int Physical(int i)
        {
            if ((uint)i >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(i));
            }

            return (_start + i) % Capacity;
        }
    }
}
