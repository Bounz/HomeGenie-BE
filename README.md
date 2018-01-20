## User forum

http://homegenie.club

## User's Guide and Developer Docs

https://bounz.github.io/HomeGenie-BE/

## Precompiled packages and install instructions

**Windows, Mac, Linux**

https://bounz.github.io/HomeGenie-BE/download.html



CURRENT FEATURES


* Modern, web based, responsive UI
  - Use it on every device, from desktop PCs to smart phones and tablets

* Integrated drivers for X10 and Z-Wave devices
  - Ready to use solution for your home automation

* Real and virtual energy metering with statistics
  - Energy consumption awareness for optimizing costs and usage

* UPnP / DLNA control point
  - Control media receiver, renderer and light/switch device types

* Wizard Scripting
  - No need to be a programmer, create scenarios with your fingers using the "live macro recording" feature

* Automation Program Plugins (APPs)
  - Easily add/share new automation programs and features

* Localization
  - English
  - Italian
  - Spanish
  - French
  - Dutch
  - Russian
  - Other languages can by added by creating a simple JSON file.

* Password protected access

* Configuration backup/restore and factory reset

* Embeddable
  - Runs on low-energy and low-cost embedded systems such as Raspberry Pi

* Customizable widgets and features
  - Create new features and widgets that interact with your devices and internet services

* Programmable Automation Engine
  - Web based Program Editor and compiler with multi-language support: C#, Javascript, Phyton and Ruby, Arduino Sketch 

* Dynamic Web Service API and Helper Classes
  - Make your devices and services talk each other using a common language over the net

* Direct access to Raspberry Pi hardware
  - GPIO, SPI, I2C programming using an event driven model

* Virtual modules

* Speech recognition / Voice Control (using Web Speech API)

* Speech synthesys

* Programmable InfraRed transceiver
  - Use a common IR remote to control your automation system

* Video4Linux camera video driver
  - Use a webcam as a remote monitoring device

* Mobile app clients (Android, Windows Phone)

* Open Source


## BUILT-IN INTERFACES AND PROTOCOL DRIVERS

- X10      Marmitek CM15Pro USB interface
- X10	   Marmitek CM11/CM12 Serial interface
- X10	   W800RF32AE X10 RF receiver
- Z-Wave   Aeon Labs Z-Stick 2 and other Zensys API compatible controllers
- Z-Wave   RaZberry, Z-Wave daughter board for Raspberry Pi
- Insteon  Insteon PLM 2413S/2413U
- LIRC	   Any LIRC compatible IR transceiver
- GPIO     Raspberry Pi GPIO 1Wire/SPI/I2C
- Arduino  Integrated Arduino(TM) development
- V4L      Video4Linux camera video driver
- Serial   Generic serial port interface
- TCP      Generic TCP interface
- UDP      Generic UDP interface


## BUILT-IN X10 FEATURES

- PLC: All Light On, All Units Off, On, Off, Dim, Bright
- RF: raw data receive and decoding


## BUILT-IN Z-WAVE FEATURES

- Nodes Discovery
- Node Inclusion/Exclusion
- Manufacturer Specific Get and Node Information (NIF)
- Basic Get/Set/Report
- Wake Up Get/Set
- Association Get/Set/Remove
- Association Group Get/Set/Remove
- Configuration Variable Get/Set
- MultiInstance/Channel Get/Set/Report (SwitchBinary, SwitchMultiLevel, SensorBinary, SensorMultiLevel)
- Metering Report
- Thermostat Mode/OperatingState/SetPoint/FanMode/FanState/Heating/SetBack
- UserCode
- DoorLock

## BUILT-IN Z-WAVE MODULE TYPES

- Switch
- Dimmer Light (MultiLevel Switch)
- Siren
- MultiChannel Switch / Dimmer
- Motion Sensor
- Temperature Sensor
- Luminance Sensor
- Door/Window Sensor
- Flood Sensor
- Smoke Sensor
- Heat Sensor
- CarbonDioxide Sensor
- CarbonMonoxide Sensor
- Thermostat


## AUTOMATION PROGRAM PLUGINS (APPs)

Lights
- IR/RF Remote Control
- Level Memory
- Smart Ligths

