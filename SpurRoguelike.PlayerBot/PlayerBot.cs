using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SpurRoguelike.Core;
using SpurRoguelike.Core.Entities;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class PlayerBot : IPlayerController
    {
        private const int maxHealth = 100;
        public State<PlayerBot> State;
        public int Health { get; private set; }
        public int Defence { get; private set; }
        public int Attack { get; private set; }
        public Location Location { get; private set; }
        public ItemView Equipment { get; private set; }

        public Turn MakeTurn(LevelView levelView, IMessageReporter messageReporter)
        {
            Thread.Sleep(100);
            UpdateInfo(levelView);

            if (State == null)
                State = new StateIdle(this);
            messageReporter.ReportMessage(State.ToString());

            return State.MakeTurn(levelView);
        }

        public void UpdateInfo(LevelView levelView)
        {
            Health = levelView.Player.Health;
            Defence = levelView.Player.Defence;
            Attack = levelView.Player.Attack;
            Location = levelView.Player.Location;
        }

        private static bool IsInAttackRange(Location a, Location b)
        {
            return a.IsInRange(b, 1);
        }

        private static IEnumerable<Location> BFS(LevelView levelView, Func<Location, bool> isTarget)
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

                    if (levelView.Field[next] > CellType.Trap && !visited.Contains(next) && !levelView.GetHealthPackAt(next).HasValue)
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
            const int safeDistance = 3;
            const int influenceValue = 10;
            var cost = new int[levelView.Field.Width, levelView.Field.Height];

            for (var y = 0; y < levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                {
                    var position = new Location(x, y);
                    if (levelView.Field[position] == CellType.Wall)
                        continue;
                    foreach (var monster in levelView.Monsters)
                    {
                        var distance = (monster.Location - position).Size();
                        if (distance > safeDistance)
                            continue;
                        cost[x, y] += (int) (influenceValue/Math.Pow(2, distance - safeDistance));
                    }
                    if (levelView.Field[position] == CellType.Wall)
                        foreach (var offset in Offset.StepOffsets)
                        {
                            var help = position + offset;
                            if ((help.X > 0) && (help.X < levelView.Field.Width) && (help.Y > 0) &&
                                (help.Y < levelView.Field.Height))
                                cost[help.X, help.Y] += 40;
                        }
                    cost[x, y] = cost[x, y] == 0 ? 1 : cost[x, y];
                }

            return cost;
        }

        private static IEnumerable<Location> Dijkstra(LevelView levelView, Func<Location, bool> isTarget)
        {
            var cost = CalculateInfluence(levelView);
            HtmlGenerator.WriteHtml(levelView, cost, @"C:\Users\Mikhail\Downloads\HtmlGenerator\result\index.html");
            var current = levelView.Player.Location;
            var dist = new Dictionary<Location, int> {[current] = 0};
            var prev = new Dictionary<Location, Location>();
            var notOpened = new HashSet<Location> {current};

            for (var y = 0; y < levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                {
                    var location = new Location(x, y);
                    if ((levelView.Field[location] > CellType.Trap) && (location != current) &&
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
                const double panicHealth = 50;
                if (Bot.Health < panicHealth)
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if ((Bot.Health > panicHealth) && levelView.Monsters.Any())
                {
                    GoToState(() => new StateAttack(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!Bot.Equipment.HasValue)
                {
                    GoToState(() => new StateEquip(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any() && (Bot.Health != maxHealth))
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any())
                {
                    GoToState(() => new StateEquip(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
                var path = BFS(levelView, location => location == exitLocation);
                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.State = factory();
            }
        }

        private class StateHeal : State<PlayerBot>
        {
            private const int panicHealth = 50;

            public StateHeal(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (levelView.Monsters.Any() && (Bot.Health > panicHealth))
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any() && (Bot.Health == maxHealth))
                {
                    GoToState(() => new StateFindExit(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                if (!levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateAttack(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var pathToHealth = Dijkstra(levelView, location => levelView.GetHealthPackAt(location).HasValue);

                return Turn.Step(pathToHealth.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.State = factory();
            }
        }

        private class StateEquip : State<PlayerBot>
        {
            public StateEquip(PlayerBot self) : base(self)
            {
            }

            private static double GetItemValue(ItemView item)
            {
                return item.AttackBonus + item.DefenceBonus;
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (!levelView.Monsters.Any())
                {
                    var items = levelView.Items.OrderByDescending(GetItemValue);
                    var bestItem = items.First();
                    if (GetItemValue(Bot.Equipment) < GetItemValue(bestItem))
                    {
                        var pathToBest = BFS(levelView, (location) => location == bestItem.Location);
                        Bot.Equipment = levelView.Player.TryGetEquippedItem(out bestItem) ? bestItem : default(ItemView);
                        return Turn.Step(pathToBest.First() - Bot.Location);
                    }
                    GoToState(() => new StateFindExit(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                var nearestItem =
                        levelView.Items.OrderBy(i => (i.Location - levelView.Player.Location).Size())
                            .FirstOrDefault();
                var nearestMonster =
                    levelView.Monsters.OrderBy(i => (i.Location - levelView.Player.Location).Size())
                        .FirstOrDefault();
                if ((nearestItem.Location - Bot.Location).Size() < (nearestMonster.Location - Bot.Location).Size())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var path = BFS(levelView, location => location == nearestItem.Location);
                return Turn.Step(path.First() - Bot.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.State = factory();
            }
        }

        private class StateAttack : State<PlayerBot>
        {
            private const double panicHealth = maxHealth*0.5;

            public StateAttack(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if ((Bot.Health < panicHealth) && levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var nearbyMonster = levelView.Monsters.
                    OrderBy(m => (m.Location - levelView.Player.Location).Size()).
                    ThenBy(m => m.Health).
                    FirstOrDefault();
                if (nearbyMonster.HasValue && IsInAttackRange(levelView.Player.Location, nearbyMonster.Location))
                    return Turn.Attack(nearbyMonster.Location - levelView.Player.Location);

                var path = BFS(levelView, location => location == nearbyMonster.Location);
                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.State = factory();
            }
        }

        private class StateFindExit : State<PlayerBot>
        {
            public StateFindExit(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (levelView.Monsters.Any())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (levelView.Player.Health != maxHealth)
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
                var path = BFS(levelView, location => exitLocation == location);

                return Turn.Step(path.First() - levelView.Player.Location);
            }

            public override void GoToState<TState>(Func<TState> factory)
            {
                Bot.State = factory();
            }
        }
    }
}