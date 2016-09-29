using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using SpurRoguelike;
using SpurRoguelike.Core;
using SpurRoguelike.Core.Entities;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class PlayerBot : IPlayerController
    {
        private Monster enemy;
        private Entity objective;
        private State<PlayerBot> state;
        public int Health { get; private set; }
        private const int maxHealth = 100;
        private const int panicHealth = 50;
        public int Defence { get; private set; }
        public int Attack { get; private set; }

        public void UpdateInfo(LevelView levelView)
        {
            Health = levelView.Player.Health;
            Defence = levelView.Player.Defence;
            Attack = levelView.Player.Attack;
        }

        public Turn MakeTurn(LevelView levelView, IMessageReporter messageReporter)
        {
            messageReporter.ReportMessage("Hey ho! I'm still breathing");
            Thread.Sleep(100);
            UpdateInfo(levelView);

            if (state == null)
                state = new StateIdle(this);

            //messageReporter.ReportMessage(levelView.Field[levelView.Player.Location].ToString());

            var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
            messageReporter.ReportMessage(state.ToString());
            //var path = FindPath(levelView, exitLocation);

            messageReporter.ReportMessage(exitLocation + " Exit location");
            messageReporter.ReportMessage(levelView.Player.Location + " Player location");

            //var nearbyMonster = levelView.Monsters.FirstOrDefault(m => IsInAttackRange(levelView.Player.Location, m.Location));
            //if (nearbyMonster.HasValue)
            //return Turn.Attack(nearbyMonster.Location - levelView.Player.Location);

           // var playerLocation = levelView.Player.Location;
            //var nextLocation = path.First();
            var cost = CalculateInfluence(levelView);

            return state.MakeTurn(levelView);
        }

        private static bool IsInAttackRange(Location a, Location b)
        {
            return a.IsInRange(b, 1);
        }

        private static List<Location> BFS(LevelView levelView, Func<Location, bool> isTarget)
        {
            var queue = new Queue<Location>();
            var start = levelView.Player.Location;
            queue.Enqueue(start);

            var visited = new HashSet<Location> {start};
            var pred = new Dictionary<Location, Location>();

            while (queue.Any())
            {
                var current = queue.Dequeue();
                if (isTarget(current))
                    return FindPath(levelView, current, pred);

                foreach (var offset in Offset.StepOffsets)
                {
                    var next = current + offset;

                    if (((levelView.Field[next] > CellType.Trap) && !visited.Contains(next)))
                    {
                        queue.Enqueue(next);
                        visited.Add(next);
                        pred[next] = current;
                    }
                }
            }

            return FindPath(levelView, levelView.Player.Location, pred);
        }

        private static int[,] CalculateInfluence(LevelView levelView)
        {
            var cost = new int[levelView.Field.Width, levelView.Field.Height];
            var monsters = levelView.Monsters;

            foreach (var monster in levelView.Monsters)
            {
                for (var radius=1; radius < (int)Math.Log(monster.TotalAttack, 2); radius++)
                    foreach (var offset in Offset.AttackOffsets)
                    {
                        var neighbourField = monster.Location + offset;
                        cost[neighbourField.X * radius, neighbourField.Y * radius] += Math.Max(monster.TotalAttack / 2 * radius, 1);
                    }
            }

            for (var y=0; y< levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                    cost[x,y] = cost[x, y] == 0 ? 1 : cost[x, y];

            return cost;
        }

        private static List<Location> Dijkstra(LevelView levelView, Func<Location, bool> isTarget)
        {
            var cost = CalculateInfluence(levelView);
            var current = levelView.Player.Location;
            var dist = new Dictionary<Location, int> {[current] = 0};
            var prev = new Dictionary<Location, Location> {};
            var notOpened = new HashSet<Location> {current};

            for (var y = 0; y < levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                {
                    var location = new Location(x, y);
                    if (levelView.Field[location] > CellType.Trap && location != current)
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
                    if (dist.ContainsKey(node) && dist[node] < bestPrice)
                    {
                        bestPrice = dist[node];
                        toOpen = node;
                    }
                notOpened.Remove(toOpen);

                if (isTarget(toOpen))
                    return FindPath(levelView, toOpen, prev);

                foreach (var stepOffset in Offset.StepOffsets)
                {
                    var neighbour = toOpen + stepOffset;
                    if (!notOpened.Contains(neighbour))
                        continue;
                    var alt = dist[toOpen] + cost[neighbour.X, neighbour.Y];
                    if (alt < dist[neighbour])
                    {
                        dist[neighbour] = alt;
                        prev[neighbour] = toOpen;
                    }
                }
            }
            return FindPath(levelView, current, prev);
        }

        public static List<Location> FindPath(LevelView levelView, Location target, Dictionary<Location, Location> prev)
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

        private class StateIdle : State<PlayerBot>
        {
            public StateIdle(PlayerBot bot) : base(bot)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (Bot.Health < panicHealth)
                {
                    GoToState(() => new FindHealthState(Bot));
                    return Bot.state.MakeTurn(levelView);
                }
                if (Bot.Health > panicHealth && levelView.Monsters.Any())
                {
                    GoToState((() => new AttackState(Bot)));
                    return Bot.state.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any())
                {
                    GoToState((() => new FindExitState(Bot)));
                    return Bot.state.MakeTurn(levelView);
                }

                var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
                var path = BFS(levelView, (l) => l == exitLocation);
                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.state = factory();

            }
        }

        private class FindHealthState : State<PlayerBot>
        {
            public FindHealthState(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (Bot.Health > panicHealth)
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.state.MakeTurn(levelView);
                }

                var pathToHealth = Dijkstra(levelView, location => levelView.GetHealthPackAt(location).HasValue);
                return Turn.Step(pathToHealth.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.state = factory();
            }
        }

        private class AttackState : State<PlayerBot>
        {
            public AttackState(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (Bot.Health < panicHealth)
                {
                    GoToState((() => new StateIdle(Bot)));
                    return Bot.state.MakeTurn(levelView);
                }

                var nearbyMonster = levelView.Monsters.OrderBy(m => (m.Location - levelView.Player.Location).Size()).ThenBy(m => m.Health).FirstOrDefault();
                if (nearbyMonster.HasValue && IsInAttackRange(levelView.Player.Location, nearbyMonster.Location))
                    return Turn.Attack(nearbyMonster.Location - levelView.Player.Location);
                
                var path = BFS(levelView, (location => location == nearbyMonster.Location));
                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.state = factory();
            }
        }

        private class FindExitState : State<PlayerBot>
        {
            public FindExitState(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (levelView.Monsters.Any())
                {
                    GoToState((() => new StateIdle(Bot)));
                    return Bot.state.MakeTurn(levelView);
                }

                var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
                var path = BFS(levelView, (location) => exitLocation == location);

                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.state = factory();
            }
        }
    }
}