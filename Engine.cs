using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dots
{
    public sealed class Engine
    {
        public struct Position
        {
            public int X;
            public int Y;
            public Position(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public override bool Equals(object obj)
            {
                return obj is Position && ((Position)obj).X == this.X && ((Position)obj).Y == this.Y;
            }
            public override int GetHashCode()
            {
                var hash = 31;
                unchecked
                {
                    hash *= this.X * 27;
                    hash *= this.Y * 27;
                }
                return hash;
            }
            public override string ToString()
            {
                return "(" + this.X.ToString() + ", " + this.Y.ToString() + ")";
            }
            public IEnumerable<Position> Adjacent
            {
                get
                {
                    yield return new Position(this.X + 1, this.Y + 1);
                    yield return new Position(this.X + 1, this.Y - 1);
                    yield return new Position(this.X - 1, this.Y + 1);
                    yield return new Position(this.X - 1, this.Y - 1);
                }
            }
            public IEnumerable<Position> Nearby
            {
                get
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if (x != 0 || y != 0) yield return new Position(this.X + x, this.Y + y);
                        }
                    }
                }
            }
            public IEnumerable<Position> Inrange(int distance)
            {
                for (int x = -distance; x <= distance; x++)
                {
                    for (int y = -distance; y <= distance; y++)
                    {
                        var scanPosition = new Position(this.X + x, this.Y + y);
                        if (this.Distance(scanPosition) <= distance) yield return scanPosition;
                    }
                }
            }
            public double Distance(Position position)
            {
                return Math.Sqrt(Math.Pow(((double)position.X - (double)this.X), 2) + Math.Pow(((double)position.Y - (double)this.Y), 2));
            }
        }

        public enum DotTypes { City, Unit }

        public sealed class Dot
        {
            public Engine Engine { get; set; }
            public Tile Tile { get; set; }
            public Empire Empire { get; set; }
            public DotTypes Type { get; set; }

            public int Tier { get; set; }
            public int TierProgress { get; set; }
            public int TierProgressRequired { get { return this.Tier * Engine.TierProgressCost; } }
            public int Hits { get; set; }
            public int MaxHits { get { return this.Tier * Engine.BaseHitsPerTier; } }
            public int StoredResources { get; set; }
            public Dot UnitStorage { get; set; }

            public int ControlRange
            {
                get
                {
                    if (this.Type == DotTypes.City) return Engine.BaseCityControlRange;
                    else return Engine.BaseUnitControlRange;
                }
            }

            public Dot(Engine engine, Empire empire, DotTypes type)
            {
                this.Engine = engine;
                this.Empire = empire;
                this.Type = type;
                this.Tier = 1;
                this.Hits = this.MaxHits;
            }
        }

        public sealed class Empire
        {
            public Engine Engine { get; set; }
            public int Id { get; set; }
            public HashSet<Dot> Dots { get; set; }
            public HashSet<Tile> Tiles { get; set; }

            public int LastResourceIncome { get; set; }
            public int CurrentResourceIncome { get; set; }
            public int CurrentResourceStorage { get; set; }
   
            public Empire(Engine engine, int id)
            {
                this.Engine = engine;
                this.Id = id;
                this.Dots = new HashSet<Dot>();
                this.Tiles = new HashSet<Tile>();
            }
        }

        public sealed class Tile
        {
            public Engine Engine { get; set; }
            public Position Position { get; set; }
            public Empire Owner { get; set; }
            public HashSet<Dot> Claimants { get; set; }
            public Dot Occupant { get; set; }

            public Tile(Engine engine, Position position)
            {
                this.Engine = engine;
                this.Position = position;
                this.Claimants = new HashSet<Dot>();
            }
        }

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

        private object ThreadKey = new object();
        private bool StopRequested;
        private bool isEngineRunning;
        private HashSet<Dot> Dots;
        private HashSet<Empire> Empires;
        private Dictionary<Position, Tile> Tiles;
        private int MapWidth;
        private int MapHeight;
        private Random Rng;
        private Dictionary<Tile, TileRenderer> Renderer;

        public const int UnitResourceCost = 10;
        public const int CityResourceCost = 10;
        public const int TierProgressCost = 10;
        public const int BaseHitsPerTier = 10;
        public const int BaseCityControlRange = 2;
        public const int BaseUnitControlRange = 1;

        public bool IsEngineRunning
        {
            get
            {
                var result = false;
                lock (this.ThreadKey)
                {
                    result = this.isEngineRunning;
                }
                return result;
            }
        }

        public Engine(int startingCount, int mapWidth, int mapHeight)
        {
            lock (this.ThreadKey)
            {
                this.Initialize(mapWidth, mapHeight);
                this.SpawnInitialEmpires(startingCount);
            }
        }

        private void SpawnInitialEmpires(int startingCount)
        {
            var spawnPoints = new HashSet<Position>();
            for (int x = 0; x < this.MapWidth; x++)
            {
                for (int y = 0; y < this.MapWidth; y++)
                {
                    var position = new Position(x,y);
                    spawnPoints.Add(position);
                    this.Tiles.Add(position, new Tile(this, position));
                }
            }

            for (int i = 0; i < startingCount; i++)
            {
                if (spawnPoints.Count == 0) break;
                var spawnPoint = spawnPoints.ToList()[this.Rng.Next(spawnPoints.Count)];
                foreach (var point in spawnPoint.Inrange(5))
                {
                    spawnPoints.Remove(point);
                }

                var empire = new Empire(this, i + 1);
                this.AddEmpire(empire);
                this.AddDot(empire, this.GetTileAt(spawnPoint), DotTypes.City);
            }
        }
        private void AddDot(Empire empire, Tile tile, DotTypes type)
        {
            var dot = new Dot(this, empire, type);            
            this.Dots.Add(dot);
            empire.Dots.Add(dot);
            this.MoveDotTo(dot, tile);
        }
        private void Initialize(int mapWidth, int mapHeight)
        {
            this.Dots = new HashSet<Dot>();
            this.Empires = new HashSet<Empire>();
            this.Tiles = new Dictionary<Position, Tile>();
            this.MapHeight = mapHeight;
            this.MapWidth = mapWidth;
            this.Rng = new Random();
            this.Renderer = new Dictionary<Tile, TileRenderer>();
        }
        private Tile GetTileAt(Position position)
        {
            var x = position.X;
            var y = position.Y;
            while (x < 0) { x += this.MapWidth; }
            while (y < 0) { y += this.MapHeight; }
            while (x >= this.MapWidth) { x -= this.MapWidth; }
            while (y >= this.MapHeight) { y -= this.MapHeight; }
            return this.Tiles[new Position(x, y)];
        }
        private void AddEmpire(Empire empire)
        {
            this.Empires.Add(empire);
        }
        private void MoveDotTo(Dot dot, Tile tile)
        {
            this.RemoveDotFromCurrentTile(dot);
            this.AddDotToTile(dot, tile);
        }
        private void AddDotToTile(Dot dot, Tile tile)
        {
            dot.Tile = tile;
            tile.Occupant = dot;
            this.InitializeTileRenderer(tile);
            this.Renderer[tile].Dot = dot.Empire.Id;
            foreach (var position in tile.Position.Inrange(dot.ControlRange))
            {
                var nearbyTile = this.GetTileAt(position);
                this.ClaimTile(nearbyTile, dot);
            }
        }
        private void RemoveDotFromCurrentTile(Dot dot)
        {
            if (dot.Tile != null)
            {
                foreach (var position in dot.Tile.Position.Inrange(dot.ControlRange))
                {
                    var tile = this.GetTileAt(position);
                    this.RemoveTileClaim(tile, dot);
                }
                dot.Tile.Occupant = null;
                this.InitializeTileRenderer(dot.Tile);
                this.Renderer[dot.Tile].Dot = 0;
            }
        }

        private void InitializeTileRenderer(Tile tile)
        {
            if (!this.Renderer.ContainsKey(tile))
            {
                this.Renderer.Add(tile, new TileRenderer(tile.Position));
                this.Renderer[tile].Owner = (tile.Owner != null ? tile.Owner.Id : 0);
                this.Renderer[tile].Dot = (tile.Occupant != null ? tile.Occupant.Empire.Id : 0);
            }            
        }
        private void RemoveTileClaim(Tile tile, Dot dot)
        {
            tile.Claimants.Remove(dot);
            this.ResolveTileClaim(tile);
        }
        private void ResolveTileClaim(Tile tile)
        {
            var remainingClaimingEmpires = tile.Claimants.Select(o => o.Empire).Distinct();
            if (remainingClaimingEmpires.Count() > 0)
            {
                if (!remainingClaimingEmpires.Contains(tile.Owner))
                {
                    if (tile.Owner != null) tile.Owner.Tiles.Remove(tile);
                    tile.Owner = null;
                    this.InitializeTileRenderer(tile);
                    this.Renderer[tile].Owner = 0;
                }
                if (tile.Owner == null && remainingClaimingEmpires.Count() == 1)
                {
                    tile.Owner = remainingClaimingEmpires.First();
                    tile.Owner.Tiles.Add(tile);
                    this.InitializeTileRenderer(tile);
                    this.Renderer[tile].Owner = tile.Owner.Id;
                }
            }
        }
        private void ClaimTile(Tile tile, Dot dot)
        {
            tile.Claimants.Add(dot);
            this.ResolveTileClaim(tile);
        }

        private void Spin(object threadArguments)
        {
            var continueSpinning = false;
            lock (this.ThreadKey) { continueSpinning = !this.StopRequested; }

            while (continueSpinning)
            {
                lock (this.ThreadKey)
                {
                    continueSpinning = !this.StopRequested;

                    foreach (var empire in this.Empires)
                    {
                        this.TakeTurn(empire);
                    }
                }

                Thread.Sleep(1);
            }
        }

        private void TakeTurn(Empire empire)
        {
            this.GenerateResources(empire);
            this.ManageProduction(empire);
            this.MoveUnits(empire);
            this.EmptyStorage(empire);
        }

        private void EmptyStorage(Empire empire)
        {

        }
        private void MoveUnits(Empire empire)
        {
            var units = empire.Dots.Where(o => o.Type == DotTypes.Unit);
            foreach (var unit in units)
            {
                var nearbyTiles = unit.Tile.Position.Nearby.Select(o => this.GetTileAt(o));
                var emptyTiles = nearbyTiles.Where(o => o.Occupant == null);

            }
        }
        private void ManageProduction(Empire empire)
        {
            var citiesWithoutUnitInStorage = empire.Dots.Where(o => o.Type == DotTypes.City && o.UnitStorage == null).ToList();
            while (empire.CurrentResourceStorage >= Engine.UnitResourceCost && citiesWithoutUnitInStorage.Count > 0)
            {
                var city = citiesWithoutUnitInStorage[this.Rng.Next(citiesWithoutUnitInStorage.Count)];
                citiesWithoutUnitInStorage.Remove(city);
                empire.CurrentResourceStorage -= Engine.UnitResourceCost;
                city.UnitStorage = new Dot(this, empire, DotTypes.Unit);
            }            
        }
        private void GenerateResources(Empire empire)
        {
            empire.LastResourceIncome = empire.CurrentResourceIncome;
            empire.CurrentResourceIncome = empire.Tiles.Count;
            empire.CurrentResourceStorage += empire.CurrentResourceIncome;
        }

        public void Stop()
        {
            lock (this.ThreadKey)
            {
                this.StopRequested = true;
            }
        }
        public void Start()
        {
            if (!this.IsEngineRunning)
            {
                lock (this.ThreadKey)
                {
                    this.StopRequested = false;
                    this.isEngineRunning = true;
                    ThreadPool.QueueUserWorkItem(this.Spin);
                }
            }
        }
        public List<TileRenderer> GetRenderer()
        {
            List<TileRenderer> result;

            lock (this.ThreadKey)
            {
                result = this.Renderer.Values.ToList();
                this.Renderer.Clear();
            }

            return result;
        }
    }
}
