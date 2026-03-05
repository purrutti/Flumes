/*
 Name:		Aquarium.ino
 Created:	05/04/2024 08:40:58
 Author:	pierr
*/
#include <Ethernet.h>
#include <WebSockets.h>
#include <WebSocketsClient.h>
#include <ModbusRtu.h>
#include <TimeLib.h>
#include <EEPROMex.h>
#include <ArduinoJson.h>
#include <RTC.h>
#include "Flume.h"
#include <DFRobot_GP8403.h>



const byte PLCID = 7;

/***** PIN ASSIGNMENTS *****/
const byte PIN_DEBITMETRE[4] = { 54,55,56,57 };
const byte PIN_V3VC[4] = { 4,6,9,0 };
const byte PIN_V3VF[4] = { 5,8,7,1 };
const byte PIN_CO2[4] = { 36,37,38,39 };
const byte PIN_VITESSE[4] = { 22,23,24,25 };

// Enter a MAC address for your controller below.
// Newer Ethernet shields have a MAC address printed on a sticker on the shield
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xBB, PLCID };

// Set the static IP address to use if the DHCP fails to assign
IPAddress ip(172, 16, 36, 200 + PLCID);

const char* SERVER_IP = "192.168.73.14";

WebSocketsClient webSocket;
ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI

DFRobot_GP8403 dac(&Wire, 0x5F);

Flume flume[4];

tempo tempoSensorRead;
tempo tempoRegul;
tempo tempoSendValues;
tempo tempoReadFlow;
tempo tempoReqParams;



tempo tempoMeanPID;


ModbusSensor mbSensor(1);



enum {
    REQ_PARAMS = 0,
    REQ_DATA = 1,
    SEND_PARAMS = 2,
    SEND_DATA = 3,
    CALIBRATE_SENSOR = 4,
    REQ_MASTER_DATA = 5,
    SEND_MASTER_DATA = 6
};


int state = 0;

typedef struct Calibration {
    int sensorID;
    int calibParam;
    float value;
    bool calibEnCours;
    bool calibRequested;
}Calibration;

Calibration calib;

int sensorIndex = 0;
bool pHSensor = true;


void webSocketEvent(WStype_t type, uint8_t* payload, size_t lenght) {
   // Serial.println(" WEBSOCKET EVENT:");
   // Serial.println(type);
    switch (type) {
    case WStype_DISCONNECTED:
        Serial.println(" Disconnected!");
        break;
    case WStype_CONNECTED:
        Serial.println(" Connected!");

        // send message to client
        webSocket.sendTXT("Connected");
        sendParams();
        break;
    case WStype_TEXT:

        //Serial.print(" Payload:"); Serial.println((char*)payload);
        readJSON((char*)payload);

        break;
    case WStype_ERROR:
        Serial.println(" ERROR!");
        break;
    }
}

unsigned long dateToTimestamp(int year, int month, int day, int hour, int minute, int second) {

    tmElements_t te;  //Time elements structure
    time_t unixTime; // a time stamp
    te.Day = day;
    te.Hour = hour;
    te.Minute = minute;
    te.Month = month;
    te.Second = second;
    te.Year = year - 1970;
    unixTime = makeTime(te);
    return unixTime;
}

void setRtcTimeFromCompileTime() {
    // Get compile date and time
    const char* compileDate = __DATE__;
    const char* compileTime = __TIME__;

    // Parse compile date
    int month, day, year;
    sscanf(compileDate, "%s %d %d", &month, &day, &year);

    // Parse compile time
    int hour, minute, second;
    sscanf(compileTime, "%d:%d:%d", &hour, &minute, &second);

    RTC.setTime(dateToTimestamp(year, month, day, hour, minute, second));
}



