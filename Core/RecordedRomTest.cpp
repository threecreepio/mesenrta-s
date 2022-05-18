#include "stdafx.h"

#include "RecordedRomTest.h"
#include "Console.h"
#include "EmuSettings.h"
#include "MessageManager.h"
#include "Ppu.h"
#include "MovieManager.h"
#include "BaseCartridge.h"
#include "NotificationManager.h"
#include "../Utilities/VirtualFile.h"
#include "../Utilities/FolderUtilities.h"
#include "../Utilities/md5.h"
#include "../Utilities/ZipWriter.h"
#include "../Utilities/ZipReader.h"
#include "../Utilities/ArchiveReader.h"

RecordedRomTest::RecordedRomTest(shared_ptr<Console> console)
{
	if(console) {
		_console = console;
	} else {
		_console.reset(new Console());
		_console->Initialize();
	}
	Reset();
}

RecordedRomTest::~RecordedRomTest()
{
	Reset();
}

void RecordedRomTest::SaveFrame()
{
	bool highRes = _ppu->IsHighResOutput();
	uint16_t width = highRes ? 512 : 256;
	uint16_t height = highRes ? 478 : 239;
	uint16_t* ppuFrameBuffer = _ppu->GetScreenBuffer();

	uint8_t md5Hash[16];
	GetMd5Sum(md5Hash, ppuFrameBuffer, width*height*sizeof(uint16_t));

	if(memcmp(_previousHash, md5Hash, 16) == 0 && _currentCount < 255) {
		_currentCount++;
	} else {
		uint8_t* hash = new uint8_t[16];
		memcpy(hash, md5Hash, 16);
		_screenshotHashes.push_back(hash);
		if(_currentCount > 0) {
			_repetitionCount.push_back(_currentCount);
		}
		_currentCount = 1;

		memcpy(_previousHash, md5Hash, 16);

		_signal.Signal();
	}
}

void RecordedRomTest::ValidateFrame()
{
	bool highRes = _ppu->IsHighResOutput();
	uint16_t width = highRes ? 512 : 256;
	uint16_t height = highRes ? 478 : 239;
	uint16_t* ppuFrameBuffer = _ppu->GetScreenBuffer();

	uint8_t md5Hash[16];
	GetMd5Sum(md5Hash, ppuFrameBuffer, width * height * sizeof(uint16_t));

	if(_currentCount == 0) {
		_currentCount = _repetitionCount.front();
		_repetitionCount.pop_front();
		_screenshotHashes.pop_front();
	}
	_currentCount--;

	if(memcmp(_screenshotHashes.front(), md5Hash, 16) != 0) {
		_badFrameCount++;
		//_console->BreakIfDebugging();
	} 
	
	if(_currentCount == 0 && _repetitionCount.empty()) {
		//End of test
		_runningTest = false;
		_signal.Signal();
	}
}

void RecordedRomTest::ProcessNotification(ConsoleNotificationType type, void* parameter)
{
	switch(type) {
		case ConsoleNotificationType::PpuFrameDone:
			if(_recording) {
				SaveFrame();
			} else if(_runningTest) {
				ValidateFrame();
			}
			break;
		
		default:
			break;
	}
}

void RecordedRomTest::Reset()
{
	memset(_previousHash, 0xFF, 16);
	
	_currentCount = 0;
	_repetitionCount.clear();

	for(uint8_t* hash : _screenshotHashes) {
		delete[] hash;
	}
	_screenshotHashes.clear();

	_runningTest = false;
	_recording = false;
	_badFrameCount = 0;
}

void RecordedRomTest::Record(string filename, bool reset)
{
	_console->GetNotificationManager()->RegisterNotificationListener(shared_from_this());
	_filename = filename;

	string mrtFilename = FolderUtilities::CombinePath(FolderUtilities::GetFolderName(filename), FolderUtilities::GetFilename(filename, false) + ".mrt");
	_file.open(mrtFilename, ios::out | ios::binary);

	if(_file) {
		_console->Lock();
		Reset();

		VideoConfig videoCfg = _console->GetSettings()->GetVideoConfig();
		videoCfg.DisableFrameSkipping = true;
		_console->GetSettings()->SetVideoConfig(videoCfg);

		EmulationConfig emuCfg = _console->GetSettings()->GetEmulationConfig();
		emuCfg.RamPowerOnState = RamState::Fixed55;
		_console->GetSettings()->SetEmulationConfig(emuCfg);
				
		//Start recording movie alongside with screenshots
		RecordMovieOptions options;
		string movieFilename = FolderUtilities::CombinePath(FolderUtilities::GetFolderName(filename), FolderUtilities::GetFilename(filename, false) + ".mmo");
		memcpy(options.Filename, movieFilename.c_str(), std::max(1000, (int)movieFilename.size()));
		options.RecordFrom = reset ? RecordMovieFrom::StartWithSaveData : RecordMovieFrom::CurrentState;
		_console->GetMovieManager()->Record(options);

		_ppu = _console->GetPpu().get();
		_recording = true;
		_console->Unlock();
	}
}

