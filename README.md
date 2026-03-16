# Aquarium and Experimental Flumes Control System

## Overview

This project implements a complete automated control solution for experimental aquariums and flumes used in scientific research. The system enables precise regulation of multiple environmental parameters (temperature, pH, flow rate, pressure, current velocity) to maintain controlled experimental conditions.

### General Architecture

The system consists of several interconnected modules:

1. **Arduino Controllers**: Industrial controllers (IndustrialShields) that drive sensors and actuators
2. **C# Supervision Application**: SuperviFlume_v2 - central WebSocket server, real-time display, alarm management and data storage
3. **Web Interface**: Frontend hosted at https://github.com/purrutti/flumeswebsite
4. **Database**: InfluxDB for data historization

### Communication

- **Protocol**: WebSocket (port 81) for real-time communication
- **Format**: JSON for data exchange
- **Modbus RTU**: Communication with Hamilton sensors (pH/temperature) and PODOC (O2)

---

## System Components

### 1. Aquarium (Aquarium.ino)

**Location**: `Aquarium/Aquarium/Aquarium.ino`

**Description**: Manages 3 aquariums with automatic temperature and pH regulation.

#### Main Features

**Sensors and Measurements**:
- Hamilton pH/temperature sensors via Modbus (ID 1-3)
- PODOC dissolved oxygen sensors (ID 10-12)
- Analog flowmeters (3 units)

**Temperature Regulation**:
- PID controller to maintain temperature setpoint
- Uses 2 three-way valves (V3V) per aquarium: hot (PIN_V3VC) and cold (PIN_V3VF)
- Reference temperatures: ambient water (18°C), hot (24°C), cold (5°C)
- Automatic hot/cold selection based on setpoint

**pH Regulation**:
- Reverse PID controller for CO2 injection
- CO2 solenoid valve with PWM modulation (10s cycle)
- Ambient pH reference: 8.0

**Pin Configuration**:
```cpp
PIN_DEBITMETRE[3] = {54, 55, 56}
PIN_V3VC[3] = {4, 6, 9}       // Hot three-way valves
PIN_V3VF[3] = {5, 8, 7}       // Cold three-way valves
PIN_CO2[3] = {36, 37, 38}     // CO2 solenoid valves
```

**Network Configuration**:
- PLCID: 1–4 (one board per 3 aquariums, 12 aquariums total)
- IP: 172.16.36.20X (X = PLCID)
- Server: 192.168.73.14:81

#### Main Functions

**`setup()`**:
- Initializes Ethernet, WebSocket, Modbus (9600 baud)
- Configures PIDs for temperature and pH
- Loads parameters from EEPROM

**`loop()`**:
- Cyclic reading of Modbus sensors
- Calculation and application of PID regulations
- Periodic data transmission (5s)

**`readSensors()`**:
- Reading sequence: pH then temperature for each aquarium
- O2 reading from PODOC sensors (request/read sequence)
- Calibration management if requested

**`regulTemp(int aquaID)`**:
- Determines hot/cold mode based on setpoint vs ambient temperature
- Calculates PID output with limits 50-255
- Applies PWM to appropriate valve

**`regulationpH()`** (Aqua class):
- Calculates PID output (0-100%)
- Generates software PWM for CO2 solenoid valve (10s cycle)

**JSON Communication**:
- `REQ_PARAMS (0)`: Parameter request
- `SEND_PARAMS (2)`: Send parameters (setpoints, Kp, Ki, Kd)
- `SEND_DATA (3)`: Send real-time data
- `SEND_MASTER_DATA (6)`: Receive master data (reference temperatures)

---

### 2. MasterFlumes (MasterFlumes.ino)

**Location**: `MasterFlumes/MasterFlumes/MasterFlumes.ino`

**Description**: Centralized water conditioning system for flumes. Manages 3 water circuits (hot, cold, ambient) with pressure regulation and temperature via heat pumps (PAC).

#### Main Features

**Conditioning System**:

**3 Water Circuits**:
1. **Hot water**: Temperature regulated by hot PAC (setpoint 30°C)
2. **Cold water**: Temperature regulated by cold PAC (setpoint 12°C)
3. **Ambient water**: Pressure regulated, ambient temperature + pH measurements

