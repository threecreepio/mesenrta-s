#pragma once
#include "stdafx.h"
#include "../Utilities/SimpleLock.h"
#include "EventType.h"

class Debugger;
class ScriptHost;
enum class MemoryOperationType;

class ScriptManager
{
private:
	Debugger *_debugger;
	bool _hasScript;
	SimpleLock _scriptLock;
	int _nextScriptId;
	vector<shared_ptr<ScriptHost>> _scripts;

public:
	ScriptManager(Debugger *debugger);

	int32_t LoadScript(string name, string content, int32_t scriptId);
	void RemoveScript(int32_t scriptId);
	const char* GetScriptLog(int32_t scriptId);
	void ProcessEvent(EventType type);
	void ProcessMemoryOperation(uint32_t address, uint8_t &value, MemoryOperationType type);
};