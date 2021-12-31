#pragma once
#include "stdafx.h"
#include "DrawCommand.h"


class DrawSmallDigitsCommand : public DrawCommand
{
private:
	int _x, _y, _length, _color, _number;

	const uint32_t _digitData[0x10] = {
		0b111101101101111, // 0
		0b110010010010111, // 1
		0b111001111100111, // 2
		0b111001111001111, // 3
		0b101101111001001, // 4
		0b111100111001111, // 5
		0b111100111101111, // 6
		0b111001001001001, // 7
		0b111101111101111, // 8
		0b111101111001001, // 9
	};

protected:
	void InternalDraw()
	{
		int startX = (int)(_x * _xScale / _yScale);
		int startY = _y;
		int start = 1;
		for (int i = 1; i <= _length; i += 1) {
			int digit = (_number / start);
			if (digit == 0) break;
			int digitMod = digit % 10;
			int digitX = startX + (i * -4);
			int digitBits = _digitData[digitMod];
			for (int x = 0; x < 3; ++x) {
				for (int y = 0; y < 5; ++y) {
					int isset = digitBits & (1 << ((y * 3) + x));
					if (isset > 0) {
						DrawPixel(digitX + (4 - x), startY + (5 - y), _color);
					}
				}
			}
			start *= 10;
		}
	}

public:
	DrawSmallDigitsCommand(int x, int y, int number, int length, int color, int frameCount, int startFrame) :
		DrawCommand(startFrame, frameCount, true), _x(x), _y(y), _color(color), _length(length), _number(number)
	{
		//Invert alpha byte - 0 = opaque, 255 = transparent (this way, no need to specifiy alpha channel all the time)
		_color = (~color & 0xFF000000) | (color & 0xFFFFFF);
	}
};
