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
        public bool HasItem { get; private set; }

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

        private static Turn NextStep(LevelView levelView, IEnumerable<Location> path)
        {
            return Turn.Step(path.First() - levelView.Player.Location);
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
                if (!levelView.Monsters.Any() && !Bot.HasItem)
                {
                    GoToState(() => new StateEquip(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                if (!levelView.Monsters.Any() && Bot.Health != maxHealth && levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var exitLocation = levelView.Field.GetCellsOfType(CellType.Exit).FirstOrDefault();
                var path = Algorithm.BFS(levelView, location => location == exitLocation);
                return NextStep(levelView, path);
            }
        }

        private class StateHeal : State<PlayerBot>
        {
            public StateHeal(PlayerBot self) : base(self)
            {
            }
            
            public override Turn MakeTurn(LevelView levelView)
            {
                if (levelView.Monsters.Any() && ((Bot.Health > panicHealth) || !levelView.HealthPacks.Any()))
                {
                    GoToState(() => new StateAttack(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (Bot.Health == maxHealth || !levelView.HealthPacks.Any())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }

                var pathToHealth = Algorithm.Dijkstra(levelView,
                    location => IsHealthPack(levelView, location));

                return NextStep(levelView, pathToHealth);
            }

            private static bool IsHealthPack(LevelView levelView, Location location)
            {
                return levelView.GetHealthPackAt(location).HasValue;
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
                        var pathToBestItem = Algorithm.BFS(levelView, location => location == bestItem.Location, true);
                        return Turn.Step(pathToBestItem.First() - levelView.Player.Location);
                    }
                    Bot.HasItem = true;
                    GoToState(() => new StateIdle(Bot));
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
                var path = Algorithm.BFS(levelView, location => location == nearestItem.Location);
                return NextStep(levelView, path);
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
                    GoToState(() => new StateHeal(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                if (!levelView.Monsters.Any())
                {
                    GoToState(() => new StateIdle(Bot));
                    return Bot.State.MakeTurn(levelView);
                }
                var nearbyMonster = levelView.Monsters.
                    OrderBy(m => (m.Location - Bot.Location).Size()).
                    ThenBy(m => m.Health).
                    FirstOrDefault();
                if (nearbyMonster.HasValue && IsInAttackRange(Bot.Location, nearbyMonster.Location))
                    return Turn.Attack(nearbyMonster.Location - Bot.Location);

                var path = Algorithm.BFS(levelView, location => location == nearbyMonster.Location);
                return NextStep(levelView, path);
            }
        }
    }
}