//css_co /platform:x64
//css_ref System.Data.dll

using System;

public class Script
{
    public string Run(string[] args)
    {
        if (args == null || args.Length == 0)
            return "Hello from plugin-safe script (no arguments)";

        // Join arguments safely
        string joined = string.Join(", ", args);
		return "Hello from plugin-safe script\nArguments: " + joined;
    }
}
