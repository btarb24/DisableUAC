using System;
using System.ServiceProcess;
using System.Timers;

namespace DisableUAC
{
  using Microsoft.Win32;
  using System.Configuration;
  using System.Linq;

  public partial class DisableUAC : ServiceBase
	{
		private Timer regTimer;
		private double DefaultTimerInterval = TimeSpan.FromSeconds(30).TotalMilliseconds;

		public DisableUAC()
		{
			InitializeComponent();
		}

    private double GetTimerInterval()
    {
      var configValue = ConfigurationManager.AppSettings[@"TimerIntervalInSeconds"];
      if (string.IsNullOrWhiteSpace(configValue))
      {
        Logging.WriteErrorLog("Timer Interval Configuration Invalid!");
        return DefaultTimerInterval;
      }
      
      if (!double.TryParse(configValue, out var parseValue))
      {
        Logging.WriteErrorLog("Timer Interval Configuration Invalid!");
        return DefaultTimerInterval;
      }
      parseValue *= 1000;

      return parseValue;
    }

		protected override void OnStart(string[] args)
		{
      var interval = GetTimerInterval();
      regTimer = new Timer { Interval = interval };
			regTimer.Elapsed += regTimer_Tick;
			regTimer.Enabled = true;
			Logging.WriteErrorLog("Service has started.");
		}
    
		protected override void OnStop()
		{
			regTimer.Stop();
			Logging.WriteErrorLog("Service has stopped.");
		}

		private void regTimer_Tick(object sender, ElapsedEventArgs e)
		{
			try
			{
				WriteUACRegistryValue();
			}
			catch (Exception ex)
			{
				Logging.WriteErrorLog(ex);
			}
		}

    private void WriteUACRegistryValue()
    {
      //taken from http://blog.pythonaro.com/2013/05/fully-disable-user-access-control-uac.html
      Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "EnableLUA", 0);
      Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "ConsentPromptBehaviorAdmin", 0);
      Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "PromptOnSecureDesktop", 0);
      Registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Action Center\\Checks\\{C8E6F269-B90A-4053-A3BE-499AFCEC98C4}.check.0", "CheckSetting", StringToByteArray("23004100430042006C006F00620000000000000000000000010000000000000000000000"), RegistryValueKind.Binary);
    }

    private byte[] StringToByteArray(string hex)
    {
      return Enumerable.Range(0, hex.Length)
               .Where(x => x % 2 == 0)
               .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
               .ToArray();
    }
  }
}
