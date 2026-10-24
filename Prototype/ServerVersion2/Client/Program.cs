using System.Net.Sockets;
using System.Security.Cryptography;
using Tcp.Client;
using Tcp.Shared;

namespace Client
{
    /// <summary>
    /// Tests the running server over TCP. No fake data: uses the test-only Echo / EchoFile operations,
    /// plus the session/role checks every other operation runs before reaching the database.
    ///
    /// Run the Server project first, then:  dotnet run --project Client [host] [port]
    /// Exit code 0 = all passed.
    /// </summary>
    internal class Program
    {
        private static string host = "127.0.0.1";
        private static int port = Wire.DefaultPort;
        private static ServerConnection connection = new ServerConnection(host, port);
        private static int sessionExpiredCount;
        private static int passed;
        private static int failed;

        static async Task<int> Main(string[] args)
        {
            if (args.Length > 0)
            {
                host = args[0];
            }
            if (args.Length > 1)
            {
                port = int.Parse(args[1]);
            }
            connection = new ServerConnection(host, port);
            connection.SessionExpired += OnSessionExpired;

            Console.WriteLine("Testing server " + host + ":" + port);

            await Run("Echo", TestEcho);
            await Run("EchoFile", TestEchoFile);
            await Run("Login", TestLogin);
            await Run("Session checks", TestSessionChecks);
            await Run("Rejected upload", TestRejectedUpload);
            await Run("Malformed frame", TestMalformedFrame);
            await Run("Concurrency", TestConcurrency);
            await Run("Unreachable server", TestUnreachable);

            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        private static async Task TestEcho()
        {
            EchoBody sent = new EchoBody();
            sent.Text = "Bài tập 1 – đề cương";
            sent.Number = 42;

            Response response = await connection.Send(MessageType.Echo, sent);
            Check("Echo with body -> Ok", response.IsOk, response.Status + " " + response.Error);

            EchoBody back = response.GetBody<EchoBody>();
            Check("Echo body round-trips, Vietnamese text intact", back.Text == sent.Text && back.Number == sent.Number, back.Text);

            response = await connection.Send(MessageType.Echo);
            Check("Echo without body -> Ok, no body", response.IsOk && response.Body == null);
        }

        private static async Task TestEchoFile()
        {
            byte[] data = RandomNumberGenerator.GetBytes(10_000_000);
            EchoResult result = await EchoFile(data);
            Check("EchoFile 10 MB -> Ok", result.Response.IsOk, result.Response.Status + " " + result.Response.Error);
            Check("EchoFile 10 MB -> identical bytes", result.File != null && result.File.AsSpan().SequenceEqual(data));

            result = await EchoFile(new byte[0]);
            Check("EchoFile 0 bytes -> Ok, empty file", result.Response.IsOk && result.File != null && result.File.Length == 0);

            result = await EchoFile(null);
            Check("EchoFile without file -> Error 'Invalid request'",
                result.Response.Status == ResponseStatus.Error && result.Response.Error == "Invalid request: Request has no file.",
                result.Response.Error ?? "");
        }

        private static async Task TestLogin()
        {
            EchoBody body = new EchoBody();
            Response response = await connection.Login(body);
            Check("Login -> Error 'Not implemented yet.' (no database)",
                response.Status == ResponseStatus.Error && response.Error == "Not implemented yet.",
                response.Status + " " + response.Error);
            Check("Login failed -> client not logged in", !connection.IsLoggedIn);
        }

        private static async Task TestSessionChecks()
        {
            IEnumerable<MessageType> protectedTypes =
                from type in Enum.GetValues<MessageType>()
                where type != MessageType.Login && type != MessageType.Echo && type != MessageType.EchoFile
                select type;

            int total = 0;
            int unauthorized = 0;
            sessionExpiredCount = 0;
            foreach (MessageType type in protectedTypes)
            {
                total++;
                Response response = await connection.Send(type);
                if (response.Status == ResponseStatus.Unauthorized && response.Type == type)
                {
                    unauthorized++;
                }
                else
                {
                    Console.WriteLine("    " + type + " -> " + response.Status);
                }
            }
            Check("Every protected operation without session -> Unauthorized", unauthorized == total, unauthorized + "/" + total);
            Check("SessionExpired event raised for each", sessionExpiredCount == total, sessionExpiredCount + "/" + total);

            Response fake = await RawExchange(Request.Create(MessageType.GetProfile, "0123456789ABCDEF"));
            Check("Made-up SessionId -> Unauthorized", fake.Status == ResponseStatus.Unauthorized);

            await connection.Logout();
            Check("Logout while logged out -> no error", !connection.IsLoggedIn);
        }

        private static async Task TestRejectedUpload()
        {
            string path = Path.Combine(Path.GetTempPath(), "qlbt-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(5_000_000));
            try
            {
                Response response = await connection.Upload(MessageType.SubmitAssignment, new EchoBody(), path);
                Check("5 MB upload rejected (no session) -> client still gets Unauthorized",
                    response.Status == ResponseStatus.Unauthorized, response.Status.ToString());
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static async Task TestMalformedFrame()
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F });
                int read = await stream.ReadAsync(new byte[1]);
                Check("Invalid length header -> server closes connection", read == 0);
            }

            Response response = await connection.Send(MessageType.Echo);
            Check("Server still serving afterwards", response.IsOk);
        }

