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
        }

        public sealed class Dot
        {
            public Position Position { get; set; }
            public int Owner { get; set; }
        }

        private object ThreadLock = new object();
        private Dictionary<Position, int> OwnerByPosition;
        private Dictionary<Position, int> ChangesSinceLastRender;
        private List<Dot> Dots;
        private int MapWidth;
        private int MapHeight;
        private Random Rng;
        private bool StopRequested;
        private bool isEngineRunning;

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

        public Engine(int startingDotCount, int mapWidth, int mapHeight)
        {
            lock (this.ThreadLock)
            {
                this.OwnerByPosition = new Dictionary<Position, int>();
                this.ChangesSinceLastRender = new Dictionary<Position, int>();
                this.Dots = new List<Dot>();
                this.MapHeight = mapHeight;
                this.MapWidth = mapWidth;
                this.Rng = new Random();

                for (int i = 0; i < startingDotCount; i++)
                {
                    var dot = new Dot();
                    dot.Owner = i + 1;
                    var position = new Position(this.Rng.Next(this.MapWidth + 1), this.Rng.Next(this.MapHeight + 1));
                    while (this.OwnerByPosition.ContainsKey(position))
                    {
                        position = new Position(this.Rng.Next(this.MapWidth + 1), this.Rng.Next(this.MapHeight + 1));
                    }
                    dot.Position = position;
                    this.Dots.Add(dot);
                    this.MoveDotTo(dot, position);
                }
            }
        }

        private void MoveDotTo(Dot dot, Position position)
        {
            if (!this.OwnerByPosition.ContainsKey(position)) this.OwnerByPosition.Add(position, 0);
            this.OwnerByPosition[position] = dot.Owner;
            if (!this.ChangesSinceLastRender.ContainsKey(position)) this.ChangesSinceLastRender.Add(position, 0);
            this.ChangesSinceLastRender[position] = dot.Owner;
            dot.Position = position;
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
        public void Spin(object threadArguments)
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
    }
}
