using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
{
    public sealed class TileRenderer
    {
        public Position Position { get; set; }
        public int Owner { get; set; }
        public int Dot { get; set; }

        public TileRenderer(Position position)
        {
            this.Position = position;
        }
    }
}
