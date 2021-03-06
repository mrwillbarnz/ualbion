﻿using System;
using System.Collections.Generic;
using System.Linq;
using UAlbion.Api;
using UAlbion.Core.Events;
using UAlbion.Core.Textures;
using Veldrid;

namespace UAlbion.Core
{
    public class Scene : Component, IScene
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<Scene, CollectScenesEvent>((x, e) => e.Register(x)),
            H<Scene, SetClearColourEvent>((x, e) => x._clearColour = new RgbaFloat(e.Red, e.Green, e.Blue, 1.0f))
        );

        readonly IDictionary<Type, IList<IRenderable>> _renderables = new Dictionary<Type, IList<IRenderable>>();
        readonly IDictionary<(DrawLayer, int), IList<IRenderable>> _processedRenderables = new Dictionary<(DrawLayer, int), IList<IRenderable>>();
        readonly IList<Type> _activeRendererTypes;
        PaletteTexture _paletteTexture;
        RgbaFloat _clearColour;

        public string Name { get; }
        public ICamera Camera { get; }

        protected Scene(string name, ICamera camera, IList<Type> activeRendererTypes) : base(Handlers)
        {
            Name = name;
            Camera = camera;
            Children.Add(Camera);
            _activeRendererTypes = activeRendererTypes;
        }

        public void Add(IRenderable renderable) { } // TODO
        public void Remove(IRenderable renderable) { } // TODO
        public override string ToString() => $"Scene:{Name}";

        public void RenderAllStages(GraphicsDevice gd, CommandList cl, SceneContext sc, IDictionary<Type, IRenderer> renderers)
        {
            sc.SetCurrentScene(this);

            // Collect all renderables from components
            foreach(var renderer in _renderables.Values)
                renderer.Clear();

            using (PerfTracker.FrameEvent("6.1.1 Collect renderables"))
            {
                Exchange.Raise(new RenderEvent(x =>
                {
                    if (x == null || !_activeRendererTypes.Contains(x.Renderer))
                        return;
                    if (!_renderables.ContainsKey(x.Renderer))
                        _renderables[x.Renderer] = new List<IRenderable>();
                    _renderables[x.Renderer].Add(x);
                }), this);
            }

            foreach(var renderer in _renderables)
                CoreTrace.Log.CollectedRenderables(renderer.Key.Name, 0, renderer.Value.Count);

            var newPalette = Resolve<IPaletteManager>().PaletteTexture;
            if (sc.PaletteView == null || _paletteTexture != newPalette)
            {
                sc.PaletteView?.Dispose();
                sc.PaletteTexture?.Dispose();
                CoreTrace.Log.Info("Scene", "Disposed palette device texture");
                _paletteTexture = newPalette;
                sc.PaletteTexture = _paletteTexture.CreateDeviceTexture(gd, gd.ResourceFactory, TextureUsage.Sampled);
                sc.PaletteView = gd.ResourceFactory.CreateTextureView(sc.PaletteTexture);
            }
            CoreTrace.Log.Info("Scene", "Created palette device texture");

            using (PerfTracker.FrameEvent("6.1.2 Prepare per-frame resources"))
            using (new RenderDebugGroup(cl, "Prepare per-frame resources"))
            {
                _processedRenderables.Clear();
                foreach (var renderableGroup in _renderables)
                {
                    var renderer = renderers[renderableGroup.Key];
                    foreach (var renderable in renderer.UpdatePerFrameResources(gd, cl, sc, renderableGroup.Value))
                    {
                        var key = (renderable.RenderOrder, renderable.PipelineId);
                        if (!_processedRenderables.ContainsKey(key))
                            _processedRenderables[key] = new List<IRenderable>();
                        _processedRenderables[key].Add(renderable);
                    }
                }

                CoreTrace.Log.CollectedRenderables("ProcessedRenderables",
                    _processedRenderables.Count,
                    _processedRenderables.Sum(x => x.Value.Count));

                sc.UpdatePerFrameResources(gd, cl);
            }

            var orderedKeys = _processedRenderables.Keys.OrderBy(x => x).ToList();
            CoreTrace.Log.Info("Scene", "Sorted processed renderables");
            float depthClear = gd.IsDepthRangeZeroToOne ? 1f : 0f;

            // Main scene
            using (PerfTracker.FrameEvent("6.1.3 Main scene pass"))
            using (new RenderDebugGroup(cl, "Main Scene Pass"))
            {
                cl.SetFramebuffer(sc.MainSceneFramebuffer);
                var fbWidth = sc.MainSceneFramebuffer.Width;
                var fbHeight = sc.MainSceneFramebuffer.Height;
                cl.SetViewport(0, new Viewport(0, 0, fbWidth, fbHeight, 0, 1));
                cl.SetFullViewports();
                cl.SetFullScissorRects();
                cl.ClearColorTarget(0, _clearColour);
                cl.ClearDepthStencil(depthClear);
                foreach (var key in orderedKeys)
                    Render(gd, cl, sc, RenderPasses.Standard, renderers, _processedRenderables[key]);
            }

            // 2D Overlays
            using (new RenderDebugGroup(cl, "Overlay"))
            {
                foreach (var key in orderedKeys)
                    Render(gd, cl, sc, RenderPasses.Overlay, renderers, _processedRenderables[key]);
            }

            if (sc.MainSceneColorTexture.SampleCount != TextureSampleCount.Count1)
                cl.ResolveTexture(sc.MainSceneColorTexture, sc.MainSceneResolvedColorTexture);

            using (new RenderDebugGroup(cl, "Duplicator"))
            {
                cl.SetFramebuffer(sc.DuplicatorFramebuffer);
                cl.SetFullViewports();
                foreach (var key in orderedKeys)
                    Render(gd, cl, sc, RenderPasses.Duplicator, renderers, _processedRenderables[key]);
            }

            using (new RenderDebugGroup(cl, "Swapchain Pass"))
            {
                cl.SetFramebuffer(gd.SwapchainFramebuffer);
                cl.SetFullViewports();
                foreach (var key in orderedKeys)
                    Render(gd, cl, sc, RenderPasses.SwapchainOutput, renderers, _processedRenderables[key]);
            }
        }

        void Render(GraphicsDevice gd,
            CommandList cl,
            SceneContext sc,
            RenderPasses pass,
            IDictionary<Type, IRenderer> renderers,
            IEnumerable<IRenderable> renderableList)
        {
            foreach (IRenderable renderable in renderableList)
            {
                var renderer = renderers[renderable.Renderer];
                if ((renderer.RenderPasses & pass) != 0)
                    renderer.Render(gd, cl, sc, pass, renderable);
            }
        }
    }
}
