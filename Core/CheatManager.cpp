#include "stdafx.h"
#include "CheatManager.h"
#include "MessageManager.h"
#include "Console.h"
#include "NotificationManager.h"
#include "../Utilities/HexUtilities.h"

CheatManager::CheatManager(Console* console)
{
	_console = console;
}

void CheatManager::AddCheat(CheatCode code)
{
	_cheats.push_back(code);
	_cheatsByAddress.emplace(code.Address, code);
	_hasCheats = true;
	_bankHasCheats[code.Address >> 16] = true;

	if(code.Address >= 0x7E0000 && code.Address < 0x7E2000) {
		//Mirror codes for the first 2kb of workram across all workram mirrors
		CheatCode mirror;
		mirror.Value = code.Value;
		for(int i = 0; i < 0x3F; i++) {
			mirror.Address = (i << 16) | (code.Address & 0xFFFF);
			AddCheat(mirror);
			mirror.Address |= 0x800000;
			AddCheat(mirror);
		}
	}
}

void CheatManager::SetCheats(vector<CheatCode> codes)
{
	auto lock = _console->AcquireLock();

	bool hasCheats = !_cheats.empty();
	ClearCheats(false);
	for(CheatCode &code : codes) {
		AddCheat(code);
	}

	if(codes.size() > 1) {
		MessageManager::DisplayMessage("Cheats", "CheatsApplied", std::to_string(codes.size()));
	} else if(codes.size() == 1) {
		MessageManager::DisplayMessage("Cheats", "CheatApplied");
	} else if(hasCheats) {
		MessageManager::DisplayMessage("Cheats", "CheatsDisabled");
	}

	_console->GetNotificationManager()->SendNotification(ConsoleNotificationType::CheatsChanged);
}

void CheatManager::SetCheats(uint32_t codes[], uint32_t length)
{
	vector<CheatCode> cheats;
	cheats.reserve(length);
	for(uint32_t i = 0; i < length; i++) {
		CheatCode code;
		code.Address = codes[i] >> 8;
		code.Value = codes[i] & 0xFF;
		cheats.push_back(code);
	}
	SetCheats(cheats);
}

void CheatManager::ClearCheats(bool showMessage)
{
	auto lock = _console->AcquireLock();

	bool hadCheats = !_cheats.empty();

	_cheats.clear();
	_cheatsByAddress.clear();
	_hasCheats = false;
	memset(_bankHasCheats, 0, sizeof(_bankHasCheats));

	if(showMessage && hadCheats) {
		MessageManager::DisplayMessage("Cheats", "CheatsDisabled");

		//Used by net play
		_console->GetNotificationManager()->SendNotification(ConsoleNotificationType::CheatsChanged);
	}
}

void CheatManager::AddStringCheat(string code)
{
	static string _convertTable = "DF4709156BC8A23E";
	
	auto lock = _console->AcquireLock();
	
	std::transform(code.begin(), code.end(), code.begin(), ::toupper);

	if(code.size() == 9 && code[4] == '-') {
		uint32_t rawValue = 0;
		for(int i = 0; i < (int)code.size(); i++) {
			if(code[i] != '-') {
				rawValue <<= 4;
				size_t pos = _convertTable.find_first_of(code[i]);
				if(pos == string::npos) {
					//Invalid code
					return;
				}
				rawValue |= (uint32_t)pos;
			}
		}

		CheatCode cheat;
		cheat.Address = (
			((rawValue & 0x3C00) << 10) |
			((rawValue & 0x3C) << 14) |
			((rawValue & 0xF00000) >> 8) |
			((rawValue & 0x03) << 10) |
			((rawValue & 0xC000) >> 6) |
			((rawValue & 0xF0000) >> 12) |
			((rawValue & 0x3C0) >> 6)
		);

		cheat.Value = rawValue >> 24;

		AddCheat(cheat);
	} else if(code.size() == 8) {
		for(int i = 0; i < (int)code.size(); i++) {
			if((code[i] < 'A' || code[i] > 'F') && (code[i] < '0' || code[i] > '9')) {
				//Invalid code
				return;
			}
		}

		uint32_t rawValue = HexUtilities::FromHex(code);
		CheatCode cheat;
		cheat.Address = rawValue >> 8;
		cheat.Value = rawValue & 0xFF;
		AddCheat(cheat);
	}
}

bool CheatManager::HasCheats()
{
	return _hasCheats;
}

vector<CheatCode> CheatManager::GetCheats()
{
	return _cheats;
}