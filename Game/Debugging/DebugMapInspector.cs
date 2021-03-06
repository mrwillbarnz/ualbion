﻿using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using UAlbion.Api;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Core.Textures;
using UAlbion.Core.Visual;
using UAlbion.Game.Events;
using UAlbion.Game.Settings;
using UAlbion.Game.State;

namespace UAlbion.Game.Debugging
{
    [Event("hide_debug_window", "Hide the debug window")]
    public class HideDebugWindowEvent : Event { }

    public class DebugMapInspector : Component
    {
        static readonly HandlerSet Handlers = new HandlerSet(
            H<DebugMapInspector, EngineUpdateEvent>((x, _) => x.RenderDialog()),
            H<DebugMapInspector, HideDebugWindowEvent>((x, _) => x._hits = null),
            H<DebugMapInspector, ShowDebugInfoEvent>((x, e) =>
            {
                x._hits = e.Selections;
                x._mousePosition = e.MousePosition;
            }),
            H<DebugMapInspector, SetTextureOffsetEvent>((x, e) =>
            {
                EightBitTexture.OffsetX = e.X;
                EightBitTexture.OffsetY = e.Y;
            }),
            H<DebugMapInspector, SetTextureScaleEvent>((x, e) =>
            {
                EightBitTexture.ScaleAdjustX = e.X;
                EightBitTexture.ScaleAdjustY = e.Y;
            }));

        readonly IDictionary<Type, Action<DebugInspectorAction, Reflector.ReflectedObject>> _behaviours =
            new Dictionary<Type, Action<DebugInspectorAction, Reflector.ReflectedObject>>();

        IList<Selection> _hits;
        Vector2 _mousePosition;
        Reflector.ReflectedObject _lastHoveredItem;

