# Simple Reusable XInput Emulator

## What is it?

This project hooks applications that use XInput and allows the controller state to be set
programatically in a very simple way.

## Why would you want this?

I was famous for beating Dark Souls with a bunch of weird control methods, and I needed
a more reusable way to adapt various controllers to hook into the game. Typically I used
keyboard emulation, but in order to simulate analog sticks (for using Kinect as a controller),
I needed a way to have full control over XInput.

This could be used for other challenge runs, or perhaps for tool assisted/scripted gameplay.

## Requirements

* EasyHook (https://easyhook.github.io/) is used for hooking the XInput API
* SharpDX (http://sharpdx.org/) is used for some C# definitions of XInput structures,
but the dependency could probably be removed pretty easily
* I believe you need a real XInput device plugged in (i.e. Xbox 360 controller), but that could
be worked around

## Usage

* Run the XInputEmu.exe program after the program you want to hook is running. With no arguments, it looks
for a running Dark Souls process. Otherwise, the PID of the desired process can be specified as an argument.
* The program starts a UDP socket on port 13000 that listens for packets of a simple format
* To update the XInput state, simply send a UDP packet with the decimal values of the XInput states wButton,
bLeftTrigger, bRightTrigger, sThumbLX, sThumbLY, sThumbRX, sThumbRY separated by spaces
  * Example: "4096 255 0 0 30000 -30000 30000" would set the state to A pressed (bitmask 0x1000), left trigger
    fully pressed, right trigger not pressed, left analog stick straight up, and right analog stick at a 45 degree
    angle up to the left
    
## Notes

* Only ever tested by me on Dark Souls. I suspect it works for other programs, as long as they use xinput1_3.dll
* This project doesn't do much by itself - another program is needed to feed it input from another source. Simple example 
in Python of how to get a character to start walking forward and backwards every five seconds:

```python
import socket
import time
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
while True:
    s.sendto("0 0 0 30000 0 0 0", ("localhost", 13000))
    time.sleep(5)
    s.sendto("0 0 0 -30000 0 0 0", ("localhost", 13000))
```