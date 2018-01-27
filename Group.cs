using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
{
    public sealed class Group
    {
        public Engine Engine { get; set; }
        public int Id { get; set; }
        public HashSet<Dot> Dots { get; set; }
        public HashSet<Tile> Tiles { get; set; }

        public int LastResourceIncome { get; set; }
        public int CurrentResourceIncome { get; set; }
        public int CurrentResourceStorage { get; set; }

        public Group(Engine engine, int id)
        {
            this.Engine = engine;
            this.Id = id;
            this.Dots = new HashSet<Dot>();
            this.Tiles = new HashSet<Tile>();
        }

        internal void Tick()
        {
            throw new NotImplementedException();
        }
    }
}
