using System.Text;
using Svr3.Client;
using Svr3.Shared;

// Usage: Svr3.Client [baseUrl] [bigFileMb]
//   baseUrl    default http://localhost:5080
//   bigFileMb  size of the timed submission upload, default 50
Console.OutputEncoding = Encoding.UTF8;

string baseUrl = args.Length > 0 ? args[0] : ApiRoutes.DefaultBaseUrl;
int bigFileMb = args.Length > 1 ? int.Parse(args[1]) : 50;
string workDir = Path.Combine(Path.GetTempPath(), "Svr3.Client");

Scenario scenario = new Scenario(new Uri(baseUrl), workDir, bigFileMb);
int failed = await scenario.Run();
return failed == 0 ? 0 : 1;