**Sensors**:
- 3 analog pressure sensors 4-20mA (0-4 bars)
- 3 analog flowmeters 4-20mA (0-15 l/min)
- 2 PAC temperature probes 4-20mA (0-50°C)
- 1 Hamilton pH/temperature sensor (Modbus ID 1) for ambient water
- 1 level detector (alarm)

**Actuators**:
- 3 PWM proportional two-way valves (V2V) for pressure regulation
- 2 PWM three-way valves for PAC (heat exchange)

**Pin Configuration**:
```cpp
PIN_PRESSION[3] = {54, 56, 55}      // Pressure sensors
PIN_DEBITMETRE[3] = {60, 61, 62}    // Flowmeters
PIN_V2VC = 6, PIN_V2VF = 4, PIN_V2VA = 5  // 2-way valves
PIN_V3VC = 9, PIN_V3VF = 8          // PAC 3-way valves
PIN_TEMP_PAC_C = 58, PIN_TEMP_PAC_F = 57
PIN_NIVEAU = 59                      // Level alarm
```

**Network Configuration**:
- PLCID: 5
- IP: 172.16.36.205

#### Regulations

**Pressure Regulation** (Condition class):
- DIRECT PID to maintain minimum pressure (0.2 bar by default)
- PWM output 50-255 on two-way valves
- PID parameters: Kp=100, Ki=10, Kd=20
- Water level safety: cuts ambient water if alarm

**PAC Temperature Regulation** (PAC class):
- Hot PAC: DIRECT PID (Kp=50, Ki=1, Kd=20)
- Cold PAC: REVERSE PID (Kp=50, Ki=1, Kd=20)
- PWM output 50-255 on three-way valves
- Temperature reading 4-20mA → 0-50°C

#### Main Functions

**`readPressure(int lissage)`**:
- Analog reading → mA conversion → bars
- Exponential smoothing according to parameter (100 = strong smoothing)
- Mapping: 400-2000 mA → 0-4 bars

**`readFlow(int lissage)`**:
- Analog reading → mA → l/min
- Formula: flow = (9.375 * (mA - 394)) / 100.0
- Range: 0-15 l/min

**`checkNiveau()`**:
- Checks level detector
- If alarm: cuts ambient water valve, activates alarmeNiveau

**`readMBSensors()`**:
- Cyclic reading pH then temperature from ambient sensor
- Hamilton Modbus sensor ID 1

**JSON Communication**:
- `SEND_MASTER_DATA (6)`: Send data for 3 conditions
- Structure: temperature, pressure, flow + regulations for each circuit

---

### 3. Flume (Flume.ino)

**Location**: `Flume/Flume/Flume/Flume.ino`

**Description**: Manages 4 experimental flumes with advanced temperature, pH, flow regulation and current velocity measurement.

#### Particularities

**Hybrid Temperature Regulation**:
- Flumes 1-3: Classic PWM three-way valves
- Flume 4: DAC (Digital-to-Analog Converter) GP8403 I2C 0-10V
- Bidirectional PID: output -255 to +255 (negative=cold, positive=hot)
- Temperature offset support: setpoint = tempAmbient + offset

**Sensors**:
- Hamilton pH/temperature (ID 1-4)
- PODOC O2 (ID 10-13)
- Analog flowmeters
- Analog velocity sensors (4 units)

**Pin Configuration**:
```cpp
PIN_DEBITMETRE[4] = {54, 55, 56, 57}
PIN_V3VC[4] = {4, 6, 9, 0}
PIN_V3VF[4] = {5, 8, 7, 1}
PIN_CO2[4] = {36, 37, 38, 39}
PIN_VITESSE[4] = {22, 23, 24, 25}
```

**GP8403 DAC**:
- I2C Address: 0x5F
- 2 channels 0-10V (OUT0=hot, OUT1=cold)
- Resolution: 15 bits (0-10000 = 0-10V)

#### Specific Functions

**`regulTemp(int flumeID)`**:
- Bidirectional PID calculation
- If output < 0: activate cold circuit
- If output > 0: activate hot circuit
- Flume 4: DAC control instead of PWM
- Output mapping: -255/+255 → 0-10V DAC or inverted PWM

