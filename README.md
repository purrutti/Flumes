# Aquarium and Experimental Flumes Control System

## Overview

This project implements a complete automated control solution for experimental aquariums and flumes used in scientific research. The system enables precise regulation of multiple environmental parameters (temperature, pH, flow rate, pressure, current velocity) to maintain controlled experimental conditions.

### General Architecture

The system consists of several interconnected modules:

1. **Arduino Controllers**: Industrial controllers (IndustrialShields) that drive sensors and actuators
2. **C# WebSocket Server**: SuperviFlume - central communication and data storage server
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
- PLCID: 7
- IP: 172.16.36.207
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

### 4. ESP32_MoteurFlumes (ESP32_MoteurFlumes.ino)

**Location**: `ESP32_MoteurFlumes/ESP32_MoteurFlumes/ESP32_MoteurFlumes.ino`

**Description**: ESP32 controller to drive 8 brushless motors (ESC) for flumes via web server.

#### Features

**Network**:
- Access Point mode: SSID "flumesmotors" / Password "flumesmotors"
- mDNS: flumemotors.local
- Asynchronous HTTP server (AsyncWebServer port 80)

**Motor Control**:
- 8 motors via ESC (Electronic Speed Controller)
- Servo protocol: 1500μs = stop, 1500-1900μs = speed
- ESP32Servo library

**PWM Pins**:
```cpp
pwmPins[8] = {2, 4, 12, 13, 14, 15, 25, 26}
```

#### Web Interface

**Bootstrap HTML Page**:
- Table with 8 rows (Flume 13-20)
- ON/OFF toggle switch per motor
- Setpoint slider 0-100%
- Submit button to apply

**Functions**:

**`setup()`**:
- Initializes WiFi AP and mDNS
- Attaches servos to 8 pins
- Sends stop signal (1500μs) to all ESCs
- Configures HTTP routes

**`generateRows()`**:
- Generates dynamic HTML for table
- Toggle state and setpoint for each flume

**`/submit` POST Route**:
- Retrieves toggle0-7 and consigne0-7 parameters
- Calculates PWM: map(setpoint, 0-100, 1500-1900μs)
- If OFF: force 1500μs (stop)
- Applies servo.writeMicroseconds()

---

### 5. SuperviFlume - C# Server (MainWindow.xaml.cs)

**Location**: `SuperviFlume/SuperviFlume/MainWindow.xaml.cs`

**Description**: Central C# WPF WebSocket server that orchestrates communication between all Arduino controllers and the web interface.

#### Architecture

**Framework**: .NET WPF with HttpListener for WebSocket

**Database**:
- InfluxDB (localhost:8086)
- Organization, bucket and token configured in App.config
- Asynchronous data point writing

**Data Model**:

**`Aquarium` Class**:
```csharp
- ID, PLCID: identifiers
- debit, debitCircul: flow rates
- temperature, pH, oxy: measurements
- regulTemp, regulpH: Regul objects with setpoints and PID
- lastUpdated: last update timestamp
```

**`MasterData` Class**:
```csharp
- Command, PLCID, Time
- List<DataItem>: 3 conditions (hot, cold, ambient)
- Each DataItem: temperature, pressure, flow, regulations
```

#### Features

**WebSocket Server**:
- Listens on: 172.16.253.82:81
- Accepts multiple simultaneous connections
- 1024 byte buffer for messages

**Command Management** (`ReadData`):

| Command | Description |
|---------|-------------|
| 0 | REQ_PARAMS: Send aquarium parameters to client |
| 1 | REQ_DATA: Not used |
| 2 | SEND_PARAMS: Receive parameters from Arduino |
| 3 | SEND_DATA: Receive real-time data from Arduino |
| 4 | CALIBRATE_SENSOR: Not used |
| 6 | SEND_MASTER_DATA: Receive Master data |
| 7 | Frontend request: Send all aquariums to frontend |

**InfluxDB Storage** (`writeDataPointAsync`):
- Measurement: "Flumes"
- Tag: Aquarium ID or Condition ID
- Fields: temperature, pH, flow, setpoints, PID outputs, etc.
- Precision: seconds

**CSV Save** (`saveToFile`):
- Daily file: dataFileBasePath_YYYY-MM-DD.csv
- Automatic headers for 12 aquariums
- Line per interval (configurable in App.config)