// the setup function runs once when you press reset or power the board
void setup() {
    Serial.begin(115200);
    master.begin(9600); // baud-rate at 19200
    master.setTimeOut(1000); // if there is no answer in 5000 ms, roll over
    Serial.println("START SETUP");
    dac.begin();
  while (dac.begin() != 0) {
        Serial.println("DAC init error");
        delay(1000);
    }
    
    Serial.println("DAC init success");
    //Set DAC output ranges
    dac.setDACOutRange(dac.eOutputRange10V);

    dac.setDACOutVoltage(0, 0);//The DAC value for 3.5V output in OUT0 channel
    dac.setDACOutVoltage(0, 1);//The DAC value for 3.5V output in OUT1 channel
    dac.store();

    int  startAddress = 10;
    for (int i = 0; i < 4; i++) {
        pinMode(PIN_DEBITMETRE[i], INPUT);
        pinMode(PIN_VITESSE[i], INPUT);
        if (i < 3) {
            pinMode(PIN_V3VC[i], OUTPUT);
            pinMode(PIN_V3VF[i], OUTPUT);
        }
        pinMode(PIN_CO2[i], OUTPUT);
        
        flume[i] = Flume(PLCID, PLCID * 4 - 12 + i + 1, PIN_DEBITMETRE[i], PIN_V3VC[i], PIN_V3VF[i], PIN_CO2[i], PIN_VITESSE[i]);
        flume[i].startAddress = startAddress;

        //Serial.println("Load address=" + String(startAddress));
        startAddress = flume[i].load();
        flume[i].previousMode = true;

        flume[i].regulpH.pid = PID((double*)&flume[i].pH, (double*)&flume[i].regulpH.sortiePID, (double*)&flume[i].regulpH.consigne, flume[i].regulpH.Kp, flume[i].regulpH.Ki, flume[i].regulpH.Kd, REVERSE);
        flume[i].regulpH.pid.SetOutputLimits(0, 100);
        flume[i].regulpH.pid.SetMode(AUTOMATIC);
        flume[i].regulTemp.pid = PID((double*)&flume[i].temperature, (double*)&flume[i].regulTemp.sortiePID, (double*)&flume[i].regulTemp.consigne, flume[i].regulTemp.Kp, flume[i].regulTemp.Ki, flume[i].regulTemp.Kd, DIRECT);
        //flume[i].regulTemp.pid.SetOutputLimits(50, 255);

        flume[i].regulTemp.pid.SetControllerDirection(DIRECT);
        flume[i].regulTemp.pid.SetOutputLimits(-255, 255);
        flume[i].regulTemp.pid.SetMode(AUTOMATIC);
    }
   


    Serial.println("ETHER BEGIN");
    Ethernet.begin(mac);
    Serial.println("ETHER");
    if (Ethernet.hardwareStatus() == EthernetNoHardware) {

        Serial.println("NO ETHERNET");
        while (true) {
            delay(1000); // do nothing, no point running without Ethernet hardware
            if (Ethernet.hardwareStatus() != EthernetNoHardware) break;
        }
    }



    Serial.println("Ethernet connected");

    Serial.print("localIP"); Serial.println(Ethernet.localIP());

    webSocket.begin(SERVER_IP, 81);
    //webSocket.begin("echo.websocket.org", 80);
    webSocket.onEvent(webSocketEvent);

    if (true) setRtcTimeFromCompileTime();
    RTC.read();
    //setPIDparams();

    tempoSensorRead.interval = 100;
    tempoRegul.interval = 100;
    tempoSendValues.interval = 5000;
    tempoSensorRead.debut = millis() + 2000;

    tempoReadFlow.interval = 5000;
    tempoReadFlow.debut = tempoSensorRead.debut;


    tempoMeanPID.debut = 1000;

    tempoMeanPID.interval = 5000;

    tempoReqParams.debut = 1000;
    tempoReqParams.interval = 10000;

    Serial.println("START");
}

