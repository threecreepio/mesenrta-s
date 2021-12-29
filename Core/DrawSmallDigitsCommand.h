#pragma once
#include "stdafx.h"
#include "DrawCommand.h"


class DrawSmallDigitsCommand : public DrawCommand
{
private:
	int _x, _y, _length, _color, _number;

	const uint32_t _digitData[0x10] = {
		0b11111001100110011111, // 0
		0b01100010001000100111, // 1
		0b11110001111110001111, // 2
		0b11110001111100011111, // 3
		0b10011001111100010001, // 4
		0b11111000111100011111, // 5
		0b11111000111110011111, // 6
		0b11110001000100010001, // 7
		0b11111001111110011111, // 8
		0b11111001111100010001, // 9
	};

protected:
	void InternalDraw()
	{
		int startX = (int)(_x * _xScale / _yScale);
		int startY = _y;
		int start = 1;
		for (int i = 1; i <= _length; i += 1) {
			int digit = (_number / start);
			int digitMod = digit % 10;
			int digitX = startX + (i * -5);
			int digitBits = _digitData[digitMod];
			for (int x = 0; x < 4; ++x) {
				for (int y = 0; y < 5; ++y) {
					int isset = digitBits & (1 << ((y * 4) + x));
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
