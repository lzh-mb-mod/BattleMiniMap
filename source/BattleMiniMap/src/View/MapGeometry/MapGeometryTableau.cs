using System;
using System.Threading;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapGeometry
{
    // Separate per-target lifetime. The renderer owns the scene and delayed release.
    internal sealed class MapGeometryTableau
    {
        private sealed class Binding
        {
            public Scene Scene;
            public Camera Camera;
            public volatile bool Retired, Enabled, Dynamic;
            public int Remaining = RequiredPaints;
            public int Generation, SubmittedGeneration = -1, PublicationRemaining = RequiredPaints;
            public int PaintCount, CallbackCount;
            public string Failure, Name;
        }
        private static int _nextId;
        private readonly Binding _binding;
        private Texture _target;
        private TableauView _view;
        private bool _parked, _needsPermit;
        public const int RequiredPaints = 2;
        public UiTexture Texture { get; private set; }
        public string Name => _binding.Name;
        public int PaintCount => Volatile.Read(ref _binding.PaintCount);
        public int CallbackCount => Volatile.Read(ref _binding.CallbackCount);
        // Previous cycle passed the engine submission path, not a GPU-completion fence.
        public bool HasSubmittedCache => PaintCount >= RequiredPaints && !_binding.Dynamic && Volatile.Read(ref _binding.Remaining) == 0;
        // Readiness counts total submissions; dirty content only resets the idle tail.
        public void SetContentActive(bool active)
        {
            lock (_binding)
            {
                _binding.Dynamic = active;
                if (active) { _binding.Remaining = RequiredPaints; _needsPermit = true; }
            }
        }
        public int InvalidateContent()
        {
            lock (_binding)
            {
                _binding.Remaining = _binding.PublicationRemaining = RequiredPaints;
                _needsPermit = true;
                return ++_binding.Generation;
            }
        }
        public bool IsContentSubmitted(int generation) => Volatile.Read(ref _binding.SubmittedGeneration) >= generation;
        public string Failure => _binding.Failure;
        public MapGeometryTableau(Scene scene, Camera camera)
        {
            _binding = new Binding { Scene = scene, Camera = camera,
                Name = "BattleMiniMap.MainGeometry." + Interlocked.Increment(ref _nextId) };
        }
        public void Initialize(int width, int height)
        {
            lock (_binding)
            {
                if (_binding.Retired) return;
                _binding.Enabled = true;
                var binding = _binding;
                _target = TableauView.AddTableau(Name, (sender, args) => Paint(binding, sender), null, width, height);
                _view = _target.TableauView;
                Configure(_view, _binding);
                _view.SetDoNotRenderThisFrame(false);
                Texture = new UiTexture(new EngineTexture(_target));
            }
        }
        public void SetEnabled(bool enabled)
        {
            lock (_binding)
            {
                if (_binding.Retired) return;
                var wasEnabled = _binding.Enabled;
                _binding.Enabled = enabled;
                if (_view == null) return;
                if (!enabled)
                {
                    if (wasEnabled) { _view.SetDoNotRenderThisFrame(true); _view.SetEnable(false); }
                    return;
                }
                if (HasSubmittedCache)
                {
                    if (!_parked || !wasEnabled)
                    {
                        // Keep the view alive: an already admitted cycle must finish normally.
                        _view.SetDoNotRenderThisFrame(true); _view.SetEnable(true);
                        if (!_parked) TaleWorlds.Library.Debug.Print("BattleMiniMap main geometry cached: target=" + Name +
                            " paints=" + PaintCount + " callbacks=" + CallbackCount + " policy=demand");
                        _parked = true;
                    }
                    return;
                }
                if (!wasEnabled || _parked || _needsPermit)
                { _view.SetDoNotRenderThisFrame(false); _view.SetEnable(true); }
                _parked = _needsPermit = false;
            }
        }
        private static void Configure(TableauView view, Binding binding)
        {
            binding.Scene.EnsurePostfxSystem();
            view.SetScene(binding.Scene); view.SetCamera(binding.Camera);
            view.SetClearColor(0);
            view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearColor, true);
            view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearDepth, true);
            view.SetCleanScreenUntilLoadingDone(false); view.SetClearAndDisableAfterSucessfullRender(false);
            view.SetRenderWithPostfx(false); view.SetSceneUsesSkybox(false); view.SetSceneUsesShadows(false); view.SetSceneUsesContour(false);
            view.SetAcceptGlobalDebugRenderObjects(false); view.SetRenderOrder(-2);
            view.SetDeleteAfterRendering(false); view.SetContinuousRendering(true); view.SetEnable(true);
        }
        private static void Paint(Binding binding, Texture sender)
        {
            try
            {
                lock (binding)
                {
                    // Retired/hidden gates precede every sender/native property.
                    if (binding.Retired || !binding.Enabled || sender == null) return;
                    if (sender.IsReleased || !sender.IsValid) return;
                    var view = sender.TableauView;
                    var callbacks = Interlocked.Increment(ref binding.CallbackCount);
                    if ((!binding.Dynamic && Volatile.Read(ref binding.Remaining) == 0) || binding.Failure != null)
                    {
                        view.SetDoNotRenderThisFrame(true);
                        if (callbacks == RequiredPaints + 1)
                            TaleWorlds.Library.Debug.Print("BattleMiniMap main geometry extra callback: target=" + binding.Name + " callbacks=" + callbacks);
                        return;
                    }
                    Configure(view, binding);
                    // The publication generation is independent of the collector's idle
                    // tail. Invalidation and callback completion share this small gate.
                    if (binding.PublicationRemaining > 0 && --binding.PublicationRemaining == 0)
                        Volatile.Write(ref binding.SubmittedGeneration, binding.Generation);
                    if (!binding.Dynamic && Volatile.Read(ref binding.Remaining) > 0) Interlocked.Decrement(ref binding.Remaining);
                    view.SetDoNotRenderThisFrame(!binding.Dynamic && Volatile.Read(ref binding.Remaining) == 0);
                    if (Interlocked.Increment(ref binding.PaintCount) == 1)
                        TaleWorlds.Library.Debug.Print("BattleMiniMap main geometry callback: target=" + binding.Name);
                }
            }
            catch (Exception ex)
            {
                lock (binding)
                    if (!binding.Retired) binding.Failure = "Main Tableau callback: " + ex.Message;
            }
        }
        public void Retire()
        {
            lock (_binding)
            {
                if (_binding.Retired) return;
                _binding.Retired = true; _binding.Enabled = false; _binding.Scene = null; _binding.Camera = null;
                if (_view != null)
                {
                    try { _view.SetDoNotRenderThisFrame(true); }
                    finally
                    {
                        try { _view.SetContinuousRendering(false); }
                        finally
                        {
                            try { _view.SetEnable(false); }
                            finally { _view.AddClearTask(clearOnlySceneview: true); }
                        }
                    }
                }
            }
        }
        public void Release()
        {
            lock (_binding)
            {
                try { Retire(); }
                finally { Texture = null; _target?.Release(); _target = null; _view = null; }
            }
        }
    }
}
