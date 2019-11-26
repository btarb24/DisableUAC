using System;
using System.IO;

namespace DisableUAC
{
  using System.Globalization;

  public static class Logging
	{
		public static void WriteErrorLog(Exception ex)
		{
			WriteErrorLog(ex.ToString());
		}

		public static void WriteErrorLog(string message)
		{
			try
			{
        using (var sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\DisableUAC.log", true))
        {
          sw.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": " + message);

          sw.Flush();
          sw.Close();
        }
			}
			catch 
			{ }
		}
	}
}