using System;
using System.Collections.Generic;
using System.Linq;
using SpurRoguelike.Core;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    public class PlayerBot : IPlayerController
    {
        private const int maxHealth = 100;
        private const double panicHealth = maxHealth*0.50;
        public State<PlayerBot> State;
        public int Health { get; private set; }
        public int Defence { get; private set; }
        public int Attack { get; private set; }
        public Location Location { get; private set; }
        public ItemView Equipment { get; private set; }

        public Turn MakeTurn(LevelView levelView, IMessageReporter messageReporter)
        {
            UpdateInfo(levelView);
            if (State == null)
                State = new StateIdle(this);

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

        private static IEnumerable<Location> BFS(LevelView levelView, Func<Location, bool> isTarget,
            bool acceptPickup = false)
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

        private static int[,] CalculateInfluence(LevelView levelView)
        {
            const int safeDistance = 3;
            const int monsterInfluence = 15;
            const int wallInfluence = 10;
            var cost = new int[levelView.Field.Width, levelView.Field.Height];

            for (var y = 0; y < levelView.Field.Height; y++)
                for (var x = 0; x < levelView.Field.Width; x++)
                {
                    var position = new Location(x, y);
                    foreach (var monster in levelView.Monsters)
                    {
                        var distance = (monster.Location - position).Size();
                        if (distance > safeDistance)
                            continue;
                        cost[x, y] += (int) (monsterInfluence/Math.Pow(2, distance - safeDistance));
                    }
                    if (levelView.Field[position] == CellType.Wall)
                        foreach (var offset in Offset.StepOffsets)
                        {
                            var help = position + offset;
                            if ((help.X > 0) && (help.X < levelView.Field.Width) && (help.Y > 0) &&
                                (help.Y < levelView.Field.Height))
                                cost[help.X, help.Y] += wallInfluence;
                        }
                    cost[x, y] = cost[x, y] == 0 ? 1 : cost[x, y];
                }

            return cost;
        }

        private static IEnumerable<Location> Dijkstra(LevelView levelView, Func<Location, bool> isTarget)
        {
            var cost = CalculateInfluence(levelView);
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
                    return RevertPath(levelView, toOpen, prev);

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

        private class StateIdle : State<PlayerBot>
        {
            public StateIdle(PlayerBot bot) : base(bot)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if ((Bot.Health < panicHealth) && levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if ((Bot.Health > panicHealth) && levelView.Monsters.Any())
                {
                    GoToState(() => new StateAttack(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any())
                {
                    GoToState(() => new StateEquip(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any() && (Bot.Health != maxHealth))
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (Bot.Health < panicHealth && !levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateAttack(Bot));
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
            public StateHeal(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (levelView.Monsters.Any() && (Bot.Health > panicHealth))
                {
                    GoToState(() => new StateAttack(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if ((!levelView.Monsters.Any() && (Bot.Health == maxHealth)) || !levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateFindExit(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                if (levelView.Monsters.Any() && !levelView.HealthPacks.Any())
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
                    ItemView currentItem;
                    levelView.Player.TryGetEquippedItem(out currentItem);
                    if (GetItemValue(currentItem) < GetItemValue(bestItem))
                    {
                        var pathToBestItem = BFS(levelView, location => location == bestItem.Location, true);
                        return Turn.Step(pathToBestItem.First() - levelView.Player.Location);
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
            public StateAttack(PlayerBot self) : base(self)
            {
            }

            public override Turn MakeTurn(LevelView levelView)
            {
                if (Bot.Health < panicHealth && levelView.HealthPacks.Any())
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
                if ((levelView.Player.Health != maxHealth) && levelView.HealthPacks.Any())
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