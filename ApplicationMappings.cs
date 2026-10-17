using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SpaceMouseBridge
{
    internal sealed class RotationGesture
    {
        private bool armed;
        private bool neutral;
        internal bool Armed { get { return armed; } }
        internal double Threshold = 0.45;
        internal double Neutral = 0.12;
        internal void Expire() { if (!neutral) armed = false; }
        internal void Reset() { armed = false; neutral = false; }

        internal int Update(double value)
        {
            neutral = Math.Abs(value) <= Neutral;
            if (neutral) { armed = true; return 0; }
            if (!armed || Math.Abs(value) < Threshold) return 0;
            armed = false;
            return value > 0 ? 1 : -1;
        }
    }

    internal sealed class ButtonGesture
    {
        private bool armed;
        private bool pressed;
        internal bool Update(bool down)
        {
            if (!armed)
            {
                if (!down) armed = true;
                return false;
            }
            bool released = pressed && !down;
            pressed = down;
            return released;
        }
    }

    internal sealed class TabReleaseGate
    {
        private int pending;
        private DateTime requested;
        internal bool Expired;
        internal void Reset() { pending = 0; Expired = false; }
        internal void Request(int action, DateTime now)
        {
            if (pending != 0) return;
            pending = action;
            requested = now;
            Expired = false;
        }
        internal int Update(bool safe, DateTime now)
        {
            if (pending == 0) return 0;
            if ((now - requested).TotalSeconds > 5)
            {
                Reset();
                Expired = true;
                return 0;
            }
            if (!safe) return 0;
            int action = pending;
            Reset();
            return action;
        }
    }

    internal static class BrowserCommand
    {
        internal static Native.KeyboardInput Key(ushort key, bool up)
        {
            bool extended = key >= 0x21 && key <= 0x28 || key == 0x2d || key == 0x2e || key == 0x6f || key == 0x90;
            return new Native.KeyboardInput { Type = 1, Key = key, Flags = (up ? 2u : 0u) | (extended ? 1u : 0u) };
        }

        internal static Native.KeyboardInput[] Inputs(BrowserAction action)
        {
            ushort modifier = 0;
            ushort key;
            switch (action)
            {
                case BrowserAction.None: return new Native.KeyboardInput[0];
                case BrowserAction.Back: modifier = 0x12; key = 0x25; break;
                case BrowserAction.Forward: modifier = 0x12; key = 0x27; break;
                case BrowserAction.PreviousTab: modifier = 0x11; key = 0x21; break;
                case BrowserAction.NextTab: modifier = 0x11; key = 0x22; break;
                case BrowserAction.ZoomIn: modifier = 0x11; key = 0xbb; break;
                case BrowserAction.ZoomOut: modifier = 0x11; key = 0xbd; break;
                case BrowserAction.ResetZoom: modifier = 0x11; key = 0x30; break;
                case BrowserAction.Reload: key = 0x74; break;
                case BrowserAction.Home: modifier = 0x11; key = 0x24; break;
                case BrowserAction.End: modifier = 0x11; key = 0x23; break;
                default: throw new InvalidOperationException("Unbekannte Browseraktion.");
            }
            return modifier == 0 ? new[] { Key(key, false), Key(key, true) } :
                new[] { Key(modifier, false), Key(key, false), Key(key, true), Key(modifier, true) };
        }

        internal static Native.KeyboardInput[] ReleasePartial(Native.KeyboardInput[] inputs, uint sent)
        {
            var down = new List<ushort>();
            foreach (Native.KeyboardInput input in inputs.Take(checked((int)sent)))
            {
                if (input.Type != 1) continue;
                if ((input.Flags & 2) != 0) down.Remove(input.Key);
                else if (!down.Contains(input.Key)) down.Add(input.Key);
            }
            down.Reverse();
            return down.Select(k => Key(k, true)).ToArray();
        }
    }

    internal sealed class ApplicationMappings
    {
        private readonly Dictionary<string, RotationGesture> gestures = new Dictionary<string, RotationGesture>();
        private readonly Dictionary<int, ButtonGesture> buttonGestures = new Dictionary<int, ButtonGesture>();
        private readonly TabReleaseGate releaseGate = new TabReleaseGate();
        private readonly Action<string> log;
        private readonly ProfileRouter router = new ProfileRouter();
        private readonly JogController jog = new JogController();
        private readonly JogController timelineScroll = new JogController();
        private readonly PremiereBindingRuntime premiereBindings = new PremiereBindingRuntime();
        private readonly Stopwatch jogClock = Stopwatch.StartNew();
        private int jogDirection;
        private int jogSteps;
        private IntPtr context;
        private uint contextProcess;
        private Device source;
        private DateTime lastReport;
        private ApplicationMapping mapping;
        internal ApplicationProfile ActiveProfile { get { return router.Current; } }
        internal bool Enabled;
        private bool menuOpen;
        private long resetVersion;

        internal ApplicationMappings(Action<string> logger)
        {
            log = logger;
        }

        internal void Configure(IEnumerable<ProfileSession> sessions)
        {
            router.Configure(sessions);
            context = IntPtr.Zero;
            contextProcess = 0;
            mapping = null;
            Reset();
        }

        internal void Reset()
        {
            resetVersion++;
            StopJogLog();
            gestures.Clear();
            buttonGestures.Clear();
            source = null;
            lastReport = DateTime.MinValue;
            releaseGate.Reset();
            jog.Reset();
            timelineScroll.Reset();
            premiereBindings.Reset();
        }

        private void StopJogLog()
        {
            if (jogDirection != 0)
                log("Premiere Jog gestoppt (" + jogSteps + " Schritte seit Richtungsstart).");
            jogDirection = 0;
            jogSteps = 0;
        }

        internal void Poll()
        {
            if (menuOpen) return;
            IntPtr window = Native.GetForegroundWindow();
            uint process;
            Native.GetWindowThreadProcessId(window, out process);
            if (window == context && process == contextProcess) return;
            var name = new StringBuilder(256);
            string processName = null;
            try
            {
                if (window != IntPtr.Zero && process != 0)
                {
                    Native.GetClassName(window, name, name.Capacity);
                    using (var target = Process.GetProcessById(checked((int)process)))
                        processName = target.ProcessName;
                }
            }
            catch (ArgumentException ex) { log("Vordergrundprozess beendet: " + ex.Message); }
            catch (InvalidOperationException ex) { log("Vordergrundprozess nicht verfuegbar: " + ex.Message); }
            catch (Win32Exception ex) { log("Vordergrundprozesspruefung fehlgeschlagen: " + ex.Message); }
            SelectContext(window, process, processName, name.ToString());
        }

        internal void SelectContext(IntPtr window, uint process, string processName, string windowClass)
        {
            ApplicationProfile previous = router.Current;
            if (router.Select(window, process, processName, windowClass)) Reset();
            context = window;
            contextProcess = process;
            mapping = router.ActiveMapping;
            if (previous != router.Current)
                log(router.Current == null ? "Vordergrundprofil: keine Bridge-Zuordnung." :
                    "Vordergrundprofil erkannt: " + router.Current.Name +
                    (router.Current.SupportsActions ? "." : " (Leerprofil, keine Bridge-Ausgabe)."));
        }

        internal void Receive(Device device)
        {
            if (menuOpen) return;
            if (device.Axes.Count != 6)
            {
                Poll();
                if (router.Current != ApplicationProfile.Premiere || mapping == null || mapping.PremiereInputs == null) return;
                if (!Enabled || !router.CanExecute || ModifiersDown(true)) { Reset(); return; }
                if (source == null) source = device;
                if (device.Buttons.Count > 0)
                    premiereBindings.Receive(device, mapping.PremiereInputs, jogClock.Elapsed.TotalMilliseconds);
                CompletePending();
                return;
            }
            Poll();
            if (!Enabled || !router.CanExecute || ModifiersDown(router.Current == ApplicationProfile.Premiere)) { Reset(); return; }
            DateTime now = DateTime.UtcNow;
            if (source != device) Reset();
            else if ((now - lastReport).TotalMilliseconds > 500)
                foreach (RotationGesture gesture in gestures.Values) gesture.Expire();
            source = device;
            lastReport = now;
            if (router.Current == ApplicationProfile.Premiere)
            {
                if (mapping.PremiereInputs != null)
                {
                    premiereBindings.Receive(device, mapping.PremiereInputs, jogClock.Elapsed.TotalMilliseconds);
                    CompletePending();
                    return;
                }
                ReceiveAxis(device, "Rz", jog, mapping.Jog, jogClock.Elapsed.TotalMilliseconds);
                if (mapping.TimelineScroll != null && mapping.TimelineScroll.Enabled)
                    ReceiveAxis(device, "X", timelineScroll, mapping.TimelineScroll.Timing(), jogClock.Elapsed.TotalMilliseconds);
                CompletePending();
                return;
            }
            foreach (AxisMapping assignment in mapping.Axes.Where(a => a.Mode == AxisMode.Bridge))
            {
                Axis axis = device.Axes.Single(a => a.Name == assignment.Axis);
                if (!axis.Value.HasValue) continue;
                RotationGesture gesture;
                if (!gestures.TryGetValue(axis.Name, out gesture))
                {
                    gesture = new RotationGesture { Threshold = mapping.Threshold / 100.0, Neutral = mapping.Neutral / 100.0 };
                    gestures.Add(axis.Name, gesture);
                }
                double range = axis.Value.Value < 0 ? -(double)axis.Minimum : axis.Maximum;
                if (range <= 0) throw new InvalidOperationException("Ungueltiger Achsenbereich: " + axis.Name);
                int direction = gesture.Update(axis.Value.Value / range);
                if (direction != 0) Request(direction < 0 ? assignment.Negative : assignment.Positive, now);
            }
            foreach (ButtonMapping assignment in mapping.Buttons.Where(b => b.Mode == ButtonMode.Bridge))
            {
                var groups = device.Buttons.Where(b => b.Available.Contains((ushort)assignment.Button)).ToArray();
                if (groups.Length == 0 || groups.Any(b => !b.Seen)) continue;
                ButtonGesture gesture;
                if (!buttonGestures.TryGetValue(assignment.Button, out gesture))
                {
                    gesture = new ButtonGesture();
                    buttonGestures.Add(assignment.Button, gesture);
                }
                if (gesture.Update(groups.Any(b => b.Pressed.Contains((ushort)assignment.Button))))
                    Request(assignment.Action, now);
            }
            CompletePending();
        }

        private void Request(BrowserAction action, DateTime now)
        {
            if (action != BrowserAction.None) releaseGate.Request((int)action, now);
        }

        internal static void ReceiveAxis(Device device, string name, JogController controller, JogSettings settings, double now)
        {
            Axis axis = device.Axes.Single(a => a.Name == name);
            if (axis.ReportId != device.LastReportId || axis.ValueReport != device.Reports || !axis.Value.HasValue) return;
            double range = axis.Value.Value < 0 ? -(double)axis.Minimum : axis.Maximum;
            if (range <= 0) throw new InvalidOperationException("Ungueltiger Achsenbereich: " + name);
            controller.Receive(axis.Value.Value / range, now, settings);
        }

        internal void CompletePending()
        {
            if (menuOpen) return;
            Poll();
            if (!Enabled || !router.CanExecute || source == null || source.Preparsed == IntPtr.Zero) { Reset(); return; }
            if (router.Current == ApplicationProfile.Premiere)
            {
                if (ModifiersDown(true)) { Reset(); return; }
                if (mapping.PremiereInputs != null)
                {
                    foreach (PremiereEmission emission in premiereBindings.Step(mapping.PremiereInputs, jogClock.Elapsed.TotalMilliseconds, log))
                    {
                        if (Native.GetForegroundWindow() != context || ModifiersDown(true)) { Reset(); return; }
                        if (emission.Binding.Function == PremiereFunction.ContextMenu)
                        {
                            OpenPremiereMenu();
                            return;
                        }
                        Send(emission.Inputs, "Premiere " + emission.Binding.Input + ": " + emission.Binding.Function);
                        if (emission.Binding.Button || emission.Binding.Discrete)
                            log("Premiere " + emission.Binding.Input + ": " + emission.Binding.Function);
                    }
                    return;
                }
                int steps = jog.Step(jogClock.Elapsed.TotalMilliseconds, mapping.Jog);
                if (jog.TakeTimeout()) log("Premiere Jog gestoppt: keine frischen Rz-Daten seit 250 ms. Bitte neutral stellen.");
                if (!jog.Moving) StopJogLog();
                int scrollSteps = mapping.TimelineScroll != null && mapping.TimelineScroll.Enabled ?
                    timelineScroll.Step(jogClock.Elapsed.TotalMilliseconds, mapping.TimelineScroll.Timing(), mapping.TimelineScroll.Rate) : 0;
                if (timelineScroll.TakeTimeout())
                    log("Premiere Timeline-Scrollen gestoppt: keine frischen X-Daten seit 250 ms. Bitte neutral stellen.");
                if (steps == 0 && scrollSteps == 0) return;
                if (Native.GetForegroundWindow() != context || ModifiersDown(true)) { Reset(); return; }
                if (scrollSteps != 0) Send(TimelineScrollSettings.Inputs(scrollSteps), "Premiere Timeline-Scrollen");
                if (steps == 0) return;
                if (Native.GetForegroundWindow() != context || ModifiersDown(true)) { Reset(); return; }
                int direction = Math.Sign(steps);
                Send(JogController.Inputs(direction, Math.Abs(steps)), "Premiere Jog");
                if (jogDirection != direction)
                {
                    StopJogLog();
                    jogDirection = direction;
                    log(direction < 0 ? "Premiere Jog: rueckwaerts." : "Premiere Jog: vorwaerts.");
                }
                jogSteps += Math.Abs(steps);
                return;
            }
            bool neutral = source.Axes.All(a => a.Value.HasValue && Math.Abs(a.Value.Value) <=
                (a.Value.Value < 0 ? -(double)a.Minimum : a.Maximum) * mapping.Neutral / 100.0);
            int action = releaseGate.Update(neutral && !ModifiersDown(true), DateTime.UtcNow);
            if (releaseGate.Expired)
            {
                log("Chrome-Aktion verworfen: 5 Sekunden ohne sichere Neutralstellung.");
                releaseGate.Reset();
            }
            if (action == 0) return;
            if (Native.GetForegroundWindow() != context || ModifiersDown(true)) { Reset(); return; }
            var inputs = BrowserCommand.Inputs((BrowserAction)action);
            Send(inputs, "Chrome-Aktion");
            log("Chrome: " + (BrowserAction)action);
        }

        private void OpenPremiereMenu()
        {
            IntPtr target = context;
            uint process = contextProcess;
            ApplicationMapping original = mapping;
            Reset();
            long version = resetVersion;
            menuOpen = true;
            try
            {
                log("Premiere: Bridge-Menue geoeffnet; Achsenausgabe pausiert.");
                using (var popup = new PremiereMenuPopup(mapping.PremiereMenu, System.Windows.Forms.Cursor.Position))
                {
                    bool selected = popup.ShowDialog() == System.Windows.Forms.DialogResult.OK && popup.SelectedShortcut != null;
                    uint foregroundProcess, targetProcess;
                    uint bridgeProcess;
                    using (var bridge = Process.GetCurrentProcess()) bridgeProcess = (uint)bridge.Id;
                    IntPtr foreground = Native.GetForegroundWindow();
                    Native.GetWindowThreadProcessId(foreground, out foregroundProcess);
                    Native.GetWindowThreadProcessId(target, out targetProcess);
                    if (!CanDispatchMenu(Enabled && resetVersion == version && mapping == original,
                        target, process, targetProcess, foreground, foregroundProcess,
                        bridgeProcess) || selected && ModifiersDown(true))
                    {
                        log("Premiere-Menueaktion verworfen: Fokus, Quelle, Profil oder Modifier haben sich geaendert.");
                        return;
                    }
                    if (foreground != target && !Native.SetForegroundWindow(target))
                    {
                        log("Premiere-Menueaktion verworfen: urspruengliches Fenster konnte nicht aktiviert werden.");
                        return;
                    }
                    if (!selected) return;
                    Native.GetWindowThreadProcessId(target, out targetProcess);
                    if (Native.GetForegroundWindow() != target || targetProcess != process || ModifiersDown(true))
                    {
                        log("Premiere-Menueaktion verworfen: Premiere hat nicht den Eingabefokus.");
                        return;
                    }
                    Send(popup.SelectedShortcut.Inputs(), "Premiere-Menueaktion");
                    log("Premiere-Menueaktion: " + popup.SelectedShortcut);
                }
            }
            finally
            {
                menuOpen = false;
                Reset();
            }
        }

        internal static bool CanDispatchMenu(bool unchanged, IntPtr target, uint process, uint targetProcess,
            IntPtr foreground, uint foregroundProcess, uint bridgeProcess)
        {
            return unchanged && target != IntPtr.Zero && process != 0 && targetProcess == process &&
                foreground != IntPtr.Zero && (foreground == target || foregroundProcess == bridgeProcess);
        }

        private void Send(Native.KeyboardInput[] inputs, string description)
        {
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.KeyboardInput)));
            if (sent != inputs.Length)
            {
                int error = Marshal.GetLastWin32Error();
                var release = BrowserCommand.ReleasePartial(inputs, sent);
                if (release.Length > 0 &&
                    Native.SendInput((uint)release.Length, release, Marshal.SizeOf(typeof(Native.KeyboardInput))) != release.Length)
                    log("FEHLER: Freigabe nach unvollstaendiger Tastatureingabe fehlgeschlagen.");
                throw new Win32Exception(error, description + " unvollstaendig gesendet. Ausgabe wird deaktiviert.");
            }
        }

        private static bool ModifiersDown(bool includeControl)
        {
            foreach (int key in new[] { 0x10, 0x12, 0x5b, 0x5c })
                if ((Native.GetAsyncKeyState(key) & 0x8000) != 0) return true;
            return includeControl && (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
        }
    }
}
