using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dots
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
}
