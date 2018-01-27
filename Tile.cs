using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
{
    public sealed class Tile
    {
        public Engine Engine { get; set; }
        public Position Position { get; set; }
        public Group Owner { get; set; }
        public HashSet<Dot> Claimants { get; set; }
        public Dot Occupant { get; set; }

        public Tile(Engine engine, Position position)
        {
            this.Engine = engine;
            this.Position = position;
            this.Claimants = new HashSet<Dot>();
        }
    }
}
