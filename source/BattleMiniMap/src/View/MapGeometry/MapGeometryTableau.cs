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
            public volatile bool Retired, Enabled;
            public int PaintCount;
            public string Failure, Name;
        }
        private static int _nextId, _unboundErrorReported;
        private readonly Binding _binding;
        private Texture _target;
        private TableauView _view;
        public UiTexture Texture { get; private set; }
        public string Name => _binding.Name;
        public int PaintCount => Volatile.Read(ref _binding.PaintCount);
        public string Failure => _binding.Failure;
        public MapGeometryTableau(Scene scene, Camera camera)
        {
            _binding = new Binding { Scene = scene, Camera = camera,
                Name = "BattleMiniMap.MainGeometry." + Interlocked.Increment(ref _nextId) };
        }
        public void Initialize(int width, int height)
        {
            _binding.Enabled = true;
            _target = TableauView.AddTableau(Name, Paint, _binding, width, height);
            _view = _target.TableauView;
            Configure(_view, _binding);
            Texture = new UiTexture(new EngineTexture(_target));
        }
        public void SetEnabled(bool enabled)
        {
            _binding.Enabled = enabled;
            if (_view == null) return;
            _view.SetDoNotRenderThisFrame(!enabled);
            _view.SetEnable(enabled);
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
        private static void Paint(Texture sender, EventArgs args)
        {
            Binding binding = null;
            try
            {
                if (sender == null || sender.IsReleased || !sender.IsValid) return;
                binding = sender.UserData as Binding;
                if (binding == null || binding.Retired || !binding.Enabled) return;
                var view = sender.TableauView;
                Configure(view, binding);
                view.SetDoNotRenderThisFrame(true);
                if (Interlocked.Increment(ref binding.PaintCount) == 1)
                    TaleWorlds.Library.Debug.Print("BattleMiniMap main geometry callback: target=" + binding.Name);
            }
            catch (Exception ex)
            {
                if (binding != null && !binding.Retired) binding.Failure = "Main Tableau callback: " + ex.Message;
                else if (Interlocked.Exchange(ref _unboundErrorReported, 1) == 0)
                    TaleWorlds.Library.Debug.Print("BattleMiniMap ignored unbound main geometry callback: " + ex.Message);
            }
        }
        public void Retire()
        {
            _binding.Retired = true; _binding.Enabled = false; _binding.Scene = null; _binding.Camera = null;
            if (_view != null)
            {
                _view.SetDoNotRenderThisFrame(true); _view.SetContinuousRendering(false);
                _view.SetEnable(false); _view.AddClearTask(clearOnlySceneview: true);
            }
        }
        public void Release() { Texture = null; _target?.Release(); _target = null; _view = null; }
    }
}
