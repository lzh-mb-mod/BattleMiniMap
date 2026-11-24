using System;
using System.Collections.Generic;
using System.Globalization;

namespace BattleMiniMap.View.MapGeometry
{
    // Tick ENTRY intervals and managed Update time; never GPU execution time.
    internal sealed class MapGeometryPerformance
    {
        private const int Capacity = 8192;
        private readonly double[] _cpu = new double[Capacity], _tick = new double[Capacity];
        private sealed class Counters { public int LongGaps, Dropped; }
        private readonly Dictionary<string, Counters> _counters = new Dictionary<string, Counters>();
        private string _context, _backend;
        private double _last, _warmUntil, _start;
        private bool _started;
        private int _count;
        public void Reset()
        {
            if (_backend != null && _count > 0) _counters[_backend].Dropped += _count;
            _context = _backend = null; _count = 0; _last = _warmUntil = _start = 0; _started = false;
        }
        private void Begin(string context, string backend, double now)
        {
            Reset(); _context = context; _backend = backend; _last = now; _warmUntil = now + 2;
        }
        public string Record(string context, double now, double cpuMs, bool paused, string backend = null)
        {
            if (context == null || paused || double.IsNaN(now) || double.IsInfinity(now) ||
                cpuMs < 0 || double.IsNaN(cpuMs) || double.IsInfinity(cpuMs)) { Reset(); return null; }
            backend = backend ?? context;
            if (!_counters.TryGetValue(backend, out var counters)) _counters.Add(backend, counters = new Counters());
            if (_context != context) { Begin(context, backend, now); return null; }
            var gap = now - _last;
            if (gap <= 0) { Begin(context, backend, now); return null; }
            if (gap > .5)
            {
                ++counters.LongGaps;
                Begin(context, backend, now);
                return string.Format(CultureInfo.InvariantCulture,
                    "{0} event=reset reason=long_gap gap_ms={1:F3} long_gap_resets={2} dropped_samples={3}",
                    context, gap * 1000, counters.LongGaps, counters.Dropped);
            }
            _last = now;
            if (now < _warmUntil) return null;
            if (!_started) { _start = now; _started = true; return null; }
            if (_count == Capacity)
            {
                Begin(context, backend, now);
                return context + " event=reset reason=capacity dropped_samples=" + counters.Dropped;
            }
            _cpu[_count] = cpuMs; _tick[_count++] = gap * 1000;
            if (now - _start < 5) return null;
            double cpuTotal = 0, tickTotal = 0;
            for (var i = 0; i < _count; ++i) { cpuTotal += _cpu[i]; tickTotal += _tick[i]; }
            Array.Sort(_cpu, 0, _count); Array.Sort(_tick, 0, _count);
            var p95 = Math.Max(0, (int)Math.Ceiling(_count * .95) - 1);
            var text = string.Format(CultureInfo.InvariantCulture,
                "{0} event=window samples={1} window_s={2:F3} update_cpu_mean_ms={3:F4} update_cpu_p95_ms={4:F4} mission_tick_mean_ms={5:F4} mission_tick_p95_ms={6:F4} long_gap_resets={7} dropped_samples={8}",
                _context, _count, now - _start, cpuTotal / _count, _cpu[p95], tickTotal / _count, _tick[p95], counters.LongGaps, counters.Dropped);
            _count = 0; _started = false;
            return text;
        }
    }
    internal struct MapGeometryPerformanceContext : IEquatable<MapGeometryPerformanceContext>
    {
        public bool Gpu, Proxy, Tint, Follow, Dynamic, DiagnosticDepth, MarkersRequested, MarkerProfile;
        public int Generation, Width, Diagnostic;
        public float Zoom;
        public bool Equals(MapGeometryPerformanceContext o) => Gpu == o.Gpu && Proxy == o.Proxy && Tint == o.Tint &&
            Follow == o.Follow && Dynamic == o.Dynamic && DiagnosticDepth == o.DiagnosticDepth && MarkersRequested == o.MarkersRequested &&
            MarkerProfile == o.MarkerProfile && Generation == o.Generation && Width == o.Width && Diagnostic == o.Diagnostic && Zoom.Equals(o.Zoom);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "backend={0} generation={1} variant={2} tintApplied={3} follow={4} zoom={5:R} dynamic={6} width={7} diagnostic={8} diagnosticDepth={9} markersMeshRequested={10} markerProfile={11}",
            Gpu ? "GPU" : "CPU", Generation, Proxy ? "proxy" : "original", Tint, Follow, Zoom, Dynamic, Width, Diagnostic, DiagnosticDepth, MarkersRequested, MarkerProfile);
    }
}