/*
double regulTemp(int aquaID) {
    if (flume[aquaID].regulTemp.useOffset) flume[aquaID].regulTemp.consigne = tempAmbiante + flume[aquaID].regulTemp.offset;
    //Serial.println("AQUA ID:" + String(aquaID));
    if (flume[aquaID].temperature > flume[aquaID].regulTemp.consigne) {//froid
        if (aquaID != 3) analogWrite(flume[aquaID].pinV3VC, 255);
        flume[aquaID].regulTemp.pid.SetControllerDirection(DIRECT);
        flume[aquaID].regulTemp.pid.Compute();

        double output = flume[aquaID].regulTemp.sortiePID;
        if (output > 255) output = 255;
        if (output < 50) output = 50;

        flume[aquaID].regulTemp.sortiePID_pc = map(output, 50, 255, 0, 100);

        if (aquaID == 3) {
            //DAC
            int DACoutput = map(output, 50, 255, 0, 10000);

            dac.setDACOutVoltage(10000, 0);
            dac.setDACOutVoltage(DACoutput, 1);
            dac.store();
        }
        else analogWrite(flume[aquaID].pinV3VF, output);

        //Serial.println("value" + String(value));
        //Serial.println("SPID" + String(output));

    }
    else {//chaud

        if (aquaID != 3) analogWrite(flume[aquaID].pinV3VF, 255);
        flume[aquaID].regulTemp.pid.SetControllerDirection(REVERSE);
        flume[aquaID].regulTemp.pid.Compute();


        double output = flume[aquaID].regulTemp.sortiePID;
        if (output > 255) output = 255;
        if (output < 50) output = 50;
        flume[aquaID].regulTemp.sortiePID_pc = map(output, 50, 255, 100, 0);

        if (aquaID == 3) {
            //DAC

            int DACoutput = map(output, 50, 255, 0, 10000);

            dac.setDACOutVoltage(10000, 1);
            dac.setDACOutVoltage(DACoutput, 0);
            dac.store();
        }
        else analogWrite(flume[aquaID].pinV3VC, output);

        //Serial.println("value" + String(value));
        //Serial.println("SPID" + String(output));
    }
}*/


double regulTemp(int aquaID) {
    if (flume[aquaID].regulTemp.useOffset) flume[aquaID].regulTemp.consigne = tempAmbiante + flume[aquaID].regulTemp.offset;

        flume[aquaID].regulTemp.pid.Compute();

        /*Serial.println("FROID Computed SPID" + String(flume[aquaID].regulTemp.sortiePID));
        Serial.println("FROID Kp" + String(flume[aquaID].regulTemp.Kp));
        Serial.println("FROID Consigne" + String(flume[aquaID].regulTemp.consigne));
        Serial.println("FROID Temp" + String(flume[aquaID].temperature));*/
        /*double value = map(flume[aquaID].regulTemp.consigne, tempFroid, tempAmbiante, 0, 255);
        double output = flume[aquaID].regulTemp.sortiePID + value;*/
        double output = flume[aquaID].regulTemp.sortiePID;


        flume[aquaID].regulTemp.sortiePID_pc = map(output, -255, 255, -100, 100);

        if (flume[aquaID].regulTemp.sortiePID < 0) {//froid

            if (aquaID != 3)
            {
                analogWrite(flume[aquaID].pinV3VF, 255+output);
                analogWrite(flume[aquaID].pinV3VC, 255);
            }
            else {
                int DACoutput = map(-output, 255, 0, 0, 10000);

                dac.setDACOutVoltage(10000, 0);
                dac.setDACOutVoltage(DACoutput, 1);
                dac.store();
            }
        }
        else {//chaud
            if (aquaID != 3)
            {
                analogWrite(flume[aquaID].pinV3VC, 255-output);
                analogWrite(flume[aquaID].pinV3VF, 255);
            }
            else {
                int DACoutput = map(output, 255, 0, 0, 10000);

                dac.setDACOutVoltage(10000, 1);
                dac.setDACOutVoltage(DACoutput, 0);
                dac.store();
            }
        }
}

