using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO.Compression;

namespace MesenUpdater
{
	class Program
	{
		static void Main(string[] args)
		{
			if(args.Length > 1) {
				string srcFile = args[0];
				string destDir = args[1];
				string destFile = Path.Combine(destDir, "Mesen-S.exe");

				//Wait a bit for the application to shut down before trying to kill it
				System.Threading.Thread.Sleep(1000);
				try {
					foreach(Process process in Process.GetProcessesByName("Mesen-S")) {
						try {
							if(process.MainModule.FileName == destFile) {
								process.Kill();
							}
						} catch { }
					}
				} catch { }

				int retryCount = 0;
				while(retryCount < 10) {
					try {
						using(FileStream file = File.Open(destFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete | FileShare.ReadWrite)) { }
						break;
					} catch {
						retryCount++;
						System.Threading.Thread.Sleep(1000);
					}
				}

				try {
					using (var zip = ZipFile.OpenRead(srcFile))
					{
						foreach (var entry in zip.Entries)
						{
							entry.ExtractToFile(Path.Combine(destDir, entry.FullName), true);
						}
					}
				} catch {
					MessageBox.Show("Update failed. Please try downloading and installing the new version manually.", "Mesen-S", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				Process.Start(destFile);
			} else {
				MessageBox.Show("Please run Mesen-S directly to update.", "Mesen-S");
			}
		}
	}
}
