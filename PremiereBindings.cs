using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    public enum PremiereFunction { None, Jog, HorizontalScroll, VerticalScroll, TimelineZoom, Playback, Shortcut, ContextMenu }

    public sealed class PremiereShortcut
    {
        public int Key { get; set; }
        public bool Control { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }

        internal void Validate()
        {
            if (Key < 8 || Key > 254 || !Enum.IsDefined(typeof(Keys), Key) ||
                new[] { 16, 17, 18, 91, 92, 93, 160, 161, 162, 163, 164, 165 }.Contains(Key))
                throw new InvalidDataException("Tastenkuerzel: eine regulaere Taste mit optional Strg/Umschalt/Alt erforderlich.");
        }

        internal Native.KeyboardInput[] Inputs()
        {
            Validate();
            var keys = new List<ushort>();
            if (Control) keys.Add(0x11);
            if (Shift) keys.Add(0x10);
            if (Alt) keys.Add(0x12);
            keys.Add((ushort)Key);
            return keys.Select(k => BrowserCommand.Key(k, false))
                .Concat(keys.AsEnumerable().Reverse().Select(k => BrowserCommand.Key(k, true))).ToArray();
        }

        public override string ToString()
        {
            return (Control ? "Strg+" : "") + (Shift ? "Umschalt+" : "") + (Alt ? "Alt+" : "") + (Keys)Key;
        }
    }

    public sealed class PremiereBinding
    {
        public string Input { get; set; }
        public PremiereFunction Function { get; set; }
        public JogSettings Response { get; set; }
        public int? EndRate { get; set; }
        public int Trigger { get; set; }
        public PremiereShortcut Negative { get; set; }
        public PremiereShortcut Positive { get; set; }

        internal bool Button { get { return Input == "Button1" || Input == "Button2"; } }
        internal bool Discrete { get { return Function == PremiereFunction.Playback || Function == PremiereFunction.Shortcut; } }
        internal bool Wheel { get { return Function == PremiereFunction.HorizontalScroll || Function == PremiereFunction.VerticalScroll; } }
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(PremiereFunction), Function) || Response == null)
                throw new InvalidDataException("Ungueltige Premiere-Funktion oder fehlende Kennlinie.");
            Response.Validate();
            if (Function == PremiereFunction.ContextMenu && !Button)
                throw new InvalidDataException("Das Bridge-Menue bitte einer SpaceMouse-Taste zuweisen.");
            if (Trigger <= Response.Deadzone || Trigger > 95)
                throw new InvalidDataException(Input + ": Ausloeseschwelle muss ueber der Totzone und bei hoechstens 95 % liegen.");
            if (EndRate.HasValue && (EndRate < Response.MaximumRate || EndRate > 120))
                throw new InvalidDataException(Input + ": Endtempo muss zwischen Maximum und 120 liegen.");
            if (Function == PremiereFunction.Shortcut &&
                (Positive == null || !Button && Negative == null))
                throw new InvalidDataException(Input + ": Tastenkuerzel fuer beide Richtungen bzw. die Taste aufzeichnen.");
            if (Negative != null) Negative.Validate();
            if (Positive != null) Positive.Validate();
        }

        internal double Rate(double value)
        {
            double start = Math.Max(0.6, Response.ContinuousStart);
            double extra = Math.Max(0, (Math.Min(1, Math.Abs(value)) - start) / (1 - start));
            return Response.Rate(value) + ((EndRate ?? Response.MaximumRate) - Response.MaximumRate) * extra * extra;
        }

        internal Native.KeyboardInput[] Inputs(int steps)
        {
            if (steps == 0 || Math.Abs((long)steps) > 6)
                throw new InvalidOperationException("Ungueltige Premiere-Schrittzahl.");
            int direction = Math.Sign(steps);
            switch (Function)
            {
                case PremiereFunction.Jog: return JogController.Inputs(direction, Math.Abs(steps));
                case PremiereFunction.HorizontalScroll: return TimelineScrollSettings.Inputs(steps);
                case PremiereFunction.VerticalScroll:
                    return new[] { BrowserCommand.Key(0x11, false) }
                        .Concat(TimelineScrollSettings.Inputs(steps))
                        .Concat(new[] { BrowserCommand.Key(0x11, true) }).ToArray();
                case PremiereFunction.TimelineZoom:
                    return Enumerable.Range(0, Math.Abs(steps)).SelectMany(i => new[] {
                        BrowserCommand.Key(direction < 0 ? (ushort)0xbd : (ushort)0xbb, false),
                        BrowserCommand.Key(direction < 0 ? (ushort)0xbd : (ushort)0xbb, true) }).ToArray();
                case PremiereFunction.Playback:
                    return new[] { BrowserCommand.Key(0x20, false), BrowserCommand.Key(0x20, true) };
                case PremiereFunction.Shortcut:
                    return (Button || direction > 0 ? Positive : Negative).Inputs();
                default: throw new InvalidOperationException("Keine Premiere-Ausgabefunktion ausgewaehlt.");
            }
        }
    }

    public sealed class PremiereBindings
    {
        public PremiereBinding[] Items { get; set; }
        internal static readonly string[] Names = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Button1", "Button2" };

        internal static PremiereBindings From(ApplicationMapping mapping)
        {
            if (mapping.PremiereInputs != null) return mapping.PremiereInputs;
            var result = new PremiereBindings {
                Items = Names.Select(n => new PremiereBinding {
                    Input = n, Function = PremiereFunction.None, Trigger = 45,
                    Response = new JogSettings { Deadzone = 12, MinimumRate = 0.5m, MaximumRate = 24, CurveExponent = 2 }
                }).ToArray()
            };
            if (mapping.Jog != null)
            {
                result.Items[5].Function = PremiereFunction.Jog;
                result.Items[5].Response = mapping.Clone().Jog;
            }
            if (mapping.TimelineScroll != null)
            {
                var x = result.Items[0];
                x.Function = mapping.TimelineScroll.Enabled ? PremiereFunction.HorizontalScroll : PremiereFunction.None;
                x.Response = mapping.TimelineScroll.Timing();
                x.EndRate = mapping.TimelineScroll.EndRate;
            }
            return result;
        }

        internal void Validate()
        {
            if (Items == null || Items.Length != 8 || Items.Any(b => b == null) ||
                !Items.Select(b => b.Input).OrderBy(n => n).SequenceEqual(Names.OrderBy(n => n)))
                throw new InvalidDataException("Premiere benoetigt sechs eindeutige Achsen und zwei Tasten.");
            foreach (PremiereBinding item in Items) item.Validate();
        }
    }

    internal sealed class PremiereEmission
    {
        internal PremiereBinding Binding;
        internal int Steps;
        internal double CreatedAt;
        internal Native.KeyboardInput[] Inputs { get { return Binding.Inputs(Steps); } }
    }

    internal sealed class PremiereBindingRuntime
    {
        private sealed class State
        {
            internal readonly JogController Motion = new JogController();
            internal bool Armed, Held;
            internal int Pending;
            internal Device ButtonSource;
            internal double LastReport = double.NegativeInfinity;
            internal bool Expired(double now) { return now - LastReport >= JogController.ReportTimeout; }
        }
        private readonly Dictionary<string, State> states = new Dictionary<string, State>();
        private readonly List<PremiereEmission> pending = new List<PremiereEmission>();
        internal void Reset() { states.Clear(); pending.Clear(); }

        private State For(string name)
        {
            State state;
            if (!states.TryGetValue(name, out state)) states.Add(name, state = new State());
            return state;
        }

        internal void Receive(Device device, PremiereBindings bindings, double now)
        {
            foreach (PremiereBinding binding in bindings.Items.Where(b => b.Function != PremiereFunction.None))
            {
                State state = For(binding.Input);
                if (binding.Button)
                {
                    ushort button = binding.Input == "Button1" ? (ushort)1 : (ushort)2;
                    var groups = device.Buttons.Where(g => g.Available.Contains(button)).ToArray();
                    if (groups.Length == 0 || groups.Any(g => !g.Seen || g.ValueReport != device.Reports)) continue;
                    if (state.ButtonSource != device)
                    {
                        state.ButtonSource = device;
                        state.Armed = false;
                        state.Held = false;
                    }
                    bool down = groups.Any(g => g.Pressed.Contains(button));
                    if (!state.Armed) { if (!down) state.Armed = true; continue; }
                    if (state.Held && !down)
                        pending.Add(new PremiereEmission { Binding = binding, Steps = binding.Response.Reverse ? -1 : 1, CreatedAt = now });
                    state.Held = down;
                    continue;
                }
                Axis axis = device.Axes.SingleOrDefault(a => a.Name == binding.Input);
                if (axis == null) continue;
                if (axis.ReportId != device.LastReportId || axis.ValueReport != device.Reports || !axis.Value.HasValue) continue;
                double range = axis.Value < 0 ? -(double)axis.Minimum : axis.Maximum;
                if (range <= 0) throw new InvalidOperationException("Ungueltiger Achsenbereich: " + axis.Name);
                double value = axis.Value.Value / range;
                if (!binding.Discrete)
                {
                    state.Motion.Receive(value, now, binding.Response);
                    continue;
                }
                if (state.Expired(now)) { state.Armed = false; state.Pending = 0; }
                state.LastReport = now;
                if (Math.Abs(value) <= binding.Response.Deadzone / 100.0)
                {
                    if (state.Armed && state.Pending != 0)
                        pending.Add(new PremiereEmission { Binding = binding, Steps = state.Pending, CreatedAt = now });
                    state.Armed = true;
                    state.Pending = 0;
                }
                else if (state.Armed && state.Pending == 0 && Math.Abs(value) >= binding.Trigger / 100.0)
                    state.Pending = Math.Sign(value) * (binding.Response.Reverse ? -1 : 1);
            }
        }

        internal PremiereEmission[] Step(PremiereBindings bindings, double now, Action<string> log)
        {
            var output = pending.Where(p => now - p.CreatedAt < JogController.ReportTimeout).ToList();
            if (output.Count != pending.Count) log("Premiere: veraltete Einzelaktion nach Timerverzoegerung verworfen.");
            pending.Clear();
            foreach (PremiereBinding binding in bindings.Items.Where(b => !b.Button && b.Function != PremiereFunction.None))
            {
                State state = For(binding.Input);
                if (binding.Discrete)
                {
                    if (state.Expired(now))
                    {
                        if (state.Pending != 0) log("Premiere " + binding.Input + ": Aktion nach Datenabbruch verworfen.");
                        state.Pending = 0;
                        state.Armed = false;
                    }
                    continue;
                }
                int steps = state.Motion.Step(now, binding.Response, binding.Rate);
                if (state.Motion.TakeTimeout()) log("Premiere " + binding.Input + ": keine frischen Achsendaten seit 250 ms; bitte neutral stellen.");
                if (steps != 0) output.Add(new PremiereEmission { Binding = binding, Steps = steps });
            }
            return output.ToArray();
        }
    }
}
