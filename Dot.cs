using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
{
    public sealed class Dot
    {
        public Engine Engine { get; set; }
        public Tile Tile { get; set; }
        public Group Group { get; set; }
        public DotTypes Type { get; set; }

        public int Tier { get; set; }
        private int tierProgress;
        public int TierProgress
        {
            get { return this.tierProgress; }
            set
            {
                this.tierProgress = value;
                if (this.tierProgress >= this.TierProgressRequired)
                {
                    this.tierProgress -= this.TierProgressRequired;
                    this.Tier += 1;
                    this.TierIncreased();
                }
            }
        }

        private void TierIncreased()
        {
            var statGains = 1 + this.Engine.Random(4);
            for (int i = 0; i < statGains; i++)
            {
                var selectedStat = this.Engine.Random(3);
                if (selectedStat == 0) this.Strength += 1;
                if (selectedStat == 1) this.Strike += 1;
                if (selectedStat == 2) this.Dodge += 1;
            }
            this.Hits = this.MaxHits;
        }
        public int TierProgressRequired { get { return this.Tier * Engine.TierProgressCost; } }
        public int Hits { get; set; }
        public int MaxHits { get { return this.Tier * Engine.BaseHitsPerTier; } }
        public int StoredResources { get; set; }
        public Dot UnitStorage { get; set; }
        public int Strength { get; set; }
        public int Strike { get; set; }
        public int Dodge { get; set; }
        public int Range { get; set; }
        public int Movement { get; set; }

        public int ControlRange
        {
            get
            {
                if (this.Type == DotTypes.City) return Engine.BaseCityControlRange;
                else return Engine.BaseUnitControlRange;
            }
        }

        public Dot(Engine engine, Group empire, DotTypes type)
        {
            this.Engine = engine;
            this.Group = empire;
            this.Type = type;
            this.Tier = 1;
            this.Hits = this.MaxHits;
            this.Strength = 1;
            this.Strike = 1;
            this.Dodge = 1;
            this.Range = 2;
        }

        internal void RestoreMovement()
        {
            throw new NotImplementedException();
        }
    }
}
