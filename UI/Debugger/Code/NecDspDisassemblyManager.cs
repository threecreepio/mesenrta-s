using Mesen.GUI.Debugger.Controls;
using Mesen.GUI.Debugger.Integration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mesen.GUI.Debugger.Code
{
	public class NecDspDisassemblyManager : CpuDisassemblyManager
	{
		public override CpuType CpuType { get { return CpuType.NecDsp; } }
		public override SnesMemoryType RelativeMemoryType { get { return SnesMemoryType.NecDspMemory; } }
		public override int AddressSize { get { return 4; } }
		public override int ByteCodeSize { get { return 3; } }
		public override bool AllowSourceView { get { return false; } }

		public override void RefreshCode(ISymbolProvider symbolProvider, SourceFileInfo file)
		{
			this._provider = new CodeDataProvider(CpuType.NecDsp);
		}

		protected override int GetFullAddress(int address, int length)
		{
			return address;
		}
	}
}