// the loop function runs over and over again until power down or reset
void loop() {
    readSensors();


    for (int i = 0; i < 4; i++) {

        flume[i].regulationpH();

        flume[i].readSpeed();
        regulTemp(i);

    }


    if (elapsed(&tempoReadFlow)) {
        for (int i = 0; i < 4; i++) {
            flume[i].readFlow();
        }
    }
    

    /*if (elapsed(&tempoReadFlow)) {
        for (int i = 0; i < 4; i++) {
            Serial.print("Temp" + String(flume[i].id) + ":"); Serial.println(flume[i].temperature);
            Serial.print("consigne:"); Serial.println(flume[i].regulTemp.consigne);
            Serial.print("sortie PID:"); Serial.println(flume[i].regulTemp.sortiePID);
        }
    }*/




    webSocket.loop();
    sendData();
    RTC.read();
    //reqParams();
}

void sendData() {
    if (elapsed(&tempoSendValues)) {
        Serial.println("SEND DATA");

        StaticJsonDocument<jsonDocSize> doc;
        for (int i = 0; i < 4; i++) {
            flume[i].serializeData(RTC.getTime(), PLCID, buffer);
            //Serial.println(buffer);
            webSocket.sendTXT(buffer);
        }
    }

}

void reqParams() {
    if (elapsed(&tempoReqParams)) {
        Serial.println("REQ PARAMS");
        for (int i = 0; i < 4; i++) webSocket.sendTXT("{\"cmd\":0,\"AquaID\":" + String(flume[i].id) + "}");
    }
}

void sendParams() {
    StaticJsonDocument<jsonDocSize> doc;
    Serial.println("SEND PARAMS");
    for (int i = 0; i < 4; i++) {
        flume[i].serializeParams(RTC.getTime(), PLCID, buffer);
        //Serial.println(buffer);
        webSocket.sendTXT(buffer);
    }

}


void readJSON(char* json) {
    StaticJsonDocument<jsonDocSize> doc;
    char buffer[bufferSize];
   // Serial.print("payload received:"); Serial.println(json);
    //deserializeJson(doc, json);

    DeserializationError error = deserializeJson(doc, json);

    if (error) {
        Serial.print(F("deserializeJson() failed: "));
        Serial.println(error.f_str());
        return;
    }

    uint8_t command = doc["cmd"];
    uint8_t destID = doc["PLCID"];
    uint8_t aquaID = doc["AquaID"];

    uint32_t time = doc["time"];
    if (time > 0) RTC.setTime(time);
    if (command == SEND_MASTER_DATA) {

        tempAmbiante = doc["tempAmbiante"];
        tempChaud = doc["tempChaud"];
        tempFroid = doc["tempFroid"];
        pHAmbiant = doc["pHAmbiant"];
    }
    else
        if (destID == PLCID) {
                if (command == 4) {
                Serial.println("CALIB REQ received");
                calib.sensorID = doc[F("sensorID")];
                calib.calibParam = doc[F("calibParam")];
                calib.value = doc[F("value")];

                Serial.print(F("calib.sensorID:")); Serial.println(calib.sensorID);
                Serial.print(F("calib.calibParam:")); Serial.println(calib.calibParam);
                Serial.print(F("calib.value:")); Serial.println(calib.value);

                calib.calibRequested = true;
            }
            switch (command) {
            case REQ_PARAMS:
                sendParams();
                //condition.serializeParams(buffer, RTC.getTime(),CONDID);
                //webSocket.sendTXT(buffer);
                break;
            case REQ_DATA:
                sendData();
                break;
            case SEND_PARAMS:
                Serial.println("AQUA ID:" + String(aquaID));
                int i = aquaID - (4 * PLCID - 12 + 1);
                if (i >= 0 && i < 4) {
                    flume[i].deserializeParams(doc);
                    flume[i].save();
                }

                break;
            case 4:
                /*
                TODO
                */
                
                break;
            default:
                Serial.println("DEFAULT");
                //webSocket.sendTXT(F("wrong request"));
                break;
            }
        }
}


