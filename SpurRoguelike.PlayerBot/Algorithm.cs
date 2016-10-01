using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class Algorithm
    {
        public static IEnumerable<Location> BFS(LevelView levelView, Func<Location, bool> isTarget,
            bool acceptPickup = false)
        {
            var queue = new Queue<Location>();
            var start = levelView.Player.Location;
            queue.Enqueue(start);

            var visited = new HashSet<Location> { start };
            var pred = new Dictionary<Location, Location>();

            while (queue.Any())
            {
                var current = queue.Dequeue();
                if (isTarget(current))
                    return RevertPath(levelView, current, pred);

                foreach (var offset in Offset.StepOffsets)
                {
                    var next = current + offset;

                    if (!visited.Contains(next) && (levelView.Field[next] > CellType.Trap))
                    {
                        if (levelView.GetHealthPackAt(next).HasValue || levelView.GetItemAt(next).HasValue)
                            if (!acceptPickup)
                                continue;
                        queue.Enqueue(next);
                        visited.Add(next);
                        pred[next] = current;
                    }
                }
            }
            var path = RevertPath(levelView, levelView.Player.Location, pred);
            if (!path.Any())
                path = BFS(levelView, isTarget, true);

            return path;
        }

        public static IEnumerable<Location> Dijkstra(LevelView levelView, Func<Location, bool> isTarget)
        {
            var costs = new InfluenceMap(levelView);
            var current = levelView.Player.Location;
            var dist = new Dictionary<Location, int> { [current] = 0 };
            var prev = new Dictionary<Location, Location>();
            var notOpened = new HashSet<Location> { current };

            for (var y = 0; y < levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                {
                    var location = new Location(x, y);
                    var cellType = levelView.Field[location];
                    if ((cellType == CellType.Empty || cellType == CellType.PlayerStart) && (location != current) &&
                        !levelView.GetItemAt(location).HasValue)
                    {
                        notOpened.Add(location);
                        dist[location] = int.MaxValue;
                    }
                }
            while (notOpened.Any())
            {
                var toOpen = default(Location);
                var bestPrice = int.MaxValue;
                foreach (var node in notOpened)
                    if (dist.ContainsKey(node) && (dist[node] < bestPrice))
                    {
                        bestPrice = dist[node];
                        toOpen = node;
                    }
                notOpened.Remove(toOpen);

                if (isTarget(toOpen))
                    return RevertPath(levelView, toOpen, prev);

                foreach (var stepOffset in Offset.StepOffsets)
                {
                    var neighbour = toOpen + stepOffset;
                    if (!notOpened.Contains(neighbour))
                        continue;
                    var alt = dist[toOpen] + costs[neighbour];
                    if (alt < dist[neighbour])
                    {
                        dist[neighbour] = alt;
                        prev[neighbour] = toOpen;
                    }
                }
            }
            return RevertPath(levelView, current, prev);
        }

        private static IEnumerable<Location> RevertPath(LevelView levelView, Location target,
            Dictionary<Location, Location> prev)
        {
            var last = target;
            var path = new List<Location>();

            while (last != levelView.Player.Location)
            {
                path.Add(last);
                last = prev[last];
            }
            path.Reverse();

            return path;
        }
    }
}