int32_t RecordedRomTest::Run(string filename)
{
	_console->GetNotificationManager()->RegisterNotificationListener(shared_from_this());

	EmuSettings* settings = _console->GetSettings().get();
	string testName = FolderUtilities::GetFilename(filename, false);
	
	VirtualFile testMovie(filename, "TestMovie.msm");
	VirtualFile testRom(filename, "TestRom.sfc");

	ZipReader zipReader;
	zipReader.LoadArchive(filename);
	
	stringstream testData;
	zipReader.GetStream("TestData.mrt", testData);

	if(testData && testMovie.IsValid() && testRom.IsValid()) {
		char header[3];
		testData.read((char*)&header, 3);
		if(memcmp((char*)&header, "MRT", 3) != 0) {
			//Invalid test file
			return false;
		}
		
		Reset();

		uint32_t hashCount;
		testData.read((char*)&hashCount, sizeof(uint32_t));
			
		for(uint32_t i = 0; i < hashCount; i++) {
			uint8_t repeatCount = 0;
			testData.read((char*)&repeatCount, sizeof(uint8_t));
			_repetitionCount.push_back(repeatCount);

			uint8_t* screenshotHash = new uint8_t[16];
			testData.read((char*)screenshotHash, 16);
			_screenshotHashes.push_back(screenshotHash);
		}

		_currentCount = _repetitionCount.front();
		_repetitionCount.pop_front();

		VideoConfig cfg = settings->GetVideoConfig();
		cfg.DisableFrameSkipping = true;
		settings->SetVideoConfig(cfg);

		EmulationConfig emuCfg = _console->GetSettings()->GetEmulationConfig();
		emuCfg.RamPowerOnState = RamState::Fixed55;
		_console->GetSettings()->SetEmulationConfig(emuCfg);

		_console->Lock();
		//Start playing movie
		if(_console->LoadRom(testRom, VirtualFile(""))) {
			settings->SetFlag(EmulationFlags::MaximumSpeed);
			_console->GetMovieManager()->Play(testMovie, true);

			_ppu = _console->GetPpu().get();
			
			_runningTest = true;
			_console->Unlock();
			_signal.Wait();
			_console->Stop(false);
			_runningTest = false;
		} else {
			//Something went wrong when loading the rom
			_console->Unlock();
			return -2;
		}

		settings->ClearFlag(EmulationFlags::MaximumSpeed);

		return _badFrameCount;
	}

	return -1;
}

void RecordedRomTest::Stop()
{
	if(_recording) {
		Save();
	}
	Reset();
}

void RecordedRomTest::Save()
{
	//Wait until the next frame is captured to end the recording
	_signal.Wait();
	_repetitionCount.push_back(_currentCount);
	_recording = false;

	//Stop playing/recording the movie
	_console->GetMovieManager()->Stop();

	_file.write("MRT", 3);

	uint32_t hashCount = (uint32_t)_screenshotHashes.size();
	_file.write((char*)&hashCount, sizeof(uint32_t));
		
	for(uint32_t i = 0; i < hashCount; i++) {
		_file.write((char*)&_repetitionCount[i], sizeof(uint8_t));
		_file.write((char*)&_screenshotHashes[i][0], 16);
	}

	_file.close();

	ZipWriter writer;
	writer.Initialize(_filename);

	string mrtFilename = FolderUtilities::CombinePath(FolderUtilities::GetFolderName(_filename), FolderUtilities::GetFilename(_filename, false) + ".mrt");
	writer.AddFile(mrtFilename, "TestData.mrt");
	std::remove(mrtFilename.c_str());

	string mmoFilename = FolderUtilities::CombinePath(FolderUtilities::GetFolderName(_filename), FolderUtilities::GetFilename(_filename, false) + ".mmo");
	writer.AddFile(mmoFilename, "TestMovie.msm");
	std::remove(mmoFilename.c_str());

	writer.AddFile(_console->GetCartridge()->GetRomInfo().RomFile.GetFilePath(), "TestRom.sfc");
	
	writer.Save();

	MessageManager::DisplayMessage("Test", "TestFileSavedTo", FolderUtilities::GetFilename(_filename, true));
}