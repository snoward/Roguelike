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
    public abstract class State<T> where T : PlayerBot
    {   
        protected State(T self)
        {
            Bot = self;
        }

        public abstract Turn MakeTurn(LevelView levelView);

        public void GoToState(Func<State<PlayerBot>> factory)
        {
            Bot.State = factory();
        } 

        protected T Bot;
    }
}