        void RenderDialog()
        {
            if (_hits == null)
                return;

            var state = Resolve<IGameState>();
            var window = Resolve<IWindowManager>();
            if (state == null)
                return;

            var scene = Resolve<ISceneManager>().ActiveScene;
            Vector3 cameraPosition = scene.Camera.Position;
            Vector3 cameraTilePosition = cameraPosition;

            var map = Resolve<IMapManager>().Current;
            if (map != null)
                cameraTilePosition /= map.TileSize;

            Vector3 cameraDirection = scene.Camera.LookDirection;
            float cameraMagnification = scene.Camera.Magnification;

            ImGui.Begin("Inspector");
            ImGui.BeginChild("Inspector");
            if (ImGui.Button("Close"))
            {
                _hits = null;
                ImGui.EndChild();
                ImGui.End();
                return;
            }

            void BoolOption(string name, Func<bool> getter, Action<bool> setter)
            {
                bool value = getter();
                bool initialValue = value;
                ImGui.Checkbox(name, ref value);
                if (value != initialValue)
                    setter(value);
            }

            if (ImGui.TreeNode("Stats"))
            {
                if (ImGui.Button("Clear"))
                    PerfTracker.Clear();

                ImGui.BeginGroup();
                ImGui.Text(Resolve<IEngine>().FrameTimeText);

                var (descriptions, stats) = PerfTracker.GetFrameStats();
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 300);
                foreach (var description in descriptions)
                    ImGui.Text(description);

                ImGui.NextColumn();
                foreach (var stat in stats)
                    ImGui.Text(stat);

                ImGui.Columns(1);
                ImGui.EndGroup();
                if (ImGui.TreeNode("Textures"))
                {
                    ImGui.Text(Resolve<ITextureManager>()?.Stats());
                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("DeviceObjects"))
                {
                    ImGui.Text(Resolve<IDeviceObjectManager>()?.Stats());
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Settings"))
            {
                var settings = Resolve<ISettings>();
                ImGui.BeginGroup();
                BoolOption("DrawPositions", () => settings.Debug.DrawPositions,
                    x => Raise(new SetDrawPositionsEvent(x)));
                BoolOption("FlipDepthRange", () => settings.Engine.Flags.HasFlag(EngineFlags.FlipDepthRange),
                    x => Raise(new EngineFlagEvent(FlagOperation.Toggle, EngineFlags.FlipDepthRange)));
                BoolOption("FlipYSpace", () => settings.Engine.Flags.HasFlag(EngineFlags.FlipYSpace),
                    x => Raise(new EngineFlagEvent(FlagOperation.Toggle, EngineFlags.FlipYSpace)));
                BoolOption("HighlightEventChainZones", () => settings.Debug.HighlightEventChainZones,
                    x => Raise(new SetHighlightEventChainZonesEvent(x)));
                BoolOption("HighlightSelection", () => settings.Debug.HighlightSelection,
                    x => Raise(new SetHighlightSelectionEvent(x)));
                BoolOption("HighlightTile", () => settings.Debug.HighlightTile,
                    x => Raise(new SetHighlightTileEvent(x)));
                BoolOption("ShowBoundingBoxes", () => settings.Engine.Flags.HasFlag(EngineFlags.ShowBoundingBoxes),
                    x => Raise(new EngineFlagEvent(FlagOperation.Toggle, EngineFlags.ShowBoundingBoxes)));
                BoolOption("ShowCameraPosition", () => settings.Engine.Flags.HasFlag(EngineFlags.ShowCameraPosition),
                    x => Raise(new EngineFlagEvent(FlagOperation.Toggle, EngineFlags.ShowCameraPosition)));
                BoolOption("ShowPaths", () => settings.Debug.ShowPaths, x => Raise(new SetShowPathsEvent(x)));
                BoolOption("VSync", () => settings.Engine.Flags.HasFlag(EngineFlags.VSync),
                    x => Raise(new EngineFlagEvent(FlagOperation.Toggle, EngineFlags.VSync)));
                ImGui.EndGroup();
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Positions"))
            {
                var normPos = window.PixelToNorm(_mousePosition);
                var uiPos = window.NormToUi(normPos);
                uiPos.X = (int)uiPos.X;
                uiPos.Y = (int)uiPos.Y;
                ImGui.Text(
                    $"Cursor Pix: {_mousePosition} UI: {uiPos} Norm: {normPos} Scale: {window.GuiScale} PixSize: {window.Size}");
                ImGui.Text(
                    $"Camera World: {cameraPosition} Tile: {cameraTilePosition} Dir: {cameraDirection} Mag: {cameraMagnification}");
                ImGui.Text($"TileSize: {map?.TileSize}");
                ImGui.TreePop();
            }

            int hitId = 0;
            bool anyHovered = false;
            if (ImGui.TreeNode("Global"))
            {
                var reflected = Reflector.Reflect(null, Exchange, null, 0);
                if (reflected.SubObjects != null)
                    foreach (var child in reflected.SubObjects)
                        anyHovered |= RenderNode(child);
                ImGui.TreePop();
            }

            foreach (var hit in _hits)
            {
                if (ImGui.TreeNode($"{hitId} {hit.Target}"))
                {
                    var reflected = Reflector.Reflect(null, hit.Target, null, 0);
                    if (reflected.SubObjects != null)
                        foreach (var child in reflected.SubObjects)
                            anyHovered |= RenderNode(child);
                    ImGui.TreePop();
                }

                hitId++;
            }

            ImGui.EndChild();
            ImGui.End();

            if (!anyHovered && _lastHoveredItem?.Object != null &&
                _behaviours.TryGetValue(_lastHoveredItem.Object.GetType(), out var blurredCallback))
                blurredCallback(DebugInspectorAction.Blur, _lastHoveredItem);

            /*

            Window: Begin & End
            Menus: BeginMenuBar, MenuItem, EndMenuBar
            Colours: ColorEdit4
            Graph: PlotLines
            Text: Text, TextColored
            ScrollBox: BeginChild, EndChild

            */
        }

        bool CheckHover(Reflector.ReflectedObject reflected)
        {
            if (!ImGui.IsItemHovered()) 
                return false;

            if (_lastHoveredItem != reflected)
            {
                if (_lastHoveredItem?.Object != null &&
                    _behaviours.TryGetValue(_lastHoveredItem.Object.GetType(), out var blurredCallback))
                    blurredCallback(DebugInspectorAction.Blur, _lastHoveredItem);

                if (reflected.Object != null &&
                    _behaviours.TryGetValue(reflected.Object.GetType(), out var hoverCallback))
                    hoverCallback(DebugInspectorAction.Hover, reflected);

                _lastHoveredItem = reflected;
            }

            return true;
        }

        bool RenderNode(Reflector.ReflectedObject reflected)
        {
            var typeName = reflected.Object?.GetType().Name ?? "null";
            var description =
                reflected.Name == null
                    ? $"{reflected.Value} ({typeName})"
                    : $"{reflected.Name}: {reflected.Value} ({typeName})";

            bool anyHovered = false;
            if (reflected.SubObjects != null)
            {
                if (ImGui.TreeNode(description))
                {
                    anyHovered |= CheckHover(reflected);

                    foreach (var child in reflected.SubObjects)
                        anyHovered |= RenderNode(child);
                    ImGui.TreePop();
                }
                else anyHovered |= CheckHover(reflected);
            }
            else
            {
                ImGui.TextWrapped(description);
                anyHovered |= CheckHover(reflected);
            }

            return anyHovered;
        }

        public DebugMapInspector() : base(Handlers) { }

        public DebugMapInspector AddBehaviour(IDebugBehaviour behaviour)
        {
            _behaviours[behaviour.HandledType] = behaviour.Handle;
            return this;
        }
    }
}
