using System;

namespace IDLab.Core
{
    /// <summary>
    /// Fixed-timestep accumulator decoupling the simulation from the render frame rate.
    /// Deterministic regardless of device FPS (important for identical results on desktop and mobile).
    /// </summary>
    public sealed class SimClock
    {
        private double _accumulator;

        public double FixedStep { get; set; } = 0.02;          // 50 Hz
        public double TimeScale { get; set; } = 1.0;           // 1x, 5x, 20x "fast forward"
        public int MaxStepsPerAdvance { get; set; } = 60;      // avoids a spiral of death on slow frames
        public bool Paused { get; set; }
        public double SimTime { get; private set; }

        /// <summary>Feeds real elapsed seconds in; invokes <paramref name="step"/> zero or more times. Returns steps run.</summary>
        public int Advance(double realDeltaSeconds, Action<double> step)
        {
            if (Paused || realDeltaSeconds <= 0)
            {
                return 0;
            }

            _accumulator += realDeltaSeconds * TimeScale;
            int steps = 0;
            while (_accumulator >= FixedStep - 1e-9 && steps < MaxStepsPerAdvance)
            {
                step(FixedStep);
                SimTime += FixedStep;
                _accumulator -= FixedStep;
                steps++;
            }

            if (steps == MaxStepsPerAdvance)
            {
                _accumulator = 0; // drop the backlog instead of falling further behind
            }

            return steps;
        }

        public void Reset()
        {
            _accumulator = 0;
            SimTime = 0;
        }
    }
}