Energy Management
- Energy Monitor
- Energy Saving Mode
- Turn Off Delay

Scenes
- Group Lights ON/OFF
- Sunrise Color Scenario

Weather and Enviroment
- Earth Tools
- Generic Thermostat
- jkUtils OpenWeatherData
- jkUtils Solar Altitude
- Weather Undergroud

Messaging and Social
- Alcatel One Touch Y800Z SMS Notify
- E-Mail Account
- Pushing Box
- Windows Phone Push Notification Service

Devices and Things
- Favourites Links
- Generic IP Camera
- One-Wire Devices
- Philips Hue Bridge
- Serial Port I/O example

X10
- SC9000 RF Virtual Modules
- Set to 100% when switched on
- X10 RF Virtual Modules Mapper

Z-Wave
- Fibaro RGBW
- RFID Tag Reader
- Level Poll
- Multi Instance/Channel Virtual Modules
- Query on Wake Up

Security
- Ping Me at Home
- Presence Simulator
- Security Alarm System

Interconnections
- IR/RF remote control events forwarding
- Meter.Watts events forwarding
- Sensor.* events forwarding
- Status.Level events forwarding
- MQTT Network

Scheduling
- Timetable (for scheduling lights, shutters, thermostats, appliances)
- Scheduled ON/OFF

Raspberry Pi
- DHT-11 Temperature/Humidity sensor
- DHT-22 Temperature/Humidity sensor
- GPIO Modules
- Grove - Chainable RGB Led
- Grove - Led Bar
- HCSR04 - Ultrasonic Ranging Module
- MCP23017 GPIO Modules
- MCP3008 - Analog Input Modules
- Olimex - Nokia 3310 LCD display
- SmartIC - MCP23017 dual I/O expander
- SSD1306 - OLED display (128x64)

CubieTruck
- GPIO Modules

## Contributing

Read the [CONTRIBUTING.md](https://github.com/Bounz/HomeGenie-BE/blob/master/CONTRIBUTING.md) file
for information about contributing to this repository.

## Related projects

- https://github.com/Bounz/HomeGenie-BE-packages
- https://github.com/Bounz/HomeGenie-BE
- https://github.com/Bounz/zwave-lib-dotnet
- https://github.com/Bounz/x10-lib-dotnet
- https://github.com/Bounz/w800rf32-lib-dotnet
- https://github.com/Bounz/serialport-lib-dotnet
- https://github.com/Bounz/HomeGenie-BE-Android-ClientLib
- https://github.com/Bounz/HomeGenie-BE-Android
- https://github.com/Bounz/HomeGenie-BE-WindowsPhone


## CREDITS AND RESOURCES

HomeGenie is using:
- jQuery Mobile for its UI          http://http://jquerymobile.com
- jQuery UI Touch Punch             http://touchpunch.furf.com
- Flot for statistics graphs        http://code.google.com/p/flot
- moment.js for date formatting     http://momentjs.com
- libusb for its CM15Pro Usb Driver http://www.libusb.org
- SQLite for statistics database    http://sqlite.org
- CodeMirror                        http://codemirror.net
- Raphael Js                        http://raphaeljs.com
- Raspberry#-IO                     https://github.com/raspberry-sharp/raspberry-sharp-io
- Expression Evaluator              http://www.codeproject.com/Articles/9114/math-function-boolean-string-expression-evaluator
- NewtonSoft Json                   http://james.newtonking.com/json
- IronLanguages                     http://github.com/IronLanguages/main
- NCrontab                          http://code.google.com/p/ncrontab/
- Intel UPnP                        http://opentools.homeip.net/dev-tools-for-upnp
- LIRC                              http://lirc.org
- LAME                              http://lame.sourceforge.net
- Pepper One Z-Wave DB              http://www.pepper1.net/
- M2Mqtt                            http://m2mqtt.wordpress.com

Z-Wave driver (ZWaveLib) originarly based on article "An introduction to Z-Wave programming in C#" (http://www.digiwave.dk/en/programming/an-introduction-to-z-wave-programming-in-c/).



### License Information

[READ LICENSE FILE](LICENSE)

### Disclaimer

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
