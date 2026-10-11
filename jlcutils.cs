// dotnet build jlcutils.csproj -c Release
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using TakeCommand.Plugin;
using CSScriptLibrary;

public class jlcUtils : ITCCPlugin
{
    private static readonly TCCPluginInfo _info = new TCCPluginInfo
    {
        Name        = "jlcUtils",
        Author      = "Joe Caverly",
        Email       = "jlcaverly@pm.me",
        WWW         = "https://github.com/JoeC4281",
        Description = "Demonstrates calling TCC commands from .NET",
        Functions   = "CSTEST,CSX,DIRCOUNT,UNSAFE,WINDOWLIST,@CSTEST,@TCCEVAL",
        Major       = 1, Minor = 0, Build = 1
    };
    public TCCPluginInfo GetPluginInfo() => _info;
    public bool Initialize()            => true;
    public bool Shutdown(bool end)       => true;
    
		// Command: WINDOWLIST [all]
		// Lists titled top-level windows as CSV.
		// window handle,process ID,thread ID,process name,window title
		// Add "all" to include windows with empty titles.
		// Added 2026-10-10
		public int WINDOWLIST(StringBuilder args)
		{
				string option = args.ToString().Trim();

				if (option.Length > 0 &&
						!string.Equals(option, "all", StringComparison.OrdinalIgnoreCase))
				{
						Console.Error.WriteLine("Usage: WINDOWLIST [all]");
						return 1;
				}

				bool includeUntitled =
						string.Equals(option, "all", StringComparison.OrdinalIgnoreCase);

				var rows = new List<string[]>();

				try
				{
						// Collect the windows first so the array can be created
						// with the correct number of rows.
						bool completed = EnumWindows(
								delegate(IntPtr hWnd, IntPtr lParam)
								{
										var title = new StringBuilder(1024);
										GetWindowText(hWnd, title, title.Capacity);

										string windowTitle = title.ToString();
										if (!includeUntitled && string.IsNullOrEmpty(windowTitle))
												return true;

										uint processId;
										uint threadId =
												GetWindowThreadProcessId(hWnd, out processId);

										rows.Add(new[]
										{
												hWnd.ToInt64().ToString(),
												processId.ToString(),
												threadId.ToString(),
												GetProcessName(processId),
												windowTitle
										});

										return true;
								},
								IntPtr.Zero);

						if (!completed)
								return 1;

						// SETARRAY dimensions must be positive. If there are no
						// matching windows, create a one-row array with empty values.
						int arrayRows = Math.Max(rows.Count, 1);

						TakeCommand.PluginHost.InvokeCommand(
								string.Format("SETARRAY /F WindowList[{0},5]", arrayRows));
								
						TakeCommand.PluginHost.InvokeCommand(
						    string.Format("SET WindowListCount={0}", rows.Count));

						// Print each CSV row, then assign its five array elements.
						for (int row = 0; row < rows.Count; row++)
						{
								string[] fields = rows[row];

								// Cannot re-direct output,
								//   so commented the output lines.
								//Console.WriteLine(
								//		"{0},{1},{2},{3},{4}",
								//		fields[0],
								//		fields[1],
								//		fields[2],
								//		Csv(fields[3]),
								//		Csv(fields[4]));
								
								NativeStdout.WriteLine(string.Format(
										"{0},{1},{2},{3},{4}",
										fields[0],
										fields[1],
										fields[2],
										Csv(fields[3]),
										Csv(fields[4])));


								//var command = new StringBuilder();

								//for (int column = 0; column < 5; column++)
								//{
								//		if (column > 0)
								//				command.Append(" & ");

								//		command.AppendFormat(
								//				"SET WindowList[{0},{1}]={2}",
								//				row,
								//				column,
								//				QuoteForTcc(fields[column]));
								//}

								//TakeCommand.PluginHost.InvokeCommand(command.ToString());
						}

						return 0;
				}
				catch (Exception ex)
				{
						Console.Error.WriteLine("WINDOWLIST: " + ex.Message);
						return 1;
				}
		}

		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool EnumWindows(
				EnumWindowsProc callback,
				IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern int GetWindowText(
				IntPtr hWnd,
				StringBuilder text,
				int maxCount);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetWindowThreadProcessId(
				IntPtr hWnd,
				out uint processId);

		private static string GetProcessName(uint processId)
		{
				try
				{
						using (Process process = Process.GetProcessById((int)processId))
								return process.ProcessName;
				}
				catch
				{
						// The process may exit or be inaccessible during enumeration.
						return "";
				}
		}

		private static string Csv(string value)
		{
				if (value == null)
						value = "";

				if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
						return "\"" + value.Replace("\"", "\"\"") + "\"";

				return value;
		}

		private static string QuoteForTcc(string value)
		{
				if (value == null)
						value = "";

				// Back quotes protect the value from TCC variable expansion.
				return "`" + value + "`";
		}
 
    // Command: DIRCOUNT <path>
    // Prints the number of files in the given directory.
    public int DIRCOUNT(StringBuilder args)
    {
        string path = args.ToString().Trim();
        if (string.IsNullOrEmpty(path))
            path = ".";
        try
        {
            string output = TakeCommand.PluginHost.InvokeCommand($"dir /b \"{path}\"");
            int count = output.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            Console.WriteLine($"{count} file(s) in {path}");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"DIRCOUNT: {ex.Message}");
            return 1;
        }
    }

