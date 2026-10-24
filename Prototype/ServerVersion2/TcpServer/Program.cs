using Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tcp.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            TcpServer server = new TcpServer(new RequestHandler());
            server.LogMessage += (sender, message) => Console.WriteLine($"[{DateTime.Now} | {sender}] {message}");
            server.Start(new ServerConfig
            {
                Port = 5000,
                //StoragePath = null,
                SessionTimeoutMinutes = 20,
                RequestTimeoutSeconds = 100
            });

            // Start() returns immediately; keep the process alive until Enter is pressed.
            Console.WriteLine("Press Enter to stop.");
            Console.ReadLine();
            await server.Stop();
        }
    }
}
