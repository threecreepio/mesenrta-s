using Mesen.GUI.Config;
using Mesen.GUI.Debugger.Controls;
using Mesen.GUI.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Mesen.GUI.Debugger.RegEntry;

namespace Mesen.GUI.Debugger
{
	public partial class frmRegisterViewer : BaseForm, IRefresh
	{
		private NotificationListener _notifListener;

		private WindowRefreshManager _refreshManager;
		private DebugState _state;
		private byte _reg4210;
		private byte _reg4211;
		private byte _reg4212;

		private CoprocessorType _coprocessorType;
		private TabPage tpgCoprocessor;
		private ctrlPropertyList ctrlCoprocessor;

		public ctrlScanlineCycleSelect ScanlineCycleSelect { get { return this.ctrlScanlineCycleSelect; } }

		public frmRegisterViewer()
		{
			InitializeComponent();
		}

		protected override void OnLoad(EventArgs evt)
		{
			base.OnLoad(evt);
			if(DesignMode) {
				return;
			}

			_notifListener = new NotificationListener();
			_notifListener.OnNotification += OnNotificationReceived;

			InitShortcuts();

			RegisterViewerConfig config = ConfigManager.Config.Debug.RegisterViewer;
			RestoreLocation(config.WindowLocation, config.WindowSize);

			mnuAutoRefresh.Checked = config.AutoRefresh;
			ctrlScanlineCycleSelect.Initialize(config.RefreshScanline, config.RefreshCycle);

			_refreshManager = new WindowRefreshManager(this);
			_refreshManager.AutoRefresh = config.AutoRefresh;
			_refreshManager.AutoRefreshSpeed = RefreshSpeed.High;

			UpdateTabs();

			RefreshData();
			RefreshViewer();

			mnuAutoRefresh.CheckedChanged += mnuAutoRefresh_CheckedChanged;
		}

		private void InitShortcuts()
		{
			mnuRefresh.InitShortcut(this, nameof(DebuggerShortcutsConfig.Refresh));
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			_notifListener?.Dispose();
			_refreshManager?.Dispose();

			RegisterViewerConfig config = ConfigManager.Config.Debug.RegisterViewer;
			config.WindowSize = this.WindowState != FormWindowState.Normal ? this.RestoreBounds.Size : this.Size;
			config.WindowLocation = this.WindowState != FormWindowState.Normal ? this.RestoreBounds.Location : this.Location;
			config.AutoRefresh = mnuAutoRefresh.Checked;
			config.RefreshScanline = ctrlScanlineCycleSelect.Scanline;
			config.RefreshCycle = ctrlScanlineCycleSelect.Cycle;
			ConfigManager.ApplyChanges();

			base.OnFormClosed(e);
		}

		private void UpdateTabs()
		{
			if(tpgCoprocessor != null) {
				tabMain.TabPages.Remove(tpgCoprocessor);
			}

			_coprocessorType = EmuApi.GetRomInfo().CoprocessorType;
			if(_coprocessorType == CoprocessorType.SA1) {
				tpgCoprocessor = new TabPage();
				tpgCoprocessor.Text = "SA-1";
				ctrlCoprocessor = new ctrlPropertyList();
				ctrlCoprocessor.Dock = DockStyle.Fill;
				tpgCoprocessor.Controls.Add(ctrlCoprocessor);
				tabMain.TabPages.Add(tpgCoprocessor);
			}
		}

		private void OnNotificationReceived(NotificationEventArgs e)
		{
			switch(e.NotificationType) {
				case ConsoleNotificationType.GameLoaded:
					this.BeginInvoke((Action)(() => UpdateTabs()));
					break;

				case ConsoleNotificationType.CodeBreak:
					RefreshData();
					this.BeginInvoke((Action)(() => {
						this.RefreshViewer();
					}));
					break;
			}
		}
		
		public void RefreshData()
		{
			_state = DebugApi.GetState();
			_reg4210 = DebugApi.GetMemoryValue(SnesMemoryType.CpuMemory, 0x4210);
			_reg4211 = DebugApi.GetMemoryValue(SnesMemoryType.CpuMemory, 0x4211);
			_reg4212 = DebugApi.GetMemoryValue(SnesMemoryType.CpuMemory, 0x4212);
		}

