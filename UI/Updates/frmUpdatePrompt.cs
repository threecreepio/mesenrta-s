using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Mesen.GUI.Config;
using Mesen.GUI.Controls;
using Mesen.GUI.Forms;

namespace Mesen.GUI.Updates
{
	public partial class frmUpdatePrompt : BaseForm
	{
		private string _downloadUrl;

		public frmUpdatePrompt(Version currentVersion, Version latestVersion, string changeLog, string downloadUrl)
		{
			InitializeComponent();

			this.txtChangelog.Font = new Font(BaseControl.MonospaceFontFamily, 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

			_downloadUrl = downloadUrl;

			lblCurrentVersionString.Text = currentVersion.ToString();
			lblLatestVersionString.Text = latestVersion.ToString();
			txtChangelog.Text = changeLog.Replace("\n", Environment.NewLine);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			this.lblDonate.Visible = false;
			this.picDonate.Visible = false;

			btnUpdate.Focus();
		}
		
		private void btnUpdate_Click(object sender, EventArgs e)
		{
#if DISABLEAUTOUPDATE
			MesenMsgBox.Show("AutoUpdateDisabledMessage", MessageBoxButtons.OK, MessageBoxIcon.Information);
			this.DialogResult = DialogResult.Cancel;
			this.Close();
#else
			string destFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
			string srcFilePath = Path.Combine(ConfigManager.DownloadFolder, "Mesen-S." + lblLatestVersionString.Text + ".zip");
			string updateHelper = Path.Combine(ConfigManager.HomeFolder, "Resources", "MesenUpdater.exe");

			if (!File.Exists(updateHelper))
			{
				MesenMsgBox.Show("UpdaterNotFound", MessageBoxButtons.OK, MessageBoxIcon.Error);
				DialogResult = DialogResult.Cancel;
			}
			else if (!string.IsNullOrWhiteSpace(srcFilePath))
			{
				frmDownloadProgress frmDownload = new frmDownloadProgress(_downloadUrl, srcFilePath);
				if (frmDownload.ShowDialog() == DialogResult.OK)
				{
					FileInfo fileInfo = new FileInfo(srcFilePath);
					if (fileInfo.Length > 0)
					{
						if (Program.IsMono)
						{
							Process.Start("mono", string.Format("\"{0}\" \"{1}\" \"{2}\"", updateHelper, srcFilePath, destFilePath));
						}
						else
						{
							Process.Start(updateHelper, string.Format("\"{0}\" \"{1}\"", srcFilePath, destFilePath));
						}
					}
					else
					{
						//Download failed, mismatching hashes
						MesenMsgBox.Show("UpdateDownloadFailed", MessageBoxButtons.OK, MessageBoxIcon.Error);
						DialogResult = DialogResult.Cancel;
					}
				}
			}
#endif
		}

		private void picDonate_Click(object sender, EventArgs e)
		{
			Process.Start("https://www.mesen.ca/Donate.php?l=" + ResourceHelper.GetLanguageCode());
		}
	}
}
