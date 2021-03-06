using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mesen.GUI.Config.Shortcuts
{
	public struct KeyCombination
	{
		public UInt32 Key1;
		public UInt32 Key2;
		public UInt32 Key3;

		public bool IsEmpty { get { return Key1 == 0 && Key2 == 0 && Key3 == 0; } }

		public override string ToString()
		{
			if(IsEmpty) {
				return "";
			} else {
				return GetKeyNames();
			}
		}

		public KeyCombination(List<UInt32> scanCodes = null)
		{
			if(scanCodes != null) {
				if(scanCodes.Any(code => code > 0xFFFF)) {
					//If both keyboard & gamepad codes exist, only use the gamepad codes
					//This fixes an issue with Steam where Steam can remap gamepad buttons to send keyboard keys
					//See: Settings -> Controller Settings -> General Controller Settings -> Checking the Xbox/PS4/Generic/etc controller checkboxes will cause this
					scanCodes = scanCodes.Where(code => code > 0xFFFF).ToList();
				}

				Key1 = scanCodes.Count > 0 ? scanCodes[0] : 0;
				Key2 = scanCodes.Count > 1 ? scanCodes[1] : 0;
				Key3 = scanCodes.Count > 2 ? scanCodes[2] : 0;
			} else {
				Key1 = 0;
				Key2 = 0;
				Key3 = 0;
			}
		}

		private string GetKeyNames()
		{
			List<UInt32> scanCodes = new List<uint>() { Key1, Key2, Key3 };
			List<string> keyNames = scanCodes.Select((UInt32 scanCode) => InputApi.GetKeyName(scanCode)).Where((keyName) => !string.IsNullOrWhiteSpace(keyName)).ToList();
			keyNames.Sort((string a, string b) => {
				if(a == b) {
					return 0;
				}

				if(a == "Ctrl") {
					return -1;
				} else if(b == "Ctrl") {
					return 1;
				}

				if(a == "Alt") {
					return -1;
				} else if(b == "Alt") {
					return 1;
				}

				if(a == "Shift") {
					return -1;
				} else if(b == "Shift") {
					return 1;
				}

				return a.CompareTo(b);
			});

			return string.Join("+", keyNames);
		}
	}
}