		public void RefreshViewer()
		{
			if(tabMain.SelectedTab == tpgCpu) {
				InternalRegisterState regs = _state.InternalRegs;
				AluState alu = _state.Alu;
				ctrlPropertyCpu.UpdateState(new List<RegEntry>() {
					new RegEntry("$4200 - $4201", "IRQ/NMI/Autopoll Enabled", null),
					new RegEntry("$4200.7", "NMI Enabled", regs.EnableNmi),
					new RegEntry("$4200.5", "V IRQ Enabled", regs.EnableVerticalIrq),
					new RegEntry("$4200.4", "H IRQ Enabled", regs.EnableHorizontalIrq),
					new RegEntry("$4200.1", "Auto Joypad Poll", regs.EnableAutoJoypadRead),

					new RegEntry("$4201", "IO Port", regs.IoPortOutput, Format.X8),

					new RegEntry("$4202 - $4206", "Mult/Div Registers (Input)", null),
					new RegEntry("$4202", "Multiplicand", alu.MultOperand1, Format.X8),
					new RegEntry("$4203", "Multiplier", alu.MultOperand2, Format.X8),
					new RegEntry("$4204/5", "Dividend", alu.Dividend, Format.X16),
					new RegEntry("$4206", "Divisor", alu.Divisor, Format.X8),

					new RegEntry("$4207 - $420A", "H/V IRQ Timers", null),
					new RegEntry("$4207/8", "H Timer", regs.HorizontalTimer, Format.X16),
					new RegEntry("$4209/A", "V Timer", regs.VerticalTimer, Format.X16),

					new RegEntry("$4207 - $420A", "Misc. Flags", null),

					new RegEntry("$420D", "FastROM Enabled", regs.EnableFastRom),
					new RegEntry("$4210", "NMI Flag", (_reg4210 & 0x80) != 0),
					new RegEntry("$4211", "IRQ Flag", (_reg4211 & 0x80) != 0),

					new RegEntry("$4212.x", "V-Blank Flag", (_reg4212 & 0x80) != 0),
					new RegEntry("$4212.x", "H-Blank Flag", (_reg4212 & 0x40) != 0),
					new RegEntry("$4212.x", "Auto Joypad Read", (_reg4212 & 0x01) != 0),

					new RegEntry("$4214 - $4217", "Mult/Div Registers (Result)", null),
					new RegEntry("$4214/5", "Quotient", alu.DivResult, Format.X16),
					new RegEntry("$4216/7", "Product / Remainder", alu.MultOrRemainderResult, Format.X16),

					new RegEntry("$4218 - $421F", "Input Data", null),
					new RegEntry("$4218/9", "P1 Data", regs.ControllerData[0], Format.X16),
					new RegEntry("$421A/B", "P2 Data", regs.ControllerData[1], Format.X16),
					new RegEntry("$421C/D", "P3 Data", regs.ControllerData[2], Format.X16),
					new RegEntry("$421E/F", "P4 Data", regs.ControllerData[3], Format.X16),

				});
			} else if(tabMain.SelectedTab == tpgDma) {
				List<RegEntry> entries = new List<RegEntry>();

				//TODO
				/*for(int i = 0; i < 8; i++) {
					entries.Add(new RegEntry("$420C." + i.ToString(), "HDMA Channel " + i.ToString() + " Enabled", _state.DmaChannels[i].DmaActive));
				}*/

				for(int i = 0; i < 8; i++) {
					DmaChannelConfig ch = _state.DmaChannels[i];
					entries.Add(new RegEntry("DMA Channel " + i.ToString(), "", null));
					entries.Add(new RegEntry("$420B." + i.ToString(), "Channel Enabled", _state.DmaChannels[i].DmaActive));
					entries.Add(new RegEntry("$43" + i.ToString() + "0.0-2", "Transfer Mode", ch.TransferMode, Format.D));
					entries.Add(new RegEntry("$43" + i.ToString() + "0.3", "Fixed", ch.FixedTransfer));
					entries.Add(new RegEntry("$43" + i.ToString() + "0.4", "Decrement", ch.Decrement));
					entries.Add(new RegEntry("$43" + i.ToString() + "0.6", "Indirect HDMA", ch.HdmaIndirectAddressing));
					entries.Add(new RegEntry("$43" + i.ToString() + "0.7", "Direction", ch.InvertDirection ? "B -> A" : "A -> B"));

					entries.Add(new RegEntry("$43" + i.ToString() + "1", "B Bus Address", ch.DestAddress, Format.X8));
					entries.Add(new RegEntry("$43" + i.ToString() + "2/3/4", "A Bus Address", ((ch.SrcBank << 16) | ch.SrcAddress), Format.X24));
					entries.Add(new RegEntry("$43" + i.ToString() + "5/6", "Size", ch.TransferSize, Format.X16));
					entries.Add(new RegEntry("$43" + i.ToString() + "7", "HDMA Bank", ch.HdmaBank, Format.X8));
					entries.Add(new RegEntry("$43" + i.ToString() + "8/9", "HDMA Address", ch.HdmaTableAddress, Format.X16));
					entries.Add(new RegEntry("$43" + i.ToString() + "A", "HDMA Line Counter", ch.HdmaLineCounterAndRepeat, Format.X8));
				}
				ctrlPropertyDma.UpdateState(entries);
			} else if(tabMain.SelectedTab == tpgSpc) {
				SpcState spc = _state.Spc;
				ctrlPropertySpc.UpdateState(new List<RegEntry>() {
					new RegEntry("$F0", "Test", null),
					new RegEntry("$F0.0", "Timers Disabled", spc.TimersDisabled),
					new RegEntry("$F0.1", "RAM Write Enabled", spc.WriteEnabled),
					new RegEntry("$F0.3", "Timers Enabled", spc.TimersEnabled),
					new RegEntry("$F0.4-5", "External Speed", spc.ExternalSpeed, Format.D),
					new RegEntry("$F0.6-7", "Internal Speed", spc.InternalSpeed, Format.D),

					new RegEntry("$F1", "Control", null),
					new RegEntry("$F1.0", "Timer 0 Enabled", spc.Timer0.Enabled),
					new RegEntry("$F1.1", "Timer 1 Enabled", spc.Timer1.Enabled),
					new RegEntry("$F1.2", "Timer 2 Enabled", spc.Timer2.Enabled),
					new RegEntry("$F1.7", "IPL ROM Enabled", spc.RomEnabled),

					new RegEntry("$F2", "DSP", null),
					new RegEntry("$F2", "DSP Register", spc.DspReg, Format.X8),

					new RegEntry("$F4 - $F7", "CPU<->SPC Ports", null),
					new RegEntry("$F4", "Port 0 (CPU read)", spc.OutputReg[0], Format.X8),
					new RegEntry("$F4", "Port 0 (SPC read)", spc.CpuRegs[0], Format.X8),
					new RegEntry("$F5", "Port 1 (CPU read)", spc.OutputReg[1], Format.X8),
					new RegEntry("$F5", "Port 1 (SPC read)", spc.CpuRegs[1], Format.X8),
					new RegEntry("$F6", "Port 2 (CPU read)", spc.OutputReg[2], Format.X8),
					new RegEntry("$F6", "Port 2 (SPC read)", spc.CpuRegs[2], Format.X8),
					new RegEntry("$F7", "Port 3 (CPU read)", spc.OutputReg[3], Format.X8),
					new RegEntry("$F7", "Port 3 (SPC read)", spc.CpuRegs[3], Format.X8),

					new RegEntry("$F8 - $F9", "RAM Registers", null),
					new RegEntry("$F8", "RAM Reg 0", spc.RamReg[0], Format.X8),
					new RegEntry("$F9", "RAM Reg 1", spc.RamReg[1], Format.X8),

					new RegEntry("$FA - $FF", "Timers", null),
					new RegEntry("$FA", "Timer 0 Divider", spc.Timer0.Target, Format.X8),
					new RegEntry("$FA", "Timer 0 Frequency", GetTimerFrequency(8000, spc.Timer0.Target)),
					new RegEntry("$FB", "Timer 1 Divider", spc.Timer1.Target, Format.X8),
					new RegEntry("$FB", "Timer 1 Frequency", GetTimerFrequency(8000, spc.Timer1.Target)),
					new RegEntry("$FC", "Timer 2 Divider", spc.Timer2.Target, Format.X8),
					new RegEntry("$FC", "Timer 2 Frequency", GetTimerFrequency(64000, spc.Timer2.Target)),

					new RegEntry("$FD", "Timer 0 Output", spc.Timer0.Output, Format.X8),
					new RegEntry("$FE", "Timer 1 Output", spc.Timer1.Output, Format.X8),
					new RegEntry("$FF", "Timer 2 Output", spc.Timer2.Output, Format.X8),
				});
			} else if(tabMain.SelectedTab == tpgDsp) {
				DspState dsp = _state.Dsp;
				List<RegEntry> entries = new List<RegEntry>();

				Action<int, string> addReg = (int i, string name) => {
					entries.Add(new RegEntry("$" + i.ToString("X2"), name, dsp.Regs[i], Format.X8));
				};

				addReg(0x0C, "Main Volume (MVOL) - Left");
				addReg(0x1C, "Main Volume (MVOL) - Right");
				addReg(0x2C, "Echo Volume (EVOL) - Left");
				addReg(0x3C, "Echo Volume (EVOL) - Right");

				addReg(0x4C, "Key On (KON)");
				addReg(0x5C, "Key Off (KOF)");

				addReg(0x7C, "Source End Block (ENDX)");
				addReg(0x0D, "Echo Feedback (EFB)");
				addReg(0x2D, "Pitch Modulation (PMON)");
				addReg(0x3D, "Noise Enable (NON)");
				addReg(0x4D, "Echo Enable (EON)");
				addReg(0x5D, "Source Directory (Offset) (DIR)");
				addReg(0x6D, "Echo Buffer (Offset) (ESA)");
				addReg(0x6D, "Echo Delay (EDL)");

				entries.Add(new RegEntry("$6C", "Flags (FLG)", null));
				entries.Add(new RegEntry("$6C.0-4", "Noise Clock", dsp.Regs[0x6C] & 0x1F, Format.X8));
				entries.Add(new RegEntry("$6C.5", "Echo Disabled", (dsp.Regs[0x6C] & 0x20) != 0));
				entries.Add(new RegEntry("$6C.6", "Mute", (dsp.Regs[0x6C] & 0x40) != 0));
				entries.Add(new RegEntry("$6C.7", "Reset", (dsp.Regs[0x6C] & 0x80) != 0));

				entries.Add(new RegEntry("$xF", "Coefficients", null));
				for(int i = 0; i < 8; i++) {
					addReg((i << 4) | 0x0F, "Coefficient " + i);
				}

				for(int i = 0; i < 8; i++) {
					entries.Add(new RegEntry("Voice #" + i.ToString(), "", null));

					int voice = i << 4;
					addReg(voice | 0x00, "Left Volume (VOL)");
					addReg(voice | 0x01, "Right Volume (VOL)");
					entries.Add(new RegEntry("$" + i + "2 + $" + i + "3", "Pitch (P)", dsp.Regs[voice | 0x02] | (dsp.Regs[voice | 0x03] << 8), Format.X16));
					addReg(voice | 0x04, "Source (SRCN)");
					addReg(voice | 0x05, "ADSR1");
					addReg(voice | 0x06, "ADSR2");
					addReg(voice | 0x07, "GAIN");
					addReg(voice | 0x08, "ENVX");
					addReg(voice | 0x09, "OUTX");
				}
				ctrlPropertyDsp.UpdateState(entries);
			} else if(tabMain.SelectedTab == tpgPpu) {
				PpuState ppu = _state.Ppu;

				ctrlPropertyPpu.UpdateState(new List<RegEntry>() {
					new RegEntry("$2100", "Brightness", null),
					new RegEntry("$2100.0", "Forced Blank", ppu.ForcedVblank),
					new RegEntry("$2100.4-7", "Brightness", ppu.ScreenBrightness),
					new RegEntry("$2101", "OAM Settings", null),
					new RegEntry("$2100.0-2", "OAM Table Address", ppu.OamBaseAddress, Format.X16),
					new RegEntry("$2100.3-4", "OAM Second Table Address", (ppu.OamBaseAddress + ppu.OamAddressOffset) & 0x7FFF, Format.X16),
					new RegEntry("$2101.5-7", "OAM Size Mode", ppu.OamMode),
					new RegEntry("$2102-2103", "OAM Base Address", ppu.OamRamAddress),
					new RegEntry("$2103.7", "OAM Priority", ppu.EnableOamPriority),

					new RegEntry("$2105", "BG Mode/Size", null),
					new RegEntry("$2105.0-2", "BG Mode", ppu.BgMode),
					new RegEntry("$2105.3", "Mode 1 BG3 Priority", ppu.Mode1Bg3Priority),
					new RegEntry("$2105.4", "BG1 16x16 Tiles", ppu.Layers[0].LargeTiles),
					new RegEntry("$2105.5", "BG2 16x16 Tiles", ppu.Layers[1].LargeTiles),
					new RegEntry("$2105.6", "BG3 16x16 Tiles", ppu.Layers[2].LargeTiles),
					new RegEntry("$2105.7", "BG4 16x16 Tiles", ppu.Layers[3].LargeTiles),

					new RegEntry("$2106", "Mosaic", null),
					new RegEntry("$2106.0", "BG1 Mosaic Enabled", (ppu.MosaicEnabled & 0x01) != 0),
					new RegEntry("$2106.1", "BG2 Mosaic Enabled", (ppu.MosaicEnabled & 0x02) != 0),
					new RegEntry("$2106.2", "BG3 Mosaic Enabled", (ppu.MosaicEnabled & 0x04) != 0),
					new RegEntry("$2106.3", "BG4 Mosaic Enabled", (ppu.MosaicEnabled & 0x08) != 0),
					new RegEntry("$2106.4-7", "Mosaic Size", (ppu.MosaicSize - 1).ToString() + " (" + ppu.MosaicSize.ToString() + "x" + ppu.MosaicSize.ToString() + ")"),

					new RegEntry("$2107 - $210A", "Tilemap Addresses/Sizes", null),
					new RegEntry("$2107.0-1", "BG1 Size", GetLayerSize(ppu.Layers[0])),
					new RegEntry("$2107.2-6", "BG1 Address", ppu.Layers[0].TilemapAddress, Format.X16),
					new RegEntry("$2108.0-1", "BG2 Size", GetLayerSize(ppu.Layers[1])),
					new RegEntry("$2108.2-6", "BG2 Address", ppu.Layers[1].TilemapAddress, Format.X16),
					new RegEntry("$2109.0-1", "BG3 Size", GetLayerSize(ppu.Layers[2])),
					new RegEntry("$2109.2-6", "BG3 Address", ppu.Layers[2].TilemapAddress, Format.X16),
					new RegEntry("$210A.0-1", "BG4 Size", GetLayerSize(ppu.Layers[3])),
					new RegEntry("$210A.2-6", "BG4 Address", ppu.Layers[3].TilemapAddress, Format.X16),

					new RegEntry("$210B - $210C", "Tile Addresses", null),
					new RegEntry("$210B.0-2", "BG1 Tile Address", ppu.Layers[0].ChrAddress, Format.X16),
					new RegEntry("$210B.4-6", "BG2 Tile Address", ppu.Layers[1].ChrAddress, Format.X16),
					new RegEntry("$210C.0-2", "BG3 Tile Address", ppu.Layers[2].ChrAddress, Format.X16),
					new RegEntry("$210C.4-6", "BG4 Tile Address", ppu.Layers[3].ChrAddress, Format.X16),

					new RegEntry("$210D - $2114", "H/V Scroll Offsets", null),
					new RegEntry("$210D", "BG1 H Offset", ppu.Layers[0].HScroll, Format.X16),
					new RegEntry("$210D", "Mode7 H Offset", ppu.Mode7.HScroll, Format.X16),
					new RegEntry("$210E", "BG1 V Offset", ppu.Layers[0].VScroll, Format.X16),
					new RegEntry("$210E", "Mode7 V Offset", ppu.Mode7.VScroll, Format.X16),

					new RegEntry("$210F", "BG2 H Offset", ppu.Layers[1].HScroll, Format.X16),
					new RegEntry("$2110", "BG2 V Offset", ppu.Layers[1].VScroll, Format.X16),
					new RegEntry("$2111", "BG3 H Offset", ppu.Layers[2].HScroll, Format.X16),
					new RegEntry("$2112", "BG3 V Offset", ppu.Layers[2].VScroll, Format.X16),
					new RegEntry("$2113", "BG4 H Offset", ppu.Layers[3].HScroll, Format.X16),
					new RegEntry("$2114", "BG4 V Offset", ppu.Layers[3].VScroll, Format.X16),

					new RegEntry("$2115 - $2117", "VRAM", null),
					new RegEntry("$2115.0-1", "Increment Value", ppu.VramIncrementValue),
					new RegEntry("$2115.2-3", "Address Mapping", ppu.VramAddressRemapping),
					new RegEntry("$2115.7", "Increment on $2119", ppu.VramAddrIncrementOnSecondReg),
					new RegEntry("$2116/7", "VRAM Address", ppu.VramAddress, Format.X16),

					new RegEntry("$211A - $2120", "Mode 7", null),
					new RegEntry("$211A.0", "Mode 7 - Hor. Mirroring", ppu.Mode7.HorizontalMirroring),
					new RegEntry("$211A.1", "Mode 7 - Vert. Mirroring", ppu.Mode7.VerticalMirroring),
					new RegEntry("$211A.6", "Mode 7 - Fill w/ Tile 0", ppu.Mode7.FillWithTile0),
					new RegEntry("$211A.7", "Mode 7 - Large Tilemap", ppu.Mode7.LargeMap),

					new RegEntry("$211B", "Mode 7 - Matrix A", ppu.Mode7.Matrix[0], Format.X16),
					new RegEntry("$211C", "Mode 7 - Matrix B", ppu.Mode7.Matrix[1], Format.X16),
					new RegEntry("$211D", "Mode 7 - Matrix C", ppu.Mode7.Matrix[2], Format.X16),
					new RegEntry("$211E", "Mode 7 - Matrix D", ppu.Mode7.Matrix[3], Format.X16),

					new RegEntry("$211F", "Mode 7 - Center X", ppu.Mode7.CenterX, Format.X16),
					new RegEntry("$2120", "Mode 7 - Center Y", ppu.Mode7.CenterY, Format.X16),

					new RegEntry("$2123 - $212B", "Windows", null),
					new RegEntry("", "BG1 Windows", null),
					new RegEntry("$2123.0", "BG1 Window 1 Inverted", ppu.Window[0].InvertedLayers[0] != 0),
					new RegEntry("$2123.1", "BG1 Window 1 Active", ppu.Window[0].ActiveLayers[0] != 0),
					new RegEntry("$2123.2", "BG1 Window 2 Inverted", ppu.Window[1].InvertedLayers[0] != 0),
					new RegEntry("$2123.3", "BG1 Window 2 Active", ppu.Window[1].ActiveLayers[0] != 0),

					new RegEntry("", "BG2 Windows", null),
					new RegEntry("$2123.4", "BG2 Window 1 Inverted", ppu.Window[0].InvertedLayers[1] != 0),
					new RegEntry("$2123.5", "BG2 Window 1 Active", ppu.Window[0].ActiveLayers[1] != 0),
					new RegEntry("$2123.6", "BG2 Window 2 Inverted", ppu.Window[1].InvertedLayers[1] != 0),
					new RegEntry("$2123.7", "BG2 Window 2 Active", ppu.Window[1].ActiveLayers[1] != 0),

					new RegEntry("", "BG3 Windows", null),
					new RegEntry("$2124.0", "BG3 Window 1 Inverted", ppu.Window[0].InvertedLayers[2] != 0),
					new RegEntry("$2124.1", "BG3 Window 1 Active", ppu.Window[0].ActiveLayers[2] != 0),
					new RegEntry("$2124.2", "BG3 Window 2 Inverted", ppu.Window[1].InvertedLayers[2] != 0),
					new RegEntry("$2124.3", "BG3 Window 2 Active", ppu.Window[1].ActiveLayers[2] != 0),

					new RegEntry("", "BG4 Windows", null),
					new RegEntry("$2124.4", "BG4 Window 1 Inverted", ppu.Window[0].InvertedLayers[3] != 0),
					new RegEntry("$2124.5", "BG4 Window 1 Active", ppu.Window[0].ActiveLayers[3] != 0),
					new RegEntry("$2124.6", "BG4 Window 2 Inverted", ppu.Window[1].InvertedLayers[3] != 0),
					new RegEntry("$2124.7", "BG4 Window 2 Active", ppu.Window[1].ActiveLayers[3] != 0),

					new RegEntry("", "OAM Windows", null),
					new RegEntry("$2125.0", "OAM Window 1 Inverted", ppu.Window[0].InvertedLayers[4] != 0),
					new RegEntry("$2125.1", "OAM Window 1 Active", ppu.Window[0].ActiveLayers[4] != 0),
					new RegEntry("$2125.2", "OAM Window 2 Inverted", ppu.Window[1].InvertedLayers[4] != 0),
					new RegEntry("$2125.3", "OAM Window 2 Active", ppu.Window[1].ActiveLayers[4] != 0),

					new RegEntry("", "Color Windows", null),
					new RegEntry("$2125.4", "Color Window 1 Inverted", ppu.Window[0].InvertedLayers[5] != 0),
					new RegEntry("$2125.5", "Color Window 1 Active", ppu.Window[0].ActiveLayers[5] != 0),
					new RegEntry("$2125.6", "Color Window 2 Inverted", ppu.Window[1].InvertedLayers[5] != 0),
					new RegEntry("$2125.7", "Color Window 2 Active", ppu.Window[1].ActiveLayers[5] != 0),

					new RegEntry("", "Window Position", null),
					new RegEntry("$2126", "Window 1 Left", ppu.Window[0].Left),
					new RegEntry("$2127", "Window 1 Right", ppu.Window[0].Right),
					new RegEntry("$2128", "Window 2 Left", ppu.Window[1].Left),
					new RegEntry("$2129", "Window 2 Right", ppu.Window[1].Right),

					new RegEntry("", "Window Masks", null),
					new RegEntry("$212A.0-1", "BG1 Window Mask", ppu.MaskLogic[0].ToString().ToUpper()),
					new RegEntry("$212A.2-3", "BG2 Window Mask", ppu.MaskLogic[1].ToString().ToUpper()),
					new RegEntry("$212A.4-5", "BG3 Window Mask", ppu.MaskLogic[2].ToString().ToUpper()),
					new RegEntry("$212A.6-7", "BG4 Window Mask", ppu.MaskLogic[3].ToString().ToUpper()),
					new RegEntry("$212B.6-7", "OAM Window Mask", ppu.MaskLogic[4].ToString().ToUpper()),
					new RegEntry("$212B.6-7", "Color Window Mask", ppu.MaskLogic[5].ToString().ToUpper()),

					new RegEntry("$212C", "Main Screen Layers", null),
					new RegEntry("$212C.0", "BG1 Enabled", (ppu.MainScreenLayers & 0x01) != 0),
					new RegEntry("$212C.1", "BG2 Enabled", (ppu.MainScreenLayers & 0x02) != 0),
					new RegEntry("$212C.2", "BG3 Enabled", (ppu.MainScreenLayers & 0x04) != 0),
					new RegEntry("$212C.3", "BG4 Enabled", (ppu.MainScreenLayers & 0x08) != 0),
					new RegEntry("$212C.4", "OAM Enabled", (ppu.MainScreenLayers & 0x10) != 0),

					new RegEntry("$212D", "Sub Screen Layers", null),
					new RegEntry("$212D.0", "BG1 Enabled", (ppu.SubScreenLayers & 0x01) != 0),
					new RegEntry("$212D.1", "BG2 Enabled", (ppu.SubScreenLayers & 0x02) != 0),
					new RegEntry("$212D.2", "BG3 Enabled", (ppu.SubScreenLayers & 0x04) != 0),
					new RegEntry("$212D.3", "BG4 Enabled", (ppu.SubScreenLayers & 0x08) != 0),
					new RegEntry("$212D.4", "OAM Enabled", (ppu.SubScreenLayers & 0x10) != 0),

					new RegEntry("$212E", "Main Screen Windows", null),
					new RegEntry("$212E.0", "BG1 Mainscreen Window Enabled", ppu.WindowMaskMain[0] != 0),
					new RegEntry("$212E.1", "BG2 Mainscreen Window Enabled", ppu.WindowMaskMain[1] != 0),
					new RegEntry("$212E.2", "BG3 Mainscreen Window Enabled", ppu.WindowMaskMain[2] != 0),
					new RegEntry("$212E.3", "BG4 Mainscreen Window Enabled", ppu.WindowMaskMain[3] != 0),
					new RegEntry("$212E.4", "OAM Mainscreen Window Enabled", ppu.WindowMaskMain[4] != 0),

					new RegEntry("$212F", "Sub Screen Windows", null),
					new RegEntry("$212F.0", "BG1 Subscreen Window Enabled", ppu.WindowMaskSub[0] != 0),
					new RegEntry("$212F.1", "BG2 Subscreen Window Enabled", ppu.WindowMaskSub[1] != 0),
					new RegEntry("$212F.2", "BG3 Subscreen Window Enabled", ppu.WindowMaskSub[2] != 0),
					new RegEntry("$212F.3", "BG4 Subscreen Window Enabled", ppu.WindowMaskSub[3] != 0),
					new RegEntry("$212F.4", "OAM Subscreen Window Enabled", ppu.WindowMaskSub[4] != 0),

					new RegEntry("$2130 - $2131", "Color Math", null),
					new RegEntry("$2130.0", "Direct Color Mode", ppu.DirectColorMode),
					new RegEntry("$2130.1", "CM - Add Subscreen", ppu.ColorMathAddSubscreen),
					new RegEntry("$2130.4-5", "CM - Prevent Mode", ppu.ColorMathPreventMode.ToString()),
					new RegEntry("$2130.6-7", "CM - Clip Mode", ppu.ColorMathClipMode.ToString()),

					new RegEntry("$2131.0", "CM - BG1 Enabled", (ppu.ColorMathEnabled & 0x01) != 0),
					new RegEntry("$2131.1", "CM - BG2 Enabled", (ppu.ColorMathEnabled & 0x02) != 0),
					new RegEntry("$2131.2", "CM - BG3 Enabled", (ppu.ColorMathEnabled & 0x04) != 0),
					new RegEntry("$2131.3", "CM - BG4 Enabled", (ppu.ColorMathEnabled & 0x08) != 0),
					new RegEntry("$2131.4", "CM - OAM Enabled", (ppu.ColorMathEnabled & 0x10) != 0),
					new RegEntry("$2131.5", "CM - Background Enabled", (ppu.ColorMathEnabled & 0x20) != 0),
					new RegEntry("$2131.6", "CM - Half Mode", ppu.ColorMathHalveResult),
					new RegEntry("$2131.7", "CM - Substract Mode", ppu.ColorMathSubstractMode),

					new RegEntry("$2132 - $2133", "Misc.", null),
					new RegEntry("$2132", "Fixed Color - BGR", ppu.FixedColor, Format.X16),

					new RegEntry("$2133.0", "Screen Interlace", ppu.ScreenInterlace),
					new RegEntry("$2133.1", "OAM Interlace", ppu.ObjInterlace),
					new RegEntry("$2133.2", "Overscan Mode", ppu.OverscanMode),
					new RegEntry("$2133.3", "High Resolution Mode", ppu.HiResMode),
					new RegEntry("$2133.4", "Ext. BG Enabled", ppu.ExtBgEnabled),
				});
			} else if(tabMain.SelectedTab == tpgCoprocessor) {
				if(_coprocessorType == CoprocessorType.SA1) {
					Sa1State sa1 = _state.Sa1.Sa1;

					List<RegEntry> entries = new List<RegEntry>() {
						new RegEntry("$2200", "SA-1 CPU Control", null),
						new RegEntry("$2200.0-3", "Message", sa1.Sa1MessageReceived, Format.X8),
						new RegEntry("$2200.4", "NMI Requested", sa1.Sa1NmiRequested),
						new RegEntry("$2200.5", "Reset", sa1.Sa1Reset),
						new RegEntry("$2200.6", "Wait", sa1.Sa1Wait),
						new RegEntry("$2200.7", "IRQ Requested", sa1.Sa1IrqRequested),

						new RegEntry("$2201", "S-CPU Interrupt Enable", null),
						new RegEntry("$2201.5", "Character Conversion IRQ Enable", sa1.CharConvIrqEnabled),
						new RegEntry("$2201.7", "IRQ Enabled", sa1.CpuIrqEnabled),

						new RegEntry("$2202", "S-CPU Interrupt Clear", null),
						new RegEntry("$2202.5", "Character IRQ Flag", sa1.CharConvIrqFlag),
						new RegEntry("$2202.7", "IRQ Flag", sa1.CpuIrqRequested),

						new RegEntry("$2203/4", "SA-1 Reset Vector", sa1.Sa1ResetVector, Format.X16),
						new RegEntry("$2205/6", "SA-1 NMI Vector", sa1.Sa1ResetVector, Format.X16),
						new RegEntry("$2207/8", "SA-1 IRQ Vector", sa1.Sa1ResetVector, Format.X16),

						new RegEntry("$2209", "S-CPU Control", null),
						new RegEntry("$2209.0-3", "Message", sa1.CpuMessageReceived, Format.X8),
						new RegEntry("$2209.4", "Use NMI Vector", sa1.UseCpuNmiVector),
						new RegEntry("$2209.6", "Use IRQ Vector", sa1.UseCpuIrqVector),
						new RegEntry("$2209.7", "IRQ Requested", sa1.CpuIrqRequested),

						new RegEntry("$220A", "SA-1 CPU Interrupt Enable", null),
						new RegEntry("$220A.4", "SA-1 NMI Enabled", sa1.Sa1NmiEnabled),
						new RegEntry("$220A.5", "DMA IRQ Enabled", sa1.DmaIrqEnabled),
						new RegEntry("$220A.6", "Timer IRQ Enabled", sa1.TimerIrqEnabled),
						new RegEntry("$220A.7", "SA-1 IRQ Enabled", sa1.Sa1IrqEnabled),

						new RegEntry("$220B", "S-CPU Interrupt Clear", null),
						new RegEntry("$220B.4", "SA-1 NMI Requested", sa1.Sa1NmiRequested),
						new RegEntry("$220B.5", "DMA IRQ Flag", sa1.DmaIrqFlag),
						new RegEntry("$220B.7", "SA-1 IRQ Requested", sa1.Sa1IrqRequested),

						new RegEntry("$220C/D", "S-CPU NMI Vector", sa1.CpuNmiVector, Format.X16),
						new RegEntry("$220E/F", "S-CPU IRQ Vector", sa1.CpuIrqVector, Format.X16),

						new RegEntry("$2210", "H/V Timer Control", null),
						new RegEntry("$2210.0", "Horizontal Timer Enabled", sa1.HorizontalTimerEnabled),
						new RegEntry("$2210.1", "Vertical Timer Enabled", sa1.VerticalTimerEnabled),
						new RegEntry("$2210.7", "Linear Timer", sa1.UseLinearTimer),

						new RegEntry("$2212/3", "H-Timer", sa1.HTimer, Format.X16),
						new RegEntry("$2214/5", "V-Timer", sa1.VTimer, Format.X16),

						new RegEntry("", "ROM/BWRAM/IRAM Mappings", null),
						new RegEntry("$2220", "MMC Bank C", sa1.Banks[0], Format.X8),
						new RegEntry("$2221", "MMC Bank D", sa1.Banks[1], Format.X8),
						new RegEntry("$2222", "MMC Bank E", sa1.Banks[2], Format.X8),
						new RegEntry("$2223", "MMC Bank F", sa1.Banks[3], Format.X8),

						new RegEntry("$2224", "S-CPU BW-RAM Bank", sa1.CpuBwBank, Format.X8),
						new RegEntry("$2225.0-6", "SA-1 CPU BW-RAM Bank", sa1.Sa1BwBank, Format.X8),
						new RegEntry("$2225.7", "SA-1 CPU BW-RAM Mode", sa1.Sa1BwMode, Format.X8),
						new RegEntry("$2226.7", "S-CPU BW-RAM Write Enabled", sa1.CpuBwWriteEnabled),
						new RegEntry("$2227.7", "SA-1 BW-RAM Write Enabled", sa1.Sa1BwWriteEnabled),
						new RegEntry("$2228.0-3", "S-CPU BW-RAM Write Protected Area", sa1.BwWriteProtectedArea, Format.X8),
						new RegEntry("$2229", "S-CPU I-RAM Write Protection", sa1.CpuIRamWriteProtect, Format.X8),
						new RegEntry("$222A", "SA-1 CPU I-RAM Write Protection", sa1.Sa1IRamWriteProtect, Format.X8),

						new RegEntry("$2230", "DMA Control", null),
						new RegEntry("$2230.0-1", "DMA Source Device", sa1.DmaSrcDevice.ToString()),
						new RegEntry("$2230.2-3", "DMA Destination Device", sa1.DmaDestDevice.ToString()),
						new RegEntry("$2230.4", "Automatic DMA Character Conversion", sa1.DmaCharConvAuto),
						new RegEntry("$2230.5", "DMA Character Conversion", sa1.DmaCharConv),
						new RegEntry("$2230.6", "DMA Priority", sa1.DmaPriority),
						new RegEntry("$2230.7", "DMA Enabled", sa1.DmaEnabled),

						new RegEntry("$2231.0-1", "Character Format (BPP)", sa1.CharConvBpp, Format.D),
						new RegEntry("$2231.2-5", "Character Conversion Width", sa1.CharConvWidth, Format.X8),
						new RegEntry("$2231.7", "Character DMA Active", sa1.CharConvDmaActive),

						new RegEntry("$2232/3/4", "DMA Source Address", sa1.DmaSrcAddr, Format.X24),
						new RegEntry("$2235/6/7", "DMA Destination Address", sa1.DmaDestAddr, Format.X24),

						new RegEntry("$2238/9", "DMA Size", sa1.DmaSize, Format.X16),
						new RegEntry("$223F.7", "BW-RAM 2 bpp mode", sa1.BwRam2BppMode)
					};

					entries.Add(new RegEntry("", "Bitmap Register File", null));
					for(int i = 0; i < 8; i++) {
						entries.Add(new RegEntry("$224" + i, "BRF #" + i, sa1.BitmapRegister1[i]));
					}
					for(int i = 0; i < 8; i++) {
						entries.Add(new RegEntry("$224" + (8 + i).ToString("X"), "BRF #" + (i+8), sa1.BitmapRegister2[i]));
					}					

					entries.AddRange(new List<RegEntry>() {
						new RegEntry("", "Math Registers", null),
						new RegEntry("$2250.0-1", "Math Operation", sa1.MathOp.ToString()),
						new RegEntry("$2251/2", "Multiplicand/Dividend", sa1.MultiplicandDividend, Format.X16),
						new RegEntry("$2253/4", "Multiplier/Divisor", sa1.MultiplierDivisor, Format.X16),

						new RegEntry("", "Variable Length Registers", null),
						new RegEntry("$2258", "Variable Length Bit Processing", null),
						new RegEntry("$2258.0-3", "Variable Length Bit Count", sa1.VarLenBitCount, Format.X8),
						new RegEntry("$2258.7", "Variable Length Auto-Increment", sa1.VarLenAutoInc),
						new RegEntry("$2259/A/B", "Variable Length Address", sa1.VarLenAddress, Format.X24),

						new RegEntry("$2300", "S-CPU Status Flags", null),
						new RegEntry("$2300.0-3", "Message Received", sa1.CpuMessageReceived, Format.X8),
						new RegEntry("$2300.4", "Use NMI Vector", sa1.UseCpuNmiVector),
						new RegEntry("$2300.5", "Character Conversion IRQ Flag", sa1.CharConvIrqFlag),
						new RegEntry("$2300.6", "Use IRQ Vector", sa1.UseCpuIrqVector),
						new RegEntry("$2300.7", "IRQ Requested", sa1.CpuIrqRequested),

						new RegEntry("$2301", "SA-1 Status Flags", null),
						new RegEntry("$2301.0-3", "Message Received", sa1.Sa1MessageReceived, Format.X8),
						new RegEntry("$2301.4", "NMI Requested", sa1.Sa1NmiRequested),
						new RegEntry("$2301.5", "DMA IRQ Flag", sa1.DmaIrqFlag),
						new RegEntry("$2301.7", "IRQ Requested", sa1.Sa1IrqRequested),
						
						new RegEntry("$2302/3", "SA-1 H-Counter", 0, Format.X16),
						new RegEntry("$2304/5", "SA-1 V-Counter", 0, Format.X16),

						new RegEntry("$2306/7/8/9/A", "Math Result", sa1.MathOpResult),
						new RegEntry("$230B.7", "Math Overflow", sa1.MathOverflow)
					});

					ctrlCoprocessor.UpdateState(entries);
				}
			}
		}

		private string GetTimerFrequency(double baseFreq, int divider)
		{
			return (divider == 0 ? (baseFreq / 256) : (baseFreq / divider)).ToString(".00") + " Hz";
		}

		private string GetLayerSize(LayerConfig layer)
		{
			return (layer.DoubleWidth ? "64" : "32") + "x" + (layer.DoubleHeight ? "64" : "32");
		}

		private void mnuRefresh_Click(object sender, EventArgs e)
		{
			RefreshData();
			RefreshViewer();
		}
		
		private void mnuClose_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void mnuAutoRefresh_CheckedChanged(object sender, EventArgs e)
		{
			_refreshManager.AutoRefresh = mnuAutoRefresh.Checked;
		}

		private void tabMain_SelectedIndexChanged(object sender, EventArgs e)
		{
			RefreshViewer();
		}
	}
}
