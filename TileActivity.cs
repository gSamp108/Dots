using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
{
    public sealed class TileActivity
    {
        public enum TileActivityTypes { None, Move, Strike, TierUp }
        public TileActivityTypes Type { get; set; }
        public Tile From { get; set; }
        public Tile To { get; set; }
    }
}
