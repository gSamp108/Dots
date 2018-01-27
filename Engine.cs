using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dots
{
    public sealed class Engine
    {
        private object ThreadKey = new object();
        private bool StopRequested;
        private bool isEngineRunning;
        private HashSet<Dot> Dots;
        private int GroupIndex;
        private Dictionary<int, Group> Groups;
        private Dictionary<Position, Tile> Tiles;
        private int MapWidth;
        private int MapHeight;
        private Random Rng;
        private Dictionary<Tile, TileRenderer> Renderer;
        private List<TileActivity> Activities;
        private int CurrentTick;
        private int CurrentTickGroupId;

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
                this.SpawnInitialGroups(startingCount);
            }
        }
        public void Tick()
        {
            lock (this.ThreadKey)
            {
                if (this.Groups.Count > 0)
                {
                    var selectedGroup = default(Group);
                    while (selectedGroup == null)
                    {
                        if (this.Groups.ContainsKey(this.CurrentTickGroupId)) selectedGroup = this.Groups[this.CurrentTickGroupId];
                        else if (this.CurrentTickGroupId >= this.GroupIndex) this.AdvanceTickCount();
                        else this.CurrentTickGroupId += 1;
                    }
                    selectedGroup.Tick();
                }
            }
        }

        private void AdvanceTickCount()
        {
            this.CurrentTick += 1;
            this.CurrentTickGroupId = 0;
            this.GenerateResources();
            this.RegenerateDots();
        }

        private void RegenerateDots()
        {
            foreach (var dot in this.Dots)
            {
                dot.RestoreMovement();
            }
        }
        private void GenerateResources()
        {
            foreach (var group in this.Groups.Values)
            {
                group.LastResourceIncome = group.CurrentResourceIncome;
                group.CurrentResourceIncome = group.Tiles.Count;
                group.CurrentResourceStorage += group.CurrentResourceIncome;
            }
        }

        private void Initialize(int mapWidth, int mapHeight)
        {
            this.Dots = new HashSet<Dot>();
            this.GroupIndex = 0;
            this.Groups = new Dictionary<int, Group>();
            this.Tiles = new Dictionary<Position, Tile>();
            this.MapHeight = mapHeight;
            this.MapWidth = mapWidth;
            this.Rng = new Random();
            this.Renderer = new Dictionary<Tile, TileRenderer>();
            this.Activities = new List<TileActivity>();
        }
        private void SpawnInitialGroups(int startingCount)
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

                this.SpawnNewGroup(spawnPoint);
            }
        }

        private void SpawnNewGroup(Position position)
        {
            var group = new Group(this, this.GroupIndex);
            this.Groups.Add(group.Id, group);
            this.GroupIndex += 1;
            this.AddDot(group, this.GetTileAt(position), DotTypes.City);
        }
        private void AddDot(Group empire, Tile tile, DotTypes type)
        {
            var dot = new Dot(this, empire, type);
            this.AddDot(dot);
            this.MoveDotTo(dot, tile);
        }
        private void AddDot(Dot dot)
        {
            this.Dots.Add(dot);
            dot.Group.Dots.Add(dot);
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
        private void MoveDotTo(Dot dot, Tile tile)
        {
            this.LogMoveActivity(dot.Tile, tile);
            this.RemoveDotFromCurrentTile(dot);
            this.AddDotToTile(dot, tile);
        }
        private void LogMoveActivity(Tile from, Tile to)
        {
            var activity = new TileActivity();
            activity.From = from;
            activity.To = to;
            activity.Type = TileActivity.TileActivityTypes.Move;
            this.Activities.Add(activity);
        }
        private void AddDotToTile(Dot dot, Tile tile)
        {
            dot.Tile = tile;
            tile.Occupant = dot;
            this.InitializeTileRenderer(tile);
            this.Renderer[tile].Dot = dot.Group.Id;
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
                this.Renderer[tile].Dot = (tile.Occupant != null ? tile.Occupant.Group.Id : 0);
            }            
        }
        private void RemoveTileClaim(Tile tile, Dot dot)
        {
            tile.Claimants.Remove(dot);
            this.ResolveTileClaim(tile);
        }
        private void ResolveTileClaim(Tile tile)
        {
            var remainingClaimingEmpires = tile.Claimants.Select(o => o.Group).Distinct();
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
        private void NegitiveUnitActionOnTile(Dot unit, Tile tile)
        {
            if (tile.Occupant != null)
            {
                var activity = new TileActivity();
                activity.From = unit.Tile;
                activity.To = tile.Occupant.Tile;
                activity.Type = TileActivity.TileActivityTypes.Strike;
                this.Activities.Add(activity);

                var offense = unit;
                var defense = tile.Occupant;
                var strikePool = offense.Strike + defense.Dodge;
                var strikeRoll = this.Rng.Next(strikePool) + 1;
                if (strikeRoll > defense.Dodge)
                {
                    var damageRoll = this.Rng.Next(offense.Strength + 1) + this.Rng.Next(offense.Strength + 1);
                    defense.Hits -= damageRoll;
                    if (defense.Hits < 1)
                    {
                        offense.TierProgress += defense.Tier + this.Rng.Next(defense.Tier + 1);
                        this.RemoveDot(defense);
                    }
                }
            }
        }
        private void RemoveDot(Dot dot)
        {
            this.RemoveDotFromCurrentTile(dot);
            dot.Group.Dots.Remove(dot);
            this.Dots.Remove(dot);
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

                    this.Tick();
                }

                Thread.Sleep(1);
            }
        }
        private void TakeTurn(Group empire)
        {
            this.ManageProduction(empire);
            this.MoveUnits(empire);
            this.EmptyStorage(empire);
        }

        private void EmptyStorage(Group empire)
        {
            foreach (var city in empire.Dots.Where(o => o.Type == DotTypes.City && o.UnitStorage != null).ToList())
            {
                var emptyNearbyTiles = city.Tile.Position.Nearby.Select(o => this.GetTileAt(o)).Where(o => o.Occupant == null).ToList();
                if (emptyNearbyTiles.Count > 0)
                {
                    var selectedTile = emptyNearbyTiles[this.Rng.Next(emptyNearbyTiles.Count)];
                    var dot = city.UnitStorage;
                    city.UnitStorage = null;
                    this.AddDot(dot);
                    this.MoveDotTo(dot, selectedTile);
                }
            }
        }
        private void MoveUnits(Group empire)
        {
            var units = empire.Dots.Where(o => o.Type == DotTypes.Unit);
            foreach (var unit in units)
            {
                var nearbyTiles = unit.Tile.Position.Nearby.Select(o => this.GetTileAt(o));
                var uncontrolledTiles = nearbyTiles.Where(o => o.Claimants.Where(p => p.Group != empire).Count() == 0);
                var emptyUncontrolledTiles = uncontrolledTiles.Where(o => o.Occupant == null);
                var enemyControlledTiles = nearbyTiles.Where(o => o.Claimants.Where(p => p.Group != empire).Count() > 0);
                var inEnemyControlledTile = enemyControlledTiles.Contains(unit.Tile);

                var nearbyEnemyUnits = nearbyTiles.Where(o => o.Occupant != null && o.Occupant.Group != empire).ToList();
                var reachableEnemyUnits = unit.Tile.Position.Inrange(unit.Range).Select(o => this.GetTileAt(o)).Where(o => o.Occupant != null && o.Occupant.Group != unit.Group).ToList();
                var moveableTiles = new List<Tile>();
                moveableTiles.AddRange(emptyUncontrolledTiles);
                if (!inEnemyControlledTile) moveableTiles.AddRange(enemyControlledTiles);

                if (nearbyEnemyUnits.Count() > 0)
                {
                    var actionTile = nearbyEnemyUnits[this.Rng.Next(nearbyEnemyUnits.Count)];
                    this.NegitiveUnitActionOnTile(unit, actionTile);
                }
                else if (reachableEnemyUnits.Count() > 0)
                {
                    var actionTile = reachableEnemyUnits[this.Rng.Next(reachableEnemyUnits.Count)];
                    this.NegitiveUnitActionOnTile(unit, actionTile);
                }
                else if (moveableTiles.Count > 0)
                {
                    var actionTile = moveableTiles[this.Rng.Next(moveableTiles.Count)];
                    this.MoveDotTo(unit, actionTile);
                }
            }
        }
        private void ManageProduction(Group empire)
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
        public int Random(int input)
        {
            var result = 0;
            lock (this.ThreadKey)
            {
                result = this.Rng.Next(input);
            }
            return result;
        }
    }
}
