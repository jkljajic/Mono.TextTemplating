using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SignalR.TsGeneration.Hubs;

namespace SignalR.TsGeneration
{
    class Program
    {
        static async Task Main (string[] args)
        {
            var builder = WebApplication.CreateBuilder (args);
            builder.Services.AddSignalR ();
            var app = builder.Build ();
            app.Urls.Add ("http://localhost:5000");

            app.MapHub<ChatHub> ("/hubs/chat");
            app.MapHub<NotificationHub> ("/hubs/notifications");

            app.MapGet ("/", () => new {
                server = "SignalR.TsGeneration",
                hubs = new[] { "/hubs/chat", "/hubs/notifications" }
            });

            Console.WriteLine ("SignalR T4 Sample — http://localhost:5000");
            await app.RunAsync ();
        }
    }
}
