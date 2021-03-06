﻿using System;

namespace UAlbion.Game.Debugging
{
    public interface IDebugBehaviour
    {
        Type HandledType { get; }
        void Handle(DebugInspectorAction action, Reflector.ReflectedObject reflected);
    }
}