using System;
using System.Globalization;

public class Script 
{
    public string Run(string[] args)
    {
        if (args == null || args.Length < 1)
            return "Usage: <program> <date-YYYY-MM-DD>";

        try
        {
            var inputDate = DateTime.ParseExact(
                args[0],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            var yesterday = inputDate.Date.AddDays(-1);
            return yesterday.ToString("yyyy-MM-dd");
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }
}
