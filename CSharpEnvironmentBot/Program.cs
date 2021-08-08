using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace CSharpEnvironmentBot
{
    class Program
    {

        private static readonly DiscordSocketConfig config = new()
        {
            AlwaysDownloadUsers = true,
            MessageCacheSize = 1000
        };

        public static DiscordSocketClient _client;

        public static async Task Main(string[] args)
        {
            _client = new DiscordSocketClient(config);

            await _client.LoginAsync(TokenType.Bot, args[0]);

            await _client.StartAsync();

            _client.Log += Log;

            _client.MessageReceived += InstanceManager.EnqueueMessage;

            await Task.Delay(-1);
        }

        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