**`readSpeed()`**:
- Analog velocity sensor reading
- Stored in flume[i].vitesse

**Sensor Calibration**:
- Support for Hamilton (pH) and PODOC (O2) calibration
- Multi-step procedure via Modbus
- Factory reset available (parameter 99)

**EEPROM Save**:
- PID parameters, setpoints, offsets
- State (control/regulation)
- Sequential memory addresses per flume

---

### 4. SuperviFlume_v2 — C# Supervision Application

**Location**: `SuperviFlume_v2/SuperviFlume_v2/SuperviFlume_v2/`

**Description**: Central WPF (.NET 4.7.2) application acting as the WebSocket server. It receives real-time data from all Arduino controllers, displays the full installation state, manages alarms and exports data.

#### Architecture

**Framework**: .NET 4.7.2 WPF, HttpListener-based WebSocket server

**Key source files**:

| File | Role |
|---|---|
| `MainWindow.xaml(.cs)` | Main window — 3 tabs: General, Aquariums, Flumes |
| `Models.cs` | Data models: `Aquarium`, `MasterData`, `Regul` |
| `WebSocketServer.cs` | WebSocket server, JSON parsing, data broadcast |
| `Alarms.cs` | `AlarmManager`, `Alarme`, `AlarmSettings` classes |
| `AlarmsWindow.xaml(.cs)` | Alarm settings and active alarm viewer |
| `SetpointsWindow.xaml(.cs)` | Setpoint editor (temperature, pH, PID) |
| `SensorCalibration.xaml(.cs)` | Hamilton/PODOC calibration interface |
| `GeneralInletParamsWindow.xaml(.cs)` | General water inlet parameter editor |
| `LogWindow.xaml(.cs)` | WebSocket message log |

#### Real-time Display

- **12 aquariums** and **8 flumes**, updated on each WebSocket frame
- Per device: flow (Q), temperature (T), pH, dissolved oxygen (O₂), current velocity (flumes only)
- Setpoints and PID outputs for temperature and pH
- **Device state**: `DISABLED` / `CONTROL` / `TREATMENT` — disabled devices are grayed out
- **General inlet tab**: hot, cold and ambient circuits (T, pressure, flow, PID)

#### Alarm Management

Alarms are evaluated after each data update. Each alarm has a 5-second trigger delay before being raised, and auto-resets when the condition clears.

| Measurement | Type | Logic |
|---|---|---|
| Temperature | Alarm | \|value − setpoint\| > delta (°C) |
| pH | Warning | \|value − setpoint\| > delta |
| O₂ | Warning | value < minimum threshold (%) |
| Flow rate | Warning | value < minimum threshold (L/min) |
| Current velocity | Warning | value < minimum threshold (m/s) — flumes only |

- **Visual indicator**: the measurement label shows ⚠ in red (alarm) or orange (warning)
- **Slack notification**: HTTP POST to an Incoming Webhook on first raise
- **Manual acknowledgement** from the AlarmsWindow
- Settings persisted to `alarm_settings.json` (next to the executable)

#### Data Export

**InfluxDB**:
- Measurement: `Flumes`
- Tags: Aquarium/Flume ID
- Fields: temperature, pH, O₂, flow, setpoints, PID outputs
- Configurable write interval

**CSV**:
- One daily file: `<dataFileBasePath>_YYYY-MM-DD.csv`
- All 12 aquariums + 8 flumes per row

#### Configuration (`App.config`)

| Key | Description |
|---|---|
| `serverUrl` | WebSocket listen address (default: `http://192.168.73.14:81/`) |
| `dataLogInterval` | CSV/InfluxDB save interval (minutes) |
| `dataFileBasePath` | CSV output folder |
| `InfluxDBUrl` / `InfluxDBToken` / `InfluxDBBucket` / `InfluxDBOrg` | InfluxDB connection |
| `SlackWebhookUrl` | Slack Incoming Webhook URL |

> **Security**: do not commit tokens or webhook URLs — set them locally in `App.config` only.

---

## JSON Communication Protocol

### Message Structures

**Parameter Request**:
```json
{
  "cmd": 0,
  "AquaID": 1,
  "PLCID": 7
}
```

