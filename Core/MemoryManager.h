#pragma once
#include "stdafx.h"
#include "DebugTypes.h"
#include "MemoryMappings.h"
#include "../Utilities/ISerializable.h"

class IMemoryHandler;
class RegisterHandlerA;
class RegisterHandlerB;
class InternalRegisters;
class RamHandler;
class BaseCartridge;
class Console;
class Ppu;
class Cpu;
class CheatManager;
enum class MemoryOperationType;

class MemoryManager : public ISerializable
{
public:
	constexpr static uint32_t WorkRamSize = 0x20000;

private:
	Console* _console;
	bool _isUserModified = false;

	shared_ptr<RegisterHandlerA> _registerHandlerA;
	shared_ptr<RegisterHandlerB> _registerHandlerB;

	InternalRegisters *_regs;
	Ppu* _ppu;
	Cpu* _cpu;
	BaseCartridge* _cart;
	CheatManager* _cheatManager;

	MemoryMappings _mappings;
	vector<unique_ptr<IMemoryHandler>> _workRamHandlers;

	uint8_t *_workRam;
	uint64_t _masterClock = 0;
	uint16_t _hClock = 0;
	uint16_t _dramRefreshPosition = 0;
	uint16_t _hdmaInitPosition = 0;
	uint8_t _openBus = 0;
	uint8_t _cpuSpeed = 8;
	SnesMemoryType _memTypeBusA = SnesMemoryType::PrgRom;

	bool _hasEvent[1369];
	uint8_t _masterClockTable[2][0x10000];

	void UpdateEvents();
	__forceinline void SyncCoprocessors();
	void Exec();

public:
	void Initialize(Console* console, bool userModified);
	virtual ~MemoryManager();

	void Reset();
	bool IsUserModified();
	void SetUserModified(bool userModified);

	void GenerateMasterClockTable();

	void IncMasterClock4();
	void IncMasterClock6();
	void IncMasterClock8();
	void IncMasterClock40();
	void IncMasterClockStartup();
	void IncrementMasterClockValue(uint16_t value);

	uint8_t Read(uint32_t addr, MemoryOperationType type);
	uint8_t ReadDma(uint32_t addr, bool forBusA);

	uint8_t Peek(uint32_t addr);
	uint16_t PeekWord(uint32_t addr);
	void PeekBlock(uint32_t addr, uint8_t * dest);

	void Write(uint32_t addr, uint8_t value, MemoryOperationType type);
	void WriteDma(uint32_t addr, uint8_t value, bool forBusA);

	uint8_t GetOpenBus();
	uint64_t GetMasterClock();
	uint16_t GetHClock();
	uint8_t* DebugGetWorkRam();

	MemoryMappings* GetMemoryMappings();

	uint8_t GetCpuSpeed(uint32_t addr);
	uint8_t GetCpuSpeed();
	void SetCpuSpeed(uint8_t speed);
	SnesMemoryType GetMemoryTypeBusA();

	bool IsRegister(uint32_t cpuAddress);
	bool IsWorkRam(uint32_t cpuAddress);
	int GetRelativeAddress(AddressInfo &address, uint8_t startBank = 0);

	void Serialize(Serializer &s) override;
};
