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
            public Position Position { get; set; }
            public Empire Empire { get; set; }
            public DotTypes Type { get; set; }

            public int Tier { get; set; }
            public int Hits { get; set; }
            public int MaxHits { get { return this.Tier * 10; } }
            public int ControlRange
            {
                get
                {
                    if (this.Type == DotTypes.City) return 2;
                    else return 1;
                }
            }

            public Dot(Engine engine, Empire empire, Position position, DotTypes type)
            {
                this.Engine = engine;
                this.Empire = empire;
                this.Position = position;
                this.Type = type;
                this.Tier = 1;
                this.Hits = this.MaxHits;
            }
        }

        public sealed class Empire
        {
            public Engine Engine { get; set; }
            public int Id { get; set; }
            public int Resources { get; set; }
            public HashSet<Dot> Dots { get; set; }
            public HashSet<Position> Tiles { get; set; }

            public Empire(Engine engine, int id)
            {
                this.Engine = engine;
                this.Id = id;
                this.Resources = 0;
                this.Dots = new HashSet<Dot>();
                this.Tiles = new HashSet<Position>();
            }

        }

        private object ThreadLock = new object();
        private Dictionary<Position, int> ChangesSinceLastRender;
        private HashSet<Dot> Dots;
        private HashSet<Empire> Empires;
        private int MapWidth;
        private int MapHeight;
        private Random Rng;
        private bool StopRequested;
        private bool isEngineRunning;
        private Dictionary<Position, Empire> ClaimByPosition;
        private Dictionary<Position, HashSet<Dot>> ClaimantsByPosition;

        public bool IsEngineRunning
        {
            get
            {
                var result = false;
                lock (this.ThreadLock)
                {
                    result = this.isEngineRunning;
                }
                return result;
            }
        }

        public Engine(int startingCount, int mapWidth, int mapHeight)
        {
            lock (this.ThreadLock)
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
                    spawnPoints.Add(new Position(x, y));
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
                var dot = new Dot(this, empire, spawnPoint, DotTypes.City);
                this.AddDot(dot);
            }
        }
        private void Initialize(int mapWidth, int mapHeight)
        {
            this.ChangesSinceLastRender = new Dictionary<Position, int>();
            this.Dots = new HashSet<Dot>();
            this.Empires = new HashSet<Empire>();
            this.MapHeight = mapHeight;
            this.MapWidth = mapWidth;
            this.Rng = new Random();
            this.ClaimantsByPosition = new Dictionary<Position, HashSet<Dot>>();
            this.ClaimByPosition = new Dictionary<Position, Empire>();
        }
        private void AddDot(Dot dot)
        {
            this.Dots.Add(dot);
            dot.Empire.Dots.Add(dot);
            this.MoveDotTo(dot, dot.Position);
        }
        private void AddEmpire(Empire empire)
        {
            this.Empires.Add(empire);
        }
        private void MoveDotTo(Dot dot, Position position)
        {
            this.RemoveDotFrom(dot, dot.Position);

            dot.Position = position;
            foreach (var tile in position.Inrange(dot.ControlRange))
            {
                this.ClaimTile(tile, dot);
            }

            if (!this.OwnerByPosition.ContainsKey(position)) this.OwnerByPosition.Add(position, 0);
            if (this.OwnerByPosition[position] != dot.Empire)
            {
                this.OwnerByPosition[position] = dot.Empire;

                dot.Resources += 1;
                if (dot.Resources >= (dot.Tier * 10))
                {
                    dot.Resources -= (dot.Tier * 10);
                    dot.Tier += 1;
                    dot.Hits += (dot.Tier * 10);
                    dot.Disappointment -= (dot.Tier * 10);
                    if (dot.Disappointment < 0) dot.Disappointment = 0;
                }


            }
            if (!this.ChangesSinceLastRender.ContainsKey(position)) this.ChangesSinceLastRender.Add(position, 0);
            this.ChangesSinceLastRender[position] = dot.Empire;
        }

        private void RemoveDotFrom(Dot dot, Position position)
        {
            foreach (var tile in dot.Position.Inrange(dot.ControlRange))
            {
                this.RemoveTileClaim(tile, dot);
            }
        }

        private void RemoveTileClaim(Position tile, Dot dot)
        {
            if (!this.ClaimantsByPosition.ContainsKey(tile)) this.ClaimantsByPosition.Add(tile, new HashSet<Dot>());
            this.ClaimantsByPosition[tile].Remove(dot);
            if (!this.ClaimByPosition.ContainsKey(tile)) this.ClaimByPosition.Add(tile, null);
            var claimants = this.ClaimantsByPosition[tile].Select(o => o.Empire).Distinct();
            if (this.ClaimByPosition[tile] != null)
            {

            }
            else
            {
            }
        }

        private void ClaimTile(Position tile, Dot dot)
        {
            throw new NotImplementedException();
        }
        private bool InBounds(Position position)
        {
            var result = true;
            lock (this.ThreadLock)
            {
                if (position.X < 0) result = false;
                if (position.X > this.MapWidth) result = false;
                if (position.Y < 0) result = false;
                if (position.Y > this.MapHeight) result = false;
            }
            return result;
        }
        private void Spin(object threadArguments)
        {
            var continueSpinning = false;
            lock (this.ThreadLock) { continueSpinning = !this.StopRequested; }

            while (continueSpinning)
            {
                lock (this.ThreadLock)
                {
                    foreach (var dot in this.Dots)
                    {
                        var usable = new List<Position>();
                        foreach (var adjacent in dot.Position.Adjacent)
                        {
                            if (this.InBounds(adjacent)) usable.Add(adjacent);
                        }
                        if (usable.Count > 0) this.MoveDotTo(dot, usable[this.Rng.Next(usable.Count)]);
                    }
                    continueSpinning = !this.StopRequested;
                }

                Thread.Sleep(0);
            }
        }

        public List<KeyValuePair<Position, int>> GetRenderChanges()
        {
            List<KeyValuePair<Position, int>> result;

            lock (this.ThreadLock)
            {
                result = this.ChangesSinceLastRender.ToList();
                this.ChangesSinceLastRender.Clear();
            }

            return result;
        }
        public void Stop()
        {
            lock (this.ThreadLock)
            {
                this.StopRequested = true;
            }
        }
        public void Start()
        {
            if (!this.IsEngineRunning)
            {
                lock (this.ThreadLock)
                {
                    this.StopRequested = false;
                    this.isEngineRunning = true;
                    ThreadPool.QueueUserWorkItem(this.Spin);
                }
            }
        }
    }
}
