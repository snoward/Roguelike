using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpurRoguelike.Core;
using SpurRoguelike.Core.Entities;
using SpurRoguelike.Core.Primitives;
using SpurRoguelike.Core.Views;

namespace SpurRoguelike.PlayerBot
{
    internal abstract class State<T> where T : IPlayerController
    {   
        protected State(T self)
        {
            Bot = self;
        }

        public abstract Turn MakeTurn(LevelView levelView);

        public abstract void GoToState<TState>(Func<TState> factory) where TState : State<T>;

        protected T Bot;
        protected LevelView levelView;
    }
}
