﻿using System;
using System.Numerics;
using Veldrid;

namespace UAlbion.Game.Gui
{
    public class FixedSize : UiElement, IFixedSizeUiElement
    {
        readonly int _width;
        readonly int _height;

        public FixedSize(int width, int height, IUiElement child) : base(null)
        {
            _width = width;
            _height = height;
            Children.Add(child);
        }

        protected override int DoLayout(Rectangle extents, int order, Func<IUiElement, Rectangle, int, int> func)
        {
            var fixedExtents = new Rectangle(extents.X, extents.Y, _width, _height);
            return base.DoLayout(fixedExtents, order, func);
        }

        public override Vector2 GetSize() => new Vector2(_width, _height);
    }
}