using System;
using System.ServiceProcess;
using System.Timers;

namespace DisableUAC
{
  using Microsoft.Win32;
  using System.Configuration;
  using System.Linq;
  using System.Security.Principal;

  public partial class DisableUAC : ServiceBase
	{
		private Timer _regTimer;
    private string _SID;
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
      _SID = GetMySID();

      var interval = GetTimerInterval();
      _regTimer = new Timer { Interval = interval };
      regTimer_Tick(null, null);
			_regTimer.Elapsed += regTimer_Tick;
			_regTimer.Enabled = true;
			Logging.WriteErrorLog("Service has started.");
		}
    
		protected override void OnStop()
		{
			_regTimer.Stop();
			Logging.WriteErrorLog("Service has stopped.");
		}

		private void regTimer_Tick(object sender, ElapsedEventArgs e)
		{
      if (_SID == null)
        _SID = GetMySID();

      TryWriteUACRegistryValue();
      TryDisableChromeRestrictions();
      TrySetUnsecureScreensaver();
    }

    private string GetMySID()
    {
      try
      {
        var account = new NTAccount(@"3MHEALTH\A9DX5ZZ");
        var loggedInSID = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
        var loggedInSIDStr = loggedInSID.ToString();

        Logging.WriteErrorLog($"SID: {loggedInSIDStr}");

        return loggedInSIDStr;
      }
      catch
      {
        return null;
      }
    }

    private bool TryWriteUACRegistryValue()
    {
      try
      {
        //taken from http://blog.pythonaro.com/2013/05/fully-disable-user-access-control-uac.html
        Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "EnableLUA", 0);
        Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "ConsentPromptBehaviorAdmin", 0);
        Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System", "PromptOnSecureDesktop", 0);
        //don't seem to need this
        //Registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Action Center\\Checks\\{C8E6F269-B90A-4053-A3BE-499AFCEC98C4}.check.0", "CheckSetting", StringToByteArray("23004100430042006C006F00620000000000000000000000010000000000000000000000"), RegistryValueKind.Binary);
        return true;
      }
      catch (Exception ex)
      {
        Logging.WriteErrorLog(ex);
        return false;
      }
    }

    private bool TryDisableChromeRestrictions()
    {
      try
      {
        DeleteRegistryKey(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Google");
        return true;
      }
      catch (Exception ex)
      {
        Logging.WriteErrorLog(ex);
        return false;
      }
    }

    private bool TrySetUnsecureScreensaver()
    {
      try
      {
        SetCurrentUserValueData(@"Software\Policies\Microsoft\Windows\Control Panel\Desktop", "ScreenSaverIsSecure", 0);
        return true;
      }
      catch (Exception ex)
      {
        Logging.WriteErrorLog(ex);
        return false;
      }
    }

    private byte[] StringToByteArray(string hex)
    {
      return Enumerable.Range(0, hex.Length)
               .Where(x => x % 2 == 0)
               .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
               .ToArray();
    }

    private void DeleteRegistryKey(RegistryHive registryHive, string fullPathKeyToDelete)
    {
      using (var baseKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry64))
      {
        using (var subKey = baseKey.OpenSubKey(fullPathKeyToDelete))
        {
          if (subKey != null)
            baseKey.DeleteSubKeyTree(fullPathKeyToDelete);
        }
      }
    }

    private void SetCurrentUserValueData(string fullKeyPath, string value, int data)
    {
      if (_SID == null)
        return;

      //HKCU is wrong when running as service.  Need to specifically write the the desired user in HKU
      using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64))
      {
        var path = $@"{_SID}\{fullKeyPath}";
        using (var subKey = baseKey.OpenSubKey(path, writable: true))
        {
          if (subKey != null)
            subKey.SetValue(value, data);
          else
            Logging.WriteErrorLog($@"not found: HKU\{path}");
        }
      }
    }
  }
}