using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SpurRoguelike.Core;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class PlayerBot : IPlayerController
    {
        public Dictionary<Location, Location> bfs = new Dictionary<Location, Location>();
        public Dictionary<Location, Location> path = new Dictionary<Location, Location>();
        public bool flag = true;

        public Turn MakeTurn(LevelView levelView, IMessageReporter messageReporter)
        {
            messageReporter.ReportMessage("Hey ho! I'm still breathing");
            Thread.Sleep(100);

            messageReporter.ReportMessage(levelView.Field[levelView.Player.Location].ToString());

            var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();

            bfs = BFS(levelView, exitLocation);
            path = MakePath(levelView);
            flag = false;

            messageReporter.ReportMessage(exitLocation + "Exit location");
            messageReporter.ReportMessage(levelView.Player.Location + "Player location");

            var nearbyMonster = levelView.Monsters.FirstOrDefault(m => IsInAttackRange(levelView.Player.Location, m.Location));

            //if (nearbyMonster.HasValue)
            //return Turn.Attack(nearbyMonster.Location - levelView.Player.Location);

            var playerLocation = levelView.Player.Location;
            var nextLocation = path[playerLocation];

            //return Turn.None;
            return Turn.Step(nextLocation - playerLocation);
        }

        private static bool IsInAttackRange(Location a, Location b)
        {
            return a.IsInRange(b, 1);
        }

        public Dictionary<Location, Location> BFS(LevelView levelView, Location target)
        {
            var queue = new Queue<Location>();
            queue.Enqueue(levelView.Player.Location);

            var visited = new HashSet<Location>();
            var pred = new Dictionary<Location, Location>();

            while (queue.Any())
            {
                var current = queue.Dequeue();
                visited.Add(current);

                if (current == target)
                    break;

                foreach (var offset in Offset.StepOffsets)
                {
                    var next = current + offset;

                    if ((levelView.Field[next] == CellType.Exit || levelView.Field[next] == CellType.Empty) && !visited.Contains(next))
                    {
                        queue.Enqueue(next);
                        visited.Add(next);
                        pred[next] = current;
                    }
                }
            }

            return pred;
        }

        public Dictionary<Location, Location> MakePath(LevelView levelView)
        {
            var last = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();

            while (last != levelView.Player.Location)
            {
                var pred = bfs[last];
                path[pred] = last;
                last = pred;
            }

            return path;
        }
    }
}