**Send Parameters**:
```json
{
  "cmd": 2,
  "PLCID": 7,
  "AquaID": 1,
  "time": 1234567890,
  "state": 1,
  "rTemp": {
    "cons": 20.5,
    "Kp": 5.0,
    "Ki": 1.0,
    "Kd": 500.0,
    "aForcage": "false",
    "consForcage": 0,
    "useOffset": true,
    "offset": 2.0
  },
  "rpH": {
    "cons": 7.5,
    "Kp": 0.2,
    "Ki": 50.0,
    "Kd": 0.0,
    "aForcage": "false",
    "consForcage": 0
  }
}
```

**Send Data**:
```json
{
  "cmd": 3,
  "PLCID": 7,
  "AquaID": 1,
  "time": 1234567890,
  "temp": 20.25,
  "pH": 7.45,
  "oxy": 85.3,
  "debit": 2.15,
  "rTemp": { "cons": 20.5, "sPID_pc": 45 },
  "rpH":  { "cons": 7.5,  "sPID_pc": 23 }
}
```

**Master Data**:
```json
{
  "cmd": 6,
  "PLCID": 5,
  "time": 1234567890,
  "data": [
    {
      "CondID": 1,
      "temperature": 30.2,
      "pression": 1.25,
      "debit": 8.5,
      "rTemp":     { "cons": 30.0, "sPID_pc": 65 },
      "rPression": { "cons": 0.2,  "sPID_pc": 55 }
    },
    {
      "CondID": 2,
      "temperature": 12.1,
      "pression": 1.22,
      "debit": 7.8,
      "rTemp":     { "cons": 12.0, "sPID_pc": 42 },
      "rPression": { "cons": 0.2,  "sPID_pc": 51 }
    },
    {
      "CondID": 3,
      "temperature": 18.5,
      "pH": 8.12,
      "pression": 0.95,
      "debit": 5.2,
      "rPression": { "cons": 0.2, "sPID_pc": 38 }
    }
  ]
}
```

**Sensor Calibration**:
```json
{
  "cmd": 4,
  "PLCID": 7,
  "sensorID": 1,
  "calibParam": 0,
  "value": 7.01
}
```
- `calibParam: 0` = low point calibration
- `calibParam: 1` = high point calibration
- `calibParam: 99` = factory reset

### Command Table

| Code | Direction | Description |
|---|---|---|
| `0` | PC → PLC | Request parameters |
| `1` | PC → PLC | Request data |
| `2` | PC ↔ PLC | Send/receive parameters |
| `3` | PLC → PC | Send measured data |
| `4` | PC → PLC | Sensor calibration command |
| `5` | PC → PLC | Request general inlet data |
| `6` | PLC → PC | Send general inlet data (MasterFlumes) |

---

## Network Configuration

### IP Addresses

| Controller | PLCID | IP | Function |
|---|---|---|---|
| MasterFlumes | 5 | 172.16.36.205 | Water conditioning |
| Aquarium PLC 1 | 1 | 172.16.36.201 | Aquariums 1–3 |
| Aquarium PLC 2 | 2 | 172.16.36.202 | Aquariums 4–6 |
| Aquarium PLC 3 | 3 | 172.16.36.203 | Aquariums 7–9 |
| Aquarium PLC 4 | 4 | 172.16.36.204 | Aquariums 10–12 |
| Flume PLCs | 13–20 | — | Flumes 1–8 |
| SuperviFlume_v2 | — | 192.168.73.14:81 | WebSocket server |

### Modbus RTU

**Configuration**:
- Speed: 9600 baud
- Port: Serial3 (pin 46 direction)
- Timeout: 1000 ms

**Sensor Addresses**:
- Hamilton pH/Temp: ID 1–13 (depending on system)
- PODOC O2: ID 10–13

---

## Deployment and Usage

### Prerequisites

**Arduino / IndustrialShields**:
- Arduino IDE or Visual Micro
- Libraries: `Ethernet`, `WebSockets`, `ModbusRtu`, `TimeLib`, `EEPROMex`, `ArduinoJson`, `RTC`, `PID_v1`

**SuperviFlume_v2**:
- Windows 10+, Visual Studio 2019+, .NET Framework 4.7.2
- NuGet packages: `Newtonsoft.Json`, `InfluxDB.Client`