**FTP Transfer** (`ftpTransfer`):
- Upload CSV files to FTP server
- Credentials in App.config

**Periodic Task** (`InitializeAsync`):
- Configurable interval (dataLogInterval in minutes)
- Executes saveData() periodically
- Uses CancellationToken for clean stop

#### WPF DataGrid

**ObservableCollection**:
- 20 pre-initialized aquariums
- Bidirectional binding with interface
- Automatic refresh upon data reception

**Security**:
- Checks that only one instance is running (Process.GetProcessesByName)
- Automatic shutdown if duplicate detected

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
  "rTemp": {
    "cons": 20.5,
    "sPID_pc": 45
  },
  "rpH": {
    "cons": 7.5,
    "sPID_pc": 23
  }
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
      "rTemp": {"cons": 30.0, "sPID_pc": 65},
      "rPression": {"cons": 0.2, "sPID_pc": 55}
    },
    {
      "CondID": 2,
      "temperature": 12.1,
      "pression": 1.22,
      "debit": 7.8,
      "rTemp": {"cons": 12.0, "sPID_pc": 42},
      "rPression": {"cons": 0.2, "sPID_pc": 51}
    },
    {
      "CondID": 3,
      "temperature": 18.5,
      "pH": 8.12,
      "pression": 0.95,
      "debit": 5.2,
      "rPression": {"cons": 0.2, "sPID_pc": 38}
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

---

## Network Configuration

### IP Addresses

| Controller | PLCID | IP | Function |
|------------|-------|-----|----------|
| MasterFlumes | 5 | 172.16.36.205 | Water conditioning |
| Aquarium | 7 | 172.16.36.207 | 3 aquariums |
| Flume | 7 | 172.16.36.207 | 4 flumes |
| SuperviFlume | - | 172.16.253.82:81 | WebSocket server |
| ESP32 Motors | - | flumemotors.local | Motor control |

### Modbus RTU

**Configuration**:
- Speed: 9600 baud
- Port: Serial3 (pin 46 direction)
- Timeout: 1000ms

**Sensor Addresses**:
- Hamilton pH/Temp: ID 1-13 (depending on system)
- PODOC O2: ID 10-13

---

## Deployment and Usage

### Prerequisites

**Arduino/IndustrialShields**:
- Arduino IDE or Visual Micro
- Libraries:
  - Ethernet
  - WebSockets
  - ModbusRtu
  - TimeLib
  - EEPROMex
  - ArduinoJson
  - RTC
  - PID_v1

**ESP32**:
- ESP32 Arduino Core
- Libraries:
  - ESP32Servo
  - ESPAsyncWebServer
  - ESPmDNS

**SuperviFlume C#**:
- Visual Studio 2019+
- .NET Framework 4.7.2
- NuGet packages:
  - Newtonsoft.Json
  - InfluxDB.Client

**InfluxDB**:
- InfluxDB 2.x installed on server
- Create organization, bucket and access token

### Installation

**1. InfluxDB Configuration**:

```bash
# Create organization and bucket
influx org create -n your_org
influx bucket create -n flumes_data -o your_org
influx auth create --org your_org --all-access
```

**2. SuperviFlume Configuration**:

Edit `App.config`:
```xml
<appSettings>
  <add key="InfluxDBToken" value="your_token_here"/>
  <add key="InfluxDBBucket" value="flumes_data"/>
  <add key="InfluxDBOrg" value="your_org"/>
  <add key="dataLogInterval" value="5"/>
  <add key="dataFileBasePath" value="C:/Data/flumes"/>
  <add key="ftpUsername" value="user"/>
  <add key="ftpPassword" value="pass"/>
  <add key="ftpDir" value="ftp.example.com/data"/>
</appSettings>
```

**3. Arduino Compilation**:

- Open each .ino project in Arduino IDE
- Select appropriate IndustrialShields board
- Check PLCID and SERVER_IP in code
- Compile and upload

**4. ESP32 Compilation**:

- Open ESP32_MoteurFlumes.ino
- Select ESP32 Dev Module board
- Upload

**5. Launch**:

1. Start InfluxDB
2. Launch SuperviFlume.exe
3. Click "Start Server"
4. Power up Arduino/ESP32 controllers
5. Check WebSocket connections in SuperviFlume

### Daily Usage

**Modifying Setpoints**:

Via web interface (flumeswebsite) or directly in SuperviFlume DataGrid:
- Temperature, pH setpoints
- PID parameters (Kp, Ki, Kd)
- Manual forcing mode

**Sensor Calibration**:

1. Immerse sensor in standard solution
2. Send calibration command (cmd: 4)
3. Wait for stabilization
4. Validate or cancel

**Monitoring**:

- SuperviFlume DataGrid: real-time
- InfluxDB/Grafana: history and graphs
- CSV files: daily export

**Alarms**:

- Water level alarm: automatically cuts ambient water
- WebSocket connection loss: displayed in SuperviFlume
- Modbus timeout: automatic retry

---

## Maintenance and Troubleshooting

### Diagnostics

**WebSocket Connection Problem**:

1. Check server IP in Arduino code (SERVER_IP)
2. Check Windows firewall on port 81
3. Test with telnet: `telnet 172.16.253.82 81`

**Modbus Problem**:

1. Check RS485 wiring (A, B, GND)
2. Check 120Ω terminations
3. Check sensor 24V power supply
4. Test baud rate (9600)

**Unstable Regulation**:

1. Reduce Kp (proportional gain)
2. Increase integral time (reduce Ki)
3. Check sensors and actuators
4. Check realistic setpoint

### Logs and Debug

**Arduino Serial Monitor**:
- Speed: 115200 baud
- Displays connection, sensor data, Modbus errors

**SuperviFlume MessageTextBox**:
- Displays all received WebSocket messages
- Automatic timestamp

**InfluxDB Logs**:
- Check point writing: `influx query 'from(bucket:"flumes_data") |> range(start: -1h)'`

### Backups

**Arduino EEPROM**:
- PID parameters and setpoints automatically saved
- Restoration on restart

**Database**:
- Regularly backup InfluxDB bucket
- Automatic daily CSV export

---

## File Structure

```
Flumes/
├── Aquarium/
│   └── Aquarium/
│       ├── Aquarium.ino          # Controls 3 aquariums
│       ├── Aqua.h                # Aqua + Regul class
│       └── ModbusSensor.h        # Modbus sensor interface
├── MasterFlumes/
│   └── MasterFlumes/
│       ├── MasterFlumes.ino      # Water conditioning
│       └── ModbusSensor.h        # Modbus sensor interface
├── Flume/
│   └── Flume/Flume/
│       ├── Flume.ino             # Controls 4 flumes
│       ├── Flume.h               # Flume + Regul class
│       └── ModbusSensor.h        # Modbus sensor interface
├── ESP32_MoteurFlumes/
│   └── ESP32_MoteurFlumes/
│       └── ESP32_MoteurFlumes.ino # Controls 8 ESC motors
├── SuperviFlume/
│   └── SuperviFlume/
│       ├── MainWindow.xaml.cs    # Main WebSocket server
│       ├── MainWindow.xaml       # WPF interface
│       └── App.config            # InfluxDB/FTP configuration
└── testServo/                    # Test projects
    testPression/
    testESC/
```

---

## Future Developments

### Proposed Improvements

1. **Security**:
   - WebSocket authentication
   - TLS/SSL encryption
   - User management and permissions

2. **Redundancy**:
   - Dual SuperviFlume server (failover)
   - Local storage on Arduino (SD card)

3. **Monitoring**:
   - Grafana dashboard
   - Email/SMS alerts
   - Centralized logs (ELK stack)

4. **Calibration**:
   - Calibration wizard in web interface
   - Scheduled periodic calibrations
   - Calibration history

5. **Analytics**:
   - Statistical calculations (mean, standard deviation)
   - Anomaly detection (ML)
   - Automatic reports

---

## Author and License

**Author**: Pierre (CNRS)

**Creation Date**: 2024

**License**: Internal research use

---

## Support

For any questions or issues:
1. Check Arduino logs (Serial Monitor)
2. Check SuperviFlume MessageTextBox
3. Consult sensor documentation (Hamilton, PODOC)
4. Contact technical team

---

## References

- **Web Frontend**: https://github.com/purrutti/flumeswebsite
- **InfluxDB**: https://docs.influxdata.com/
- **Arduino PID Library**: https://playground.arduino.cc/Code/PIDLibrary/
- **Hamilton Modbus**: pH sensor technical documentation
- **IndustrialShields**: https://www.industrialshields.com/
