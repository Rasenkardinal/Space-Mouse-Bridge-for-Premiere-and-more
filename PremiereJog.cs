using System;
using System.IO;

namespace SpaceMouseBridge
{
    public enum JogBehavior { Continuous, SingleFrameRegion }

    public sealed class TimelineScrollSettings
    {
        public bool Enabled { get; set; }
        public bool Reverse { get; set; }
        public int MaximumRate { get; set; }
        public int? EndRate { get; set; }
        internal int EffectiveEndRate { get { return EndRate ?? MaximumRate; } }

        internal static TimelineScrollSettings Default()
        {
            return new TimelineScrollSettings { MaximumRate = 24 };
        }

        internal void Validate()
        {
            if (MaximumRate < 2 || MaximumRate > 120)
                throw new InvalidDataException("Timeline-Scrollen: Maximum 2 bis 120 Mausradimpulse/s.");
            if (EffectiveEndRate < MaximumRate || EffectiveEndRate > 120)
                throw new InvalidDataException("Timeline-Scrollen: Endtempo zwischen Grundtempo und 120 Mausradimpulsen/s.");
        }

        internal double Rate(double normalized)
        {
            double value = Math.Min(1, Math.Abs(normalized));
            double extra = Math.Max(0, (value - 0.6) / 0.4);
            return Timing().Rate(value) + (EffectiveEndRate - MaximumRate) * extra * extra;
        }

        internal JogSettings Timing()
        {
            return new JogSettings {
                Deadzone = 12, MinimumRate = 0.5m, MaximumRate = MaximumRate,
                CurveExponent = 2, Reverse = Reverse
            };
        }

        internal static Native.KeyboardInput[] Inputs(int steps)
        {
            if (steps == 0 || Math.Abs((long)steps) > 6)
                throw new InvalidOperationException("Ungueltige Timeline-Scrollschrittzahl.");
            // Premiere's horizontal wheel preference maps wheel-down to later time.
            return new[] { new Native.KeyboardInput { Type = 0, MouseFlags = 0x0800, MouseData = -120 * steps } };
        }
    }

    public sealed class JogSettings
    {
        public bool Reverse { get; set; }
        public int Deadzone { get; set; }
        public int MaximumRate { get; set; }
        public bool FineControl { get; set; }
        public decimal? MinimumRate { get; set; }
        public decimal? CurveExponent { get; set; }
        public int RepeatDelayMs { get; set; }
        public JogBehavior Behavior { get; set; }
        public int SingleFrameLimit { get; set; }

        internal decimal EffectiveMinimumRate { get { return MinimumRate ?? (FineControl ? 0.5m : 2m); } }
        internal decimal EffectiveExponent { get { return CurveExponent ?? (FineControl ? 2m : 1m); } }
        internal int EffectiveSingleFrameLimit { get { return SingleFrameLimit == 0 ? Math.Max(30, Deadzone + 1) : SingleFrameLimit; } }
        internal double ContinuousStart
        {
            get { return (Behavior == JogBehavior.SingleFrameRegion ? EffectiveSingleFrameLimit : Deadzone) / 100.0; }
        }

        internal bool IsSingleFrame(double normalized)
        {
            return Behavior == JogBehavior.SingleFrameRegion && Math.Abs(normalized) > Deadzone / 100.0 &&
                Math.Abs(normalized) <= EffectiveSingleFrameLimit / 100.0;
        }

        internal double Rate(double normalized)
        {
            double value = Math.Min(1, Math.Abs(normalized));
            if (value <= ContinuousStart) return 0;
            double displacement = (value - ContinuousStart) / (1 - ContinuousStart);
            return (double)EffectiveMinimumRate + (MaximumRate - (double)EffectiveMinimumRate) *
                Math.Pow(displacement, (double)EffectiveExponent);
        }

        internal static JogSettings Default()
        {
            return new JogSettings { Deadzone = 12, MaximumRate = 120, FineControl = true };
        }

