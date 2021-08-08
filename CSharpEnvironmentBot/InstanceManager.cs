using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSharpEnvironmentBot.Multi;

namespace CSharpEnvironmentBot
{
    public static class InstanceManager
    {
        private static readonly ConcurrentDictionary<string, CSharpInstance> _CSharpInstances;
        private const string AdminPrefix = "CSharpEnvironment";

        static InstanceManager()
        {
            _CSharpInstances = new();
        }

        // Determin if a user has Administrator permissions in any of their roles or if its a DM they inherently are admin
        private static bool IsAdmin(SocketUser user, string serverId)
        {
            try
            {
                return serverId == string.Empty || ((SocketGuildUser)user).Roles.Any(role => role.Permissions.Administrator == true);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string DataCapacity(long bytes)
        {
            if (bytes >= 1073741824) return $"{Math.Round(bytes / 1073741824.0, 2)} GB";
            else if (bytes >= 1048576) return $"{Math.Round(bytes / 1048576.0, 2)} MB";
            else if (bytes >= 1024) return $"{Math.Round(bytes / 1024.0, 2)} KB";
            return $"{bytes} B";
        }

        // Locating/Creating CSharpIngstance associated to incoming message
        public static async Task EnqueueMessage(SocketMessage msg)
        {
            // If the message is from a bot, discard the message (for now), future features might require handling bot messages
            if (msg.Author.IsBot) return;

            // Get Server ID
            string serverId = string.Empty;
            try
            {
                // Gets the serverID, fails if message is a DM and serverID stays string.Empty
                serverId = ((SocketGuildChannel)msg.Channel).Guild.Id.ToString();
            }
            catch (Exception) { }
            bool isAdmin = IsAdmin(msg.Author, serverId);

            // Get channel name or DM userID
            string channelId = serverId == string.Empty ? msg.Author.Id.ToString() : msg.Channel.Name;
            string instanceKey = serverId + channelId;

        Redo:
            if (_CSharpInstances.TryGetValue(instanceKey, out var csi)) // Get the current instance if one exists
            {
                // If channel ids mismatch, remove the stored instance and dispose of it
                if (msg.Channel.Id != csi.Channel.Id)
                {
                    if (_CSharpInstances.TryRemove(instanceKey, out csi)) csi.Dispose();
                    goto Redo;
                }

                if (isAdmin && msg.Content.StartsWith(AdminPrefix))
                    switch (msg.Content[AdminPrefix.Length..].Trim().ToLower())
                    {
                        case "deafen":
                            {
                                try
                                {
                                    if (csi.Listening)
                                    {
                                        csi.Listening = false;
                                        await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment stopped listening");
                                    }
                                    else await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already deafen");
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "listen":
                            {
                                try
                                {
                                    if (csi.Listening) await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already listening");
                                    else
                                    {
                                        csi.Listening = true;
                                        await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment started listening");
                                    }
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "mute":
                            {
                                try
                                {
                                    if (csi.Muted) await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already muted");
                                    else
                                    {
                                        csi.Muted = true;
                                        await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment muted");
                                    }
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "unmute":
                            {
                                try
                                {
                                    if (csi.Muted)
                                    {
                                        csi.Muted = false;
                                        await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment unmuted");
                                    }
                                    else await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already unmuted");
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "restart": // Restarts the CSharpInstance for the channel (used when the CSharpInstance is hanging or for a clean slate)
                            {
                                try
                                {
                                    csi.Restart(msg.Channel);
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment restarted");
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "start":
                            {
                                if (serverId == string.Empty || msg.Channel.Name.Trim() == "csharp-environment")
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already started, Dedicated Instance");
                                else await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already started");
                                break;
                            }
                        case "stop":
                            {
                                if (serverId == string.Empty || msg.Channel.Name.Trim() == "csharp-environment")
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment cannot stop, Dedicated Instance");
                                else
                                {
                                    if (_CSharpInstances.TryRemove(instanceKey, out csi))
                                    {
                                        try
                                        {
                                            csi.Dispose();
                                            await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment stopped");
                                        }
                                        catch (Exception)
                                        {
                                            await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment failed to stop");
                                        }
                                    }
                                    else await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        case "status":
                            {
                                try
                                {
                                    (ulong id, bool listening, bool muted, DateTime timestamp, double cpuUsage, long memorySize, long peekMemory) = csi.Status;
                                    await ((IUserMessage)msg).ReplyAsync(@$"> CSharpEnvironment {id.ToString().PadLeft(20, '0')} {{
>       Started On: {timestamp} ,
>       Up Time: {(ulong)(DateTime.Now - timestamp).TotalSeconds}s ,
>       CPU Usage: %{cpuUsage} ,
>       Memory Usage: {DataCapacity(memorySize)} ,
>       Peek Memory Usage: {DataCapacity(peekMemory)} ,
>       Listening: {listening} ,
>       Muted: {muted}
> }}");
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        default:
                            {
                                await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment unknown command");
                                break;
                            }
                    }
                else csi.Run(msg, serverId, channelId);
            }
            else // CSharpInstance didnt exist
            {
                if (serverId == string.Empty || msg.Channel.Name.Trim() == "csharp-environment")
                {
                    // Create new dedicated instance and dispose of it if one already exists and then goto Redo
                    CSharpInstance instance = _CSharpInstances.GetOrAdd(instanceKey, key => csi = new(msg.Channel));
                    if (csi.Id == instance.Id) csi.Start();
                    else
                    {
                        csi.Dispose();
                        csi = instance;
                    }
                    goto Redo;
                }

                if (isAdmin && msg.Content.StartsWith(AdminPrefix))
                    switch (msg.Content[AdminPrefix.Length..].Trim().ToLower())
                    {
                        case "deafen" or "listen" or "mute" or "unmute" or "stop" or "status":
                            {
                                await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                break;
                            }
                        case "start":
                            {
                                CSharpInstance instance = _CSharpInstances.GetOrAdd(instanceKey, key => csi = new(msg.Channel));
                                if (csi.Id == instance.Id)
                                {
                                    csi.Start();
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment started");
                                }
                                else
                                {
                                    csi.Dispose();
                                    csi = instance;
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment already started");
                                }
                                break;
                            }
                        case "restart": // Restarts the CSharpInstance for the channel (used when the CSharpInstance is hanging or for a clean slate)
                            {
                                try
                                {
                                    csi.Restart(msg.Channel);
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment restarted");
                                }
                                catch (Exception)
                                {
                                    await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment not started");
                                }
                                break;
                            }
                        default:
                            {
                                await ((IUserMessage)msg).ReplyAsync("> CSharpEnvironment unknown command");
                                break;
                            }
                    }
            }
        }

        public class CSharpInstance : IDisposable
        {
            private static readonly string[] _redefinitions = new string[]
            {
                "#r \"nuget: System.Drawing.Common, 5.0.2\"", // Install nuget package to use Bitmaps etc
                "public class Process { }", // Prohibits Process()
                "public static class Diagnostics { public class Process { } }", // Prohibits Diagnostics.Process
                // Static class to replace Console
                @"public static class DiscordIO
{
    private static readonly System.Threading.SemaphoreSlim _consoleWriteMutex = new(1, 1);

    public static class Console
    {
        public static async System.Threading.Tasks.Task WriteLine(object obj)
        {
            string objAsString = obj.GetType() == typeof(string) ? (string)obj : obj.ToString();
            byte[] objBytes = System.Text.Encoding.UTF8.GetBytes(objAsString);
            string objB64 = System.Convert.ToBase64String(objBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""0{objB64}"");
            _consoleWriteMutex.Release();
        }

        public static async System.Threading.Tasks.Task WriteQuote(object obj)
        {
            string[] objAsString = obj.GetType() == typeof(string) ? ((string)obj).Split('\n') : obj.ToString().Split('\n');
            System.Text.StringBuilder sb = new();
            foreach (string str in objAsString)
                sb.AppendLine($""> {str}"");
            byte[] objBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            string objB64 = System.Convert.ToBase64String(objBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""0{objB64}"");
            _consoleWriteMutex.Release();
        }

        public static async System.Threading.Tasks.Task WriteCode(object obj)
        {
            string[] objAsString = obj.GetType() == typeof(string) ?
                ((string)obj).Replace('`', 'ˋ').Split('\n') :
                obj.ToString().Replace('`', 'ˋ').Split('\n');
            System.Text.StringBuilder sb = new();
            foreach (string line in objAsString)
                sb.AppendLine($""`{line}`"");
            byte[] objBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            string objB64 = System.Convert.ToBase64String(objBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""0{objB64}"");
            _consoleWriteMutex.Release();
        }

        // Had to create overloads because environment doesn't supposed optional variables
        public static async System.Threading.Tasks.Task WriteBlock(object obj) => WriteBlock(obj, string.Empty);
        public static async System.Threading.Tasks.Task WriteBlock(object obj, string type)
        {
            string objAsString = obj.GetType() == typeof(string) ?
                ((string)obj).Replace('`', 'ˋ') :
                obj.ToString().Replace('`', 'ˋ');
            byte[] objBytes = System.Text.Encoding.UTF8.GetBytes($""```{type}\n{objAsString}\n```"");
            string objB64 = System.Convert.ToBase64String(objBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""0{objB64}"");
            _consoleWriteMutex.Release();
        }

        // Had to create overloads because environment doesn't supposed optional variables
        public static async System.Threading.Tasks.Task WriteBytes(byte[] arr) => WriteBytes(arr, ""data"", ""bin"");
        public static async System.Threading.Tasks.Task WriteBytes(byte[] arr, string name) => WriteBytes(arr, name, ""bin"");
        public static async System.Threading.Tasks.Task WriteBytes(byte[] arr, string name, string extension)
        {
            string arrB64 = System.Convert.ToBase64String(arr);
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes($""{name ?? ""data""}.{extension ?? ""bin""}"");
            string nameB64 = System.Convert.ToBase64String(nameBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""1{arrB64}|{nameB64}"");
            _consoleWriteMutex.Release();
        }

        // Had to create overloads because environment doesn't supposed optional variables
        public static async System.Threading.Tasks.Task WriteImage(System.Drawing.Image image) => WriteImage(image, System.Drawing.Imaging.ImageFormat.Jpeg, ""image"");
        public static async System.Threading.Tasks.Task WriteImage(System.Drawing.Image image, System.Drawing.Imaging.ImageFormat format) => WriteImage(image, format, ""image"");
        public static async System.Threading.Tasks.Task WriteImage(System.Drawing.Image image, System.Drawing.Imaging.ImageFormat format, string name)
        {
            using System.IO.MemoryStream ms = new();
            image.Save(ms, format);
            string arrB64 = System.Convert.ToBase64String(ms.ToArray());
            ms.Dispose();
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes($""{name}.{format.ToString().ToLower()}"");
            string nameB64 = System.Convert.ToBase64String(nameBytes);
            await _consoleWriteMutex.WaitAsync();
            System.Console.WriteLine($""1{arrB64}|{nameB64}"");
            _consoleWriteMutex.Release();
        }
    }
}",
                "using static DiscordIO;"
            };

            public ISocketMessageChannel Channel { get; private set; }
            private static ulong InstanceCounter = (ulong)Stopwatch.GetTimestamp();
            public readonly ulong Id;
            private NamedPipeServerStream pipe = null;
            private Process proc = null;
            private readonly SemaphoreSlim _lock;
            public bool Listening = false;
            public bool Muted = false;

            private DateTime _lastTime;
            private TimeSpan _lastTotalProcessorTime;
            private DateTime _crntTime;
            private TimeSpan _crntTotalProcessorTime;

            public CSharpInstance(ISocketMessageChannel channel)
            {
                _lock = new(1, 1);
                Id = Interlocked.Increment(ref InstanceCounter);
                Channel = channel;
            }

            public (ulong id, bool listening, bool muted, DateTime timestamp, double cpuUsage, long memorySize, long peekMemory) Status
            {
                get
                {
                    (ulong id, bool listening, bool muted, DateTime timestamp, double cpuUsage, long memorySize, long peekMemory) status = new();
                    status.id = Id;
                    status.listening = Listening;
                    status.muted = Muted;
                    _lock.Wait();
                    if (proc != null)
                    {
                        proc.Refresh();

                        _crntTime = DateTime.Now;
                        _crntTotalProcessorTime = proc.TotalProcessorTime;
                        status.cpuUsage = Math.Round((_crntTotalProcessorTime.TotalMilliseconds - _lastTotalProcessorTime.TotalMilliseconds) / (double)(_crntTime - _lastTime).TotalMilliseconds / Environment.ProcessorCount * 100.0, 2);
                        _lastTime = _crntTime;
                        _lastTotalProcessorTime = _crntTotalProcessorTime;

                        status.timestamp = proc.StartTime;
                        status.memorySize = proc.WorkingSet64;
                        status.peekMemory = proc.PeakWorkingSet64;
                    }
                    _lock.Release();
                    return status;
                }
            }

            // Starts the current environment
            public void Start()
            {
                _lock.Wait();
                if (proc == null)
                {
                    Try(
                        () => pipe = new(Id.ToString(), PipeDirection.Out, 1),
                        () => proc = new(),
                        () => proc.StartInfo.FileName = "dotnet-script",
                        () => proc.StartInfo.UseShellExecute = false,
                        () => proc.StartInfo.RedirectStandardOutput = true,
                        () => proc.StartInfo.RedirectStandardError = true,
                        () => proc.StartInfo.RedirectStandardInput = true,
                        () => proc.OutputDataReceived += OutputReceived,
                        () => proc.ErrorDataReceived += ErrorReceived,
                        () => proc.Start(),
                        () => _lastTime = DateTime.Now,
                        () => _lastTotalProcessorTime = proc.TotalProcessorTime,
                        () => proc.BeginOutputReadLine(),
                        () => proc.BeginErrorReadLine(),
                        () => Listening = true,
                        () => Muted = false,
                        () => { foreach (string def in _redefinitions) proc.StandardInput.WriteLine(def); });
                }
                _lock.Release();
            }

            // Kills and disposes of the current environment
            public void Stop()
            {
                _lock.Wait();
                if (proc != null)
                    Try(
                        () => Listening = false,
                        () => Muted = true,
                        () => proc.CancelErrorRead(),
                        () => proc.CancelOutputRead(),
                        () => proc.OutputDataReceived -= OutputReceived,
                        () => proc.ErrorDataReceived -= ErrorReceived,
                        () => proc.Kill(),
                        () => proc.Dispose(),
                        () => proc = null);
                _lock.Release();
            }

            // Reuse this instance but restart the environment with a new discord channel
            public void Restart(ISocketMessageChannel channel = null)
            {
                Stop();
                Channel = channel ?? Channel;
                Start();
            }

            // Asynchronously handle messages coming out of the environment
            private async void OutputReceived(object sender, DataReceivedEventArgs e)
            {
                if (!Muted)
                    for (int i = 0; i < e.Data.Length; i++)
                        switch (e.Data[i])
                        {
                            case '0':
                                {
                                    await HandleTextOutputs(e.Data[++i..]);
                                    return;
                                }
                            case '1':
                                {
                                    await HandleDataOutputs(e.Data[++i..]);
                                    return;
                                }
                        }
            }

            // For handling text related environment outputs
            private async Task HandleTextOutputs(string trimmed)
            {
                byte[] from64 = Convert.FromBase64String(trimmed);
                string decoded = Encoding.UTF8.GetString(from64);
                await Channel?.SendMessageAsync(decoded);
            }

            // For handling data related environment outputs
            private async Task HandleDataOutputs(string trimmed)
            {
                string[] parts = trimmed.Split('|');
                if (parts.Length == 2)
                {
                    // Decode the bytes
                    byte[] bytesFrom64 = Convert.FromBase64String(parts[0]);
                    // Decode the name
                    byte[] nameFrom64 = Convert.FromBase64String(parts[1]);
                    string nameDecoded = Encoding.UTF8.GetString(nameFrom64);
                    // Send data to discord
                    using MemoryStream ms = new(bytesFrom64);
                    await Channel?.SendFileAsync(ms, nameDecoded, null, false, null, null, false, null, null);
                }
            }

            // For handling environment error messsages
            private async void ErrorReceived(object sender, DataReceivedEventArgs e) =>
                await Channel?.SendMessageAsync(e.Data);

            // Asynchronously download text from a url, used for downloading imports
            private static async Task<string> DownloadAsync(string url)
            {
                try
                {
                    using WebClient client = new();
                    return await client.DownloadStringTaskAsync(url);
                }
                catch (Exception) { }
                return null;
            }

            // Resolves the code passed as a message so it can potentiall be ran in the environment
            private async Task<bool> Resolve(string code, StringBuilder codeList, Dictionary<string, bool> importTree, Queue<string> compiledOutput, string source)
            {
                string[] lines = code.Split('\n');

                // Checks for restricted code
                if (!Check(i => i == -1, false, out (int value, string produce)[] results,
                    (() => code.IndexOf(0, "using", "namespace"), "Limitation : 'namespace' is not supported"), // Check for using namespace
                    (() => code.IndexOf(0, "Console", ".", "Read", null, "(", null, ")"), "Limitation : Cannot read inputs from environment"), // Check for Console.Read...()
                    (() => code.IndexOf(0, "class", "Process"), "Restricted : Redefining 'Process' prohibited"), // Check for class Process
                    (() => code.IndexOf(0, "Environment", ".", "Exit", "(", null, ")"), "Restricted : Cannot terminate environment"), // Check for Environment.Exit()
                    (() => code.IndexOf(0, "System", ".", "Console"), "Restricted : 'System.Console' is reserved for internal use only"), // Check for Environment.Exit()
                    (() => code.IndexOf(0, "System", ".", "Diagnostics", ".", "Process"), "Restricted : 'System.Diagnostics.Process' is not supported"))) // Check for System.Diagnostics.Process
                {
                    // Calculate error locations
                    if (compiledOutput != null)
                    {
                        int lineNbr = 1;
                        foreach (string line in lines)
                        {
                            Iterate(results.Length - 1, i =>
                            {
                                if (results[i].value > -1)
                                {
                                    if (results[i].value - (line.Length + 1) < 0)
                                        compiledOutput.Enqueue($"> {source ?? "Main"} ({lineNbr},{results[i].value + 1}): {results[i].produce}");
                                    results[i].value -= line.Length + 1;
                                }
                            });
                            lineNbr++;
                        }
                    }
                    return false;
                }

                int parseLine = 0;
                string trimmedLine = "";
                Queue<string> importUrls = new();

                // Skip leading empty lines
                for (; parseLine < lines.Length; parseLine++) if ((trimmedLine = lines[parseLine].Trim()) != "") break;

                // Look for imports separated by spaces, imports are only valid at the very top
                if (trimmedLine.StartsWith("import"))
                {
                    // get list of imports
                    string[] imports = trimmedLine.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)[1..]; // skip first element
                    foreach (string import in imports) if (!importTree.ContainsKey(import)) importUrls.Enqueue(import); // Add to import list, no duplicates
                    lines[parseLine] = ""; // clear out the imports line
                    parseLine++;
                }

                // Reconstruct code
                StringBuilder sb = new();
                foreach (string line in lines) sb.AppendLine(line);
                string finalCode = sb.ToString();
                sb.Clear();

                // Check for syntax errors
                CompilationUnitSyntax parsed = CSharpSyntaxTree.ParseText(finalCode).GetCompilationUnitRoot();
                if (parsed.ContainsDiagnostics)
                {
                    if (compiledOutput != null) foreach (Diagnostic diag in parsed.GetDiagnostics().ToList()) compiledOutput.Enqueue($"> {source ?? "Main"} {diag}");
                    return false;
                }

                // Add to importsTree
                if (source != null) importTree.Add(source, true);

                // Download imports and resolve them first
                foreach (string import in importUrls)
                {
                    string newCode = await DownloadAsync(import);
                    if (newCode == null)
                    {
                        if (compiledOutput != null) compiledOutput.Enqueue($"> {import}: Importing failed, not found");
                        return false;
                    }
                    else
                    {
                        if (compiledOutput != null) compiledOutput.Enqueue($"> {import}: Importing");
                        if (!await Resolve(newCode, codeList, importTree, compiledOutput, import)) return false;
                    }
                }

                importUrls.Clear();
                codeList.AppendLine(finalCode);
                finalCode = null;
                return true;
            }

            // Entry point for all messages to be processed
            public async void Run(SocketMessage message, string serverId, string channelName)
            {
                if (!Listening) return;

                string code = "";

                // Determin if code block or shorthand commands, otherwise return
                if (message.Content.StartsWith("```cs\n") && message.Content.EndsWith("\n```")) code = message.Content[6..^4]; // C# code block
                else if (message.Content.StartsWith("$\"") && message.Content.EndsWith("\"")) code = $"Console.WriteLine($\"{message.Content[2..^1].Trim()}\");"; // Interpolated WriteLine shorthand
                else if (message.Content.StartsWith("$")) code = $"Console.WriteLine({message.Content[1..].Trim().TrimEnd(';')});"; // WriteLine shorthand
                else if (message.Content.StartsWith("&")) code = $"import {message.Content[1..].Trim()}";
                else return;

                StringBuilder codeList = new();
                Queue<string> compiledOutput = Muted ? null : new();
                Dictionary<string, bool> importTree = new();
                if (await Resolve(code, codeList, importTree, compiledOutput, null))
                {
                    if (importTree.Count > 0) compiledOutput?.Enqueue("> All imports compiled, Updating environment");

                    // Construct compiled output into 2000 character chunks
                    // TODO

                    await _lock.WaitAsync();
                    if (proc != null) await proc.StandardInput.WriteLineAsync(codeList.ToString());
                    _lock.Release();
                }
                else compiledOutput?.Enqueue("> Environment unchanged");

                // Temporary compiled output constructor, doesn't manage over 2000 character chunks
                StringBuilder sb = new();
                if (compiledOutput != null) foreach (string line in compiledOutput) sb.AppendLine(line);
                codeList.Clear();
                if (sb.Length > 2000) await Channel.SendMessageAsync(sb.ToString().Substring(0, 2000));
                else if (sb.Length > 0) await Channel.SendMessageAsync(sb.ToString());
            }

            // Typical Dispose pattern
            private bool disposedValue;
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Stop();
                        _lock.Dispose();
                    }
                    disposedValue = true;
                }
            }
            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}