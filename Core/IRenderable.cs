﻿using System;
using UAlbion.Api;

namespace UAlbion.Core
{
    public interface IRenderable
    {
        string Name { get; }
        DrawLayer RenderOrder { get; }
        int PipelineId { get; }
        Type Renderer { get; }
    }
}