        internal void Validate()
        {
            if (Deadzone < 5 || Deadzone > 40 || MaximumRate < 2 || MaximumRate > 120)
                throw new InvalidDataException("Jog: Totzone 5 bis 40 %, maximale Schrittfrequenz 2 bis 120 Bilder/s.");
            if (EffectiveMinimumRate < 0.1m || EffectiveMinimumRate > MaximumRate ||
                EffectiveExponent < 0.25m || EffectiveExponent > 5)
                throw new InvalidDataException("Jog: Minimum 0,1 bis Maximum Bilder/s; Kurvenexponent 0,25 bis 5.");
            if (RepeatDelayMs < 0 || RepeatDelayMs > 3000 || !Enum.IsDefined(typeof(JogBehavior), Behavior))
                throw new InvalidDataException("Jog: Wiederholungsverzoegerung 0 bis 3000 ms und gueltiges Verhalten erforderlich.");
            if ((SingleFrameLimit != 0 && (SingleFrameLimit < 6 || SingleFrameLimit > 95)) ||
                (Behavior == JogBehavior.SingleFrameRegion && EffectiveSingleFrameLimit <= Deadzone))
                throw new InvalidDataException("Jog: Einzelbildbereich muss oberhalb der Totzone und bei hoechstens 95 % enden.");
        }
    }

    internal sealed class JogController
    {
        internal const double ReportTimeout = 250;
        private bool armed;
        private bool hasReport;
        private double lastReport;
        private double value;
        private double due;
        private double previousInterval;
        private double repeatNotBefore;
        private bool inSingleFrame;
        private int direction;
        private bool timedOut;
        internal bool Moving { get { return direction != 0; } }
        internal bool TakeTimeout()
        {
            bool result = timedOut;
            timedOut = false;
            return result;
        }

        internal void Reset()
        {
            armed = false;
            hasReport = false;
            value = 0;
            direction = 0;
            due = 0;
            previousInterval = 0;
            repeatNotBefore = 0;
            inSingleFrame = false;
            timedOut = false;
        }

        internal void Receive(double normalized, double now, JogSettings settings)
        {
            Expire(now);
            value = Math.Max(-1, Math.Min(1, normalized));
            hasReport = true;
            lastReport = now;
            if (Math.Abs(value) <= settings.Deadzone / 100.0)
            {
                armed = true;
                direction = 0;
                due = now;
                previousInterval = 0;
                repeatNotBefore = 0;
                inSingleFrame = false;
            }
        }

        private void Expire(double now)
        {
            if (!hasReport || now - lastReport < ReportTimeout) return;
            bool moving = direction != 0;
            Reset();
            timedOut = moving;
        }

        internal int Step(double now, JogSettings settings)
        {
            return Step(now, settings, null);
        }

        internal int Step(double now, JogSettings settings, Func<double, double> rate)
        {
            Expire(now);
            if (!hasReport || !armed || Math.Abs(value) <= settings.Deadzone / 100.0) return 0;
            int nextDirection = Math.Sign(value) * (settings.Reverse ? -1 : 1);
            bool singleFrame = settings.IsSingleFrame(value);
            double interval = singleFrame ? 0 : 1000 / (rate == null ? settings.Rate(value) : rate(value));
            if (direction != nextDirection)
            {
                direction = nextDirection;
                inSingleFrame = singleFrame;
                due = now + interval;
                previousInterval = interval;
                repeatNotBefore = now + settings.RepeatDelayMs;
                return direction;
            }
            if (singleFrame)
            {
                inSingleFrame = true;
                previousInterval = 0;
                return 0;
            }
            if (inSingleFrame)
            {
                inSingleFrame = false;
                due = now;
            }
            if (previousInterval > 0 && now < due)
                due = now + (due - now) * interval / previousInterval;
            previousInterval = interval;
            if (now < repeatNotBefore) return 0;
            due = Math.Max(due, repeatNotBefore);
            if (now < due) return 0;
            // Batch normal timer intervals, but discard backlog after a UI stall.
            if (now - due > 50) due = now;
            int steps = Math.Min(6, 1 + (int)Math.Floor((now - due + 0.000001) / interval));
            due += steps * interval;
            return direction * steps;
        }

        internal static Native.KeyboardInput[] Inputs(int direction)
        {
            return Inputs(direction, 1);
        }

        internal static Native.KeyboardInput[] Inputs(int direction, int steps)
        {
            if ((direction != -1 && direction != 1) || steps < 1 || steps > 6)
                throw new InvalidOperationException("Ungueltige Jog-Richtung oder Schrittzahl.");
            ushort key = direction < 0 ? (ushort)0x25 : (ushort)0x27;
            var inputs = new Native.KeyboardInput[steps * 2];
            for (int i = 0; i < steps; i++)
            {
                inputs[i * 2] = BrowserCommand.Key(key, false);
                inputs[i * 2 + 1] = BrowserCommand.Key(key, true);
            }
            return inputs;
        }
    }
}
