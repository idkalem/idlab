using IDLab.Core.Data;
using Xunit;

namespace IDLab.Core.Tests
{
    public class DataSeriesTests
    {
        [Fact]
        public void RingBufferKeepsNewestSamplesInOrder()
        {
            var s = new DataSeries(3);
            for (int i = 0; i < 5; i++)
            {
                s.Add(i, i * 10);
            }

            Assert.Equal(3, s.Count);
            Assert.Equal(2, s.TimeAt(0));
            Assert.Equal(40, s.ValueAt(2));
            Assert.Equal(40, s.LastValue);
        }

        [Fact]
        public void RangeHonoursWindowStart()
        {
            var s = new DataSeries(10);
            s.Add(0, 100);
            s.Add(1, 5);
            s.Add(2, 7);
            Assert.True(s.TryGetRange(1, out double min, out double max));
            Assert.Equal(5, min);
            Assert.Equal(7, max);
        }

        [Fact]
        public void LoggerSamplesAtInterval()
        {
            var logger = new DataLogger { SampleIntervalS = 0.5 };
            double value = 0;
            var ch = logger.AddChannel("x", "X", "u", 1, () => value);
            for (int i = 1; i <= 100; i++)
            {
                value = i;
                logger.Tick(i * 0.02);
            }

            Assert.Equal(4, ch.Series.Count); // t = 0.02, 0.52, 1.02, 1.52
        }
    }
}