    public int CSTEST(StringBuilder args)
    {
        try
        {
            // Force CodeDomEvaluator (RoslynEvaluator is incompatible with TCC's plugin AppDomain)
            CSScript.EvaluatorConfig.Engine = EvaluatorEngine.CodeDom;

            dynamic script = CSScript.Evaluator.LoadCode(
                "using System;\n" +
                "public class Script {\n" +
                "    public string Hello() { return \"Hello from script\"; }\n" +
                "}");

            //Console.WriteLine(script.Hello());
            NativeStdout.WriteLine((string)script.Hello());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CSTEST error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
                Console.Error.WriteLine($"Inner stack trace:\n{ex.InnerException.StackTrace}");
            }
            return 1;
        }
    }

		public int f_CSTEST(StringBuilder args)
		{
				try
				{
						CSScript.EvaluatorConfig.Engine = EvaluatorEngine.CodeDom;

						dynamic script = CSScript.Evaluator.LoadCode(
								"public class Script {\n" +
								"    public string Hello() { return \"Hello from script\"; }\n" +
								"}");

						args.Clear();
						args.Append((string)script.Hello());
						return 0;
				}
				catch (Exception ex)
				{
						args.Clear();
						args.Append("CSTEST error: " + ex.Message);
						return 1;
				}
		}

    public int CSX(StringBuilder args)
    {
        try
        {
            CSScript.GlobalSettings.InMemoryAssembly = true;
            CSScript.EvaluatorConfig.Engine = EvaluatorEngine.CodeDom;

            string argsStr = args.ToString().Trim();
            if (string.IsNullOrEmpty(argsStr))
            {
                Console.Error.WriteLine("CSX: No script file specified");
                return 1;
            }

            string[] tokens = argsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string scriptFile = tokens[0];
            string[] scriptArgs = tokens.Length > 1 ? tokens.Skip(1).ToArray() : new string[0];

            if (!File.Exists(scriptFile))
            {
                Console.Error.WriteLine($"CSX: File not found: {scriptFile}");
                return 1;
            }

            string scriptCode = File.ReadAllText(scriptFile);
            dynamic script = CSScript.Evaluator.LoadCode(scriptCode);

            string result = script.Run(scriptArgs);
            Console.WriteLine(result);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CSX error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
                Console.Error.WriteLine($"Inner stack trace:\n{ex.InnerException.StackTrace}");
            }
            return 1;
        }
    }

    public unsafe int UNSAFE(StringBuilder args)
    {
		int number = 42;
		int* ptr = &number;
		
		NativeStdout.WriteLine((string)"Value: " + number);
    NativeStdout.WriteLine((string)"Address: " + (long)ptr);
    NativeStdout.WriteLine((string)"Value via pointer: " + *ptr);
        
    *ptr = 100;
    NativeStdout.WriteLine((string)"New value: " + number);
    return 1;
    }

    // Variable function: %@TCCEVAL[command]
    // Returns the captured output of an arbitrary TCC command.
    public int f_TCCEVAL(StringBuilder args)
    {
        string command = args.ToString().Trim();
        if (string.IsNullOrEmpty(command))
            return 1;
        try
        {
            string result = TakeCommand.PluginHost.InvokeCommand(command);
            args.Clear();
            args.Append(result.TrimEnd('\r', '\n'));
            return 0;
        }
        catch (InvalidOperationException)
        {
            args.Clear();
            return 1;
        }
    }

		internal static class NativeStdout
		{
				private const int STD_OUTPUT_HANDLE = -11;

				[DllImport("kernel32.dll", SetLastError = true)]
				private static extern IntPtr GetStdHandle(int nStdHandle);

				[DllImport("kernel32.dll", SetLastError = true)]
				[return: MarshalAs(UnmanagedType.Bool)]
				private static extern bool WriteFile(
						IntPtr hFile,
						byte[] buffer,
						uint bytesToWrite,
						out uint bytesWritten,
						IntPtr overlapped);

				public static void WriteLine(string text)
				{
						// Choose an encoding that matches the output encoding expected by TCC.
						byte[] bytes = Encoding.UTF8.GetBytes(text + "\r\n");
						IntPtr stdout = GetStdHandle(STD_OUTPUT_HANDLE);

						if (stdout == IntPtr.Zero || stdout == new IntPtr(-1))
								throw new InvalidOperationException("Standard output handle is unavailable.");

						if (!WriteFile(stdout, bytes, (uint)bytes.Length, out uint written, IntPtr.Zero)
								|| written != bytes.Length)
						{
								throw new System.ComponentModel.Win32Exception(
										Marshal.GetLastWin32Error());
						}
				}
		}
}
