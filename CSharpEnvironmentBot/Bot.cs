using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpEnvironmentBot
{
    public static class DiscordIO
    {
        public abstract class Bot
        {
            private static readonly Queue<Bot> _bots = new Queue<Bot>();

            private Bot() { }

            internal abstract void Process(string message);

        }
    }
}
