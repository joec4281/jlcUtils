// dotnet build jlcutils.csproj -c Release
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
        Functions   = "CSTEST,CSX,DIRCOUNT,UNSAFE,@TCCEVAL",
        Major       = 1, Minor = 0, Build = 1
    };
    public TCCPluginInfo GetPluginInfo() => _info;
    public bool Initialize()            => true;
    public bool Shutdown(bool end)       => true;
 
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

            Console.WriteLine(script.Hello());
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
		
		Console.WriteLine("Value: " + number);
        Console.WriteLine("Address: " + (long)ptr);
        Console.WriteLine("Value via pointer: " + *ptr);
        
        *ptr = 100;
        Console.WriteLine("New value: " + number);
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
}