void readSensors() {
    if (elapsed(&tempoSensorRead)) {
        //Serial.print("ReadSensors");

       // Serial.print("reading sensor" + String(sensorIndex + 1) + ":");
        if (state == 0 && calib.calibRequested) {
            calib.calibRequested = false;
            calib.calibEnCours = true;
        }
        if (calib.calibEnCours) {
            calibrateSensor();
        }
        else {
            switch (sensorIndex) {
            case 0: case 1: case 2: case 3:
                mbSensor.query.u8id = sensorIndex + 1;
                if (pHSensor) {

                    if (mbSensor.readPH(&master, &flume[sensorIndex].pH)) {
                        //Serial.print("pH" + String(sensorIndex + 1) + ":"); Serial.println(flume[sensorIndex].pH);
                        //Serial.print("consigne:"); Serial.println(aqua[sensorIndex].regulpH.consigne);
                        //Serial.print("sortie PID:"); Serial.println(aqua[sensorIndex].regulpH.sortiePID);
                        pHSensor = false;
                    }
                }
                else {
                    if (mbSensor.readTemp(&master, &flume[sensorIndex].temperature)) {
                        //Serial.print("TEMPERATURE " + String(sensorIndex + 1) + ":"); Serial.println(flume[sensorIndex].temperature);
                        //Serial.print("consigne:"); Serial.println(aqua[sensorIndex].regulTemp.consigne);
                        //Serial.print("sortie PID:"); Serial.println(aqua[sensorIndex].regulTemp.sortiePID);
                        sensorIndex++;
                        if (sensorIndex == 4) sensorIndex = 9;
                        pHSensor = true;
                    }
                }
                break;
            case 9:case 10:case 11:case 12:
                mbSensor.query.u8id = sensorIndex+1;//PODOC
                if (state == 0) {
                    if (mbSensor.requestValues(&master)) {
                        state = 1;
                    }
                }
                else if (mbSensor.readValues(&master, &flume[sensorIndex-9].O2)) {
                    //Serial.print(F("oxy %:")); Serial.println(flume[sensorIndex-9].O2);
                    state = 0;
                    sensorIndex++;
                }
                break;

            default:
                sensorIndex++;
                break;
            }
            if (sensorIndex > 12) sensorIndex = 0;

        }
    }
}

int HamiltonCalibStep = 0;

void calibrateSensor() {
    if (calib.sensorID < 10) {
        Serial.println("CALIBRATE pH");
        Serial.print("calib.value:"); Serial.println(calib.value);

        Serial.print("HamiltonCalibStep:"); Serial.println(HamiltonCalibStep);
        mbSensor.query.u8id = calib.sensorID;
        Serial.print("Hamilton.query.u8id:"); Serial.println(mbSensor.query.u8id);
        if (calib.calibParam == 99) {
            if (state == 0) {
                if (mbSensor.factoryReset(&master)) state = 1;
            }
            else {
                calib.calibEnCours = false;
                state = 0;
            }
        }
        else {
            HamiltonCalibStep = mbSensor.calibrate(calib.value, HamiltonCalibStep, &master);
            if (HamiltonCalibStep == 4) {
                HamiltonCalibStep = 0;
                calib.calibEnCours = false;
            }
        }

    }
    else {

        Serial.println("CALIBRATE PODOC");
        mbSensor.query.u8id = calib.sensorID;
        if (calib.calibParam == 99) {
            if (state == 0) {
                if (mbSensor.factoryReset(&master)) state = 1;
            }
            else {
                calib.calibEnCours = false;
                state = 0;
            }
        }
        else {
            int offset;
            if (state == 0) {
                //PODOC oxy
                if (calib.calibParam == 0) {
                    offset = 516;
                }
                else {
                    offset = 522;
                }
                if (mbSensor.calibrateCoeff(calib.value, offset, &master)) state = 1;
            }
            else {
                offset = 654;
                if (mbSensor.validateCalibration(offset, &master)) {
                    state = 0;
                    calib.calibEnCours = false;

                }
            }

        }

    }
    


}