        private static async Task TestConcurrency()
        {
            List<byte[]> files = new List<byte[]>();
            List<Task<EchoResult>> tasks = new List<Task<EchoResult>>();
            for (int i = 0; i < 10; i++)
            {
                byte[] data = RandomNumberGenerator.GetBytes(1_000_000);
                files.Add(data);
                tasks.Add(EchoFile(data));
            }
            EchoResult[] results = await Task.WhenAll(tasks);

            int matching = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].Response.IsOk && results[i].File != null && results[i].File!.AsSpan().SequenceEqual(files[i]))
                {
                    matching++;
                }
            }
            Check("10 parallel EchoFile, each gets its own file back", matching == 10, matching + "/10");
        }

        private static async Task TestUnreachable()
        {
            ServerConnection wrongPort = new ServerConnection(host, port + 1);
            try
            {
                await wrongPort.Send(MessageType.Echo);
                Check("Wrong port -> SocketException", false, "no exception");
            }
            catch (SocketException)
            {
                Check("Wrong port -> SocketException", true);
            }
        }

        // ----- helpers -----

        // EchoFile needs upload and download in one exchange, so it uses Wire directly.
        private static async Task<EchoResult> EchoFile(byte[]? data)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                NetworkStream stream = client.GetStream();

                Request request = Request.Create(MessageType.EchoFile, null);
                if (data != null)
                {
                    request.FileLength = data.Length;
                }
                await Wire.WriteRequest(stream, request);
                if (data != null)
                {
                    await Wire.WriteFile(stream, new MemoryStream(data), data.Length);
                }

                EchoResult result = new EchoResult();
                result.Response = await Wire.ReadResponse(stream);
                if (result.Response.FileLength != null)
                {
                    MemoryStream received = new MemoryStream();
                    await Wire.ReadFile(stream, received, result.Response.FileLength.Value);
                    result.File = received.ToArray();
                }
                return result;
            }
        }

        private static async Task<Response> RawExchange(Request request)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                NetworkStream stream = client.GetStream();
                await Wire.WriteRequest(stream, request);
                return await Wire.ReadResponse(stream);
            }
        }

        private static async Task Run(string name, Func<Task> test)
        {
            Console.WriteLine();
            Console.WriteLine("[" + name + "]");
            try
            {
                await test();
            }
            catch (Exception ex)
            {
                Check(name + " threw", false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Check(string name, bool ok)
        {
            Check(name, ok, "");
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                passed++;
                Console.WriteLine("  PASS  " + name);
            }
            else
            {
                failed++;
                Console.WriteLine("  FAIL  " + name + "  -> " + detail);
            }
        }

        private static void OnSessionExpired(object? sender, EventArgs e)
        {
            sessionExpiredCount++;
        }
    }

    internal sealed class EchoBody
    {
        public string Text { get; set; } = "";
        public int Number { get; set; }
    }

    internal sealed class EchoResult
    {
        public Response Response { get; set; } = new Response();
        public byte[]? File { get; set; }
    }
}
