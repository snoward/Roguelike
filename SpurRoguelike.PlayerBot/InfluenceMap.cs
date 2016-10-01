using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class InfluenceMap
    {
        public readonly int[,] map;
        private readonly LevelView level;
        private const int safeDistance = 3;
        private const int monsterInfluence = 100;
        private const int wallInfluence = 50;

        public InfluenceMap(LevelView levelView)
        {
            level = levelView;
            map = new int[levelView.Field.Width, levelView.Field.Height];
        }

        public int this[Location location]
        {
            get
            {
                if (map[location.X, location.Y] == 0)
                    CalculateInfluence(location);
                return map[location.X, location.Y];
            }
        }

        private bool IsOnMap(Location location)
        {
            return (location.X > 0) && (location.X < level.Field.Width) &&
                    (location.Y > 0) && (location.Y < level.Field.Height);
        }

        private void CalculateInfluence(Location location)
        {
            foreach (var monster in level.Monsters)
            {
                var distance = (monster.Location - location).Size();
                if (distance > safeDistance)
                    continue;
                map[location.X, location.Y] += (int)(monsterInfluence / Math.Pow(2, distance - safeDistance));
            }
            foreach (var offset in Offset.AttackOffsets)
            {
                var position = location + offset;
                if (level.Field[position] == CellType.Wall && IsOnMap(position))
                    map[location.X, location.Y] += wallInfluence;
            }
            map[location.X, location.Y] = map[location.X, location.Y] == 0 ? 1 : map[location.X, location.Y];
            //HtmlGenerator.WriteHtml(level, map, @"C:\Users\Mikhail\Downloads\HtmlGenerator\result\index.html");

        }
    }
}
