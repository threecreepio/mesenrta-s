using Mesen.GUI.Config;
using Mesen.GUI.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Mesen.GUI.Updates
{
	public static class UpdateHelper
	{
		public static bool PerformUpgrade()
		{
			Version newVersion = EmuApi.GetMesenVersion();
			Version oldVersion = new Version(ConfigManager.Config.Version);
			if(oldVersion < newVersion) {
				//Upgrade
				if(oldVersion <= new Version("0.1.0")) {
					ConfigManager.Config.Audio.MasterVolume = 100;
				}

				ConfigManager.Config.Version = EmuApi.GetMesenVersion().ToString();
				ConfigManager.ApplyChanges();

				return true;
			}
			return false;
		}

		public static void CheckForUpdates(bool silent)
		{
			Task.Run(() => {
				try {
					using(var client = new WebClient())
					{
						Version currentVersion = EmuApi.GetMesenVersion();
						client.Headers.Add("User-Agent", "Mesen Updater");
						var response = client.DownloadString("https://api.github.com/repos/threecreepio/mesenrta-s/releases/latest");
						dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
						var latestVersion = new Version((string)obj.name);
						var changeLog = ((string)obj.body) ?? "";
						var downloadUrl = ((Newtonsoft.Json.Linq.JArray)obj.assets)
							.Select(asset => asset["browser_download_url"])
							.Where(url => url != null && url.ToString().EndsWith("-win.zip"))
							.Select(url => url.ToString())
							.FirstOrDefault();

						if (latestVersion > currentVersion) {
							frmMain.Instance.BeginInvoke((MethodInvoker)(() => {
								using(frmUpdatePrompt frmUpdate = new frmUpdatePrompt(currentVersion, latestVersion, changeLog, downloadUrl)) {
									if(frmUpdate.ShowDialog(null, frmMain.Instance) == DialogResult.OK) {
										frmMain.Instance.Close();
									}
								}
							}));
						} else if(!silent) {
							MesenMsgBox.Show("MesenUpToDate", MessageBoxButtons.OK, MessageBoxIcon.Information);
						}
					}
				} catch(Exception ex) {
					if(!silent) {
						MesenMsgBox.Show("ErrorWhileCheckingUpdates", MessageBoxButtons.OK, MessageBoxIcon.Error, ex.ToString());
					}
				}
			});
		}
	}
}