**InfluxDB** (optional):
- InfluxDB 2.x installed on the supervision PC or a local server

### Installation

**1. InfluxDB Configuration**:
```bash
influx org create -n your_org
influx bucket create -n Flumes -o your_org
influx auth create --org your_org --all-access
```

**2. SuperviFlume_v2 Configuration** — edit `App.config`:
```xml
<appSettings>
  <add key="serverUrl"        value="http://192.168.73.14:81/" />
  <add key="dataLogInterval"  value="1" />
  <add key="dataFileBasePath" value="C:\Data\flumes\" />
  <add key="InfluxDBUrl"      value="http://localhost:8086" />
  <add key="InfluxDBToken"    value="your_token_here" />
  <add key="InfluxDBBucket"   value="Flumes" />
  <add key="InfluxDBOrg"      value="LOV" />
  <add key="SlackWebhookUrl"  value="your_webhook_url_here" />
</appSettings>
```

**3. Arduino Compilation**:
- Open each `.ino` project in Arduino IDE
- Select the appropriate IndustrialShields board
- Verify `PLCID` and `SERVER_IP` in the sketch
- Compile and upload

**4. Launch**:
1. Start InfluxDB
2. Launch `SuperviFlume_v2.exe` — the WebSocket server starts automatically
3. Power up Arduino controllers
4. Check connections in the Log window

### Daily Usage

**Modifying Setpoints**: open *Tools → Setpoints*, edit values and click Submit. The PLC is updated immediately and the display refreshes.

**Sensor Calibration**: open *Tools → Calibrate Sensors*, immerse sensor in standard solution, send calibration command and validate.

**Alarm Management**: open *Tools → Alarms* to configure thresholds and acknowledge active alarms.

**Monitoring**: SuperviFlume_v2 main window for real-time data; InfluxDB/Grafana for history; CSV files for daily export.

---

## Troubleshooting

| Symptom | Checks |
|---|---|
| PLC not connecting | Check `SERVER_IP` in sketch, Windows firewall on port 81 |
| Modbus timeout | RS-485 wiring (A/B/GND), 120 Ω terminations, sensor 24 V supply |
| Unstable PID | Reduce Kp, increase integral time, check actuators |
| No Slack alert | Check `SlackWebhookUrl` in `App.config`, network access from PC |
| No InfluxDB data | Check token/bucket/org, verify InfluxDB is running |

**Arduino Serial Monitor**: 115200 baud — shows connection events, sensor readings, Modbus errors.

**SuperviFlume_v2 Log window**: all WebSocket messages with timestamps.

---

## File Structure

```
Flumes/
├── Aquarium/
│   └── Aquarium/
│       ├── Aquarium.ino          # Controls 3 aquariums per board
│       ├── Aqua.h                # Aqua + Regul class
│       └── ModbusSensor.h        # Modbus sensor interface
├── MasterFlumes/
│   └── MasterFlumes/
│       ├── MasterFlumes.ino      # General water conditioning
│       └── ModbusSensor.h
├── Flume/
│   └── Flume/Flume/
│       ├── Flume.ino             # Controls 4 flumes per board
│       ├── Flume.h               # Flume + Regul class
│       └── ModbusSensor.h
├── SuperviFlume_v2/
│   └── SuperviFlume_v2/
│       └── SuperviFlume_v2/
│           ├── MainWindow.xaml(.cs)
│           ├── WebSocketServer.cs
│           ├── Models.cs
│           ├── Alarms.cs
│           ├── AlarmsWindow.xaml(.cs)
│           ├── SetpointsWindow.xaml(.cs)
│           ├── SensorCalibration.xaml(.cs)
│           ├── GeneralInletParamsWindow.xaml(.cs)
│           ├── LogWindow.xaml(.cs)
│           └── App.config
└── testServo/
    testPression/
    testESC/
```

---

## References

- **Web Frontend**: https://github.com/purrutti/flumeswebsite
- **InfluxDB**: https://docs.influxdata.com/
- **Arduino PID Library**: https://playground.arduino.cc/Code/PIDLibrary/
- **Hamilton Modbus**: pH sensor technical documentation
- **IndustrialShields**: https://www.industrialshields.com/
