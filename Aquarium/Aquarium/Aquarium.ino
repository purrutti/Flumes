/*
 Name:		Aquarium.ino
 Created:	05/04/2024 08:40:58
 Author:	pierr
*/
#include <Ethernet.h>
#include <WebSockets.h>
#include <WebSocketsClient.h>
#include <WebSocketsServer.h>
#include <ModbusRtu.h>
#include <TimeLib.h>
#include <EEPROMex.h>
#include <ArduinoJson.h>
#include <RTC.h>
#include "Aqua.h"



const byte PLCID = 14;

/***** PIN ASSIGNMENTS *****/
const byte PIN_DEBITMETRE[3] = { 54,55,56 };
const byte PIN_V3VC[3] = { 4,6,9 };
const byte PIN_V3VF[3] = { 5,8,7 };
const byte PIN_CO2[3] = { 36,37,38 };

// Enter a MAC address for your controller below.
// Newer Ethernet shields have a MAC address printed on a sticker on the shield
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, PLCID };

// Set the static IP address to use if the DHCP fails to assign
IPAddress ip(192, 168, 1, 160 + PLCID);

const char* SERVER_IP = "192.168.1.134";

WebSocketsClient webSocket;
ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI



Aqua aqua[3];

tempo tempoSensorRead;
tempo tempoRegul;
tempo tempoSendValues;
tempo tempoReadFlow;

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
    Serial.println(" WEBSOCKET EVENT:");
    Serial.println(type);
    switch (type) {
    case WStype_DISCONNECTED:
        //Serial.print(num); Serial.println(" Disconnected!");
        break;
    case WStype_CONNECTED:
        Serial.println(" Connected!");

        // send message to client
        webSocket.sendTXT("Connected");
        break;
    case WStype_TEXT:

        Serial.print(" Payload:"); Serial.println((char*)payload);
        readJSON((char*)payload);

        break;
    case WStype_ERROR:
        Serial.println(" ERROR!");
        break;
    }
}

unsigned long dateToTimestamp(int year, int month, int day, int hour, int minute) {

    tmElements_t te;  //Time elements structure
    time_t unixTime; // a time stamp
    te.Day = day;
    te.Hour = hour;
    te.Minute = minute;
    te.Month = month;
    te.Second = 0;
    te.Year = year - 1970;
    unixTime = makeTime(te);
    return unixTime;
}



double mesure, consigne = 17.0, sortie, kp = 10 , ki=100, kd=0;

//PID pid;
// the setup function runs once when you press reset or power the board
void setup() {
    Serial.begin(115200);
    master.begin(9600); // baud-rate at 19200
    master.setTimeOut(1000); // if there is no answer in 5000 ms, roll over
    Serial.println("START SETUP");

    for (int i = 0; i < 3; i++) {
        pinMode(PIN_DEBITMETRE[i], INPUT);
        pinMode(PIN_V3VC[i], OUTPUT);
        pinMode(PIN_V3VF[i], OUTPUT);
        pinMode(PIN_CO2[i], OUTPUT);
        aqua[i] = Aqua(PLCID,i+1, PIN_DEBITMETRE[i], PIN_V3VC[i], PIN_V3VF[i], PIN_CO2[i]);
        aqua[i].previousMode = true;

        aqua[i].regulpH.pid = PID((double*)&aqua[i].pH, (double*)&aqua[i].regulpH.sortiePID, (double*)&aqua[i].regulpH.consigne, aqua[i].regulpH.Kp, aqua[i].regulpH.Ki, aqua[i].regulpH.Kd, REVERSE);
        aqua[i].regulpH.pid.SetOutputLimits(0, 100);
        aqua[i].regulpH.pid.SetMode(AUTOMATIC);
        aqua[i].regulTemp.pid = PID((double*)&aqua[i].temperature, (double*)&aqua[i].regulTemp.sortiePID, (double*)&aqua[i].regulTemp.consigne, aqua[i].regulTemp.Kp, aqua[i].regulTemp.Ki, aqua[i].regulTemp.Kd, DIRECT);
        aqua[i].regulTemp.pid.SetOutputLimits(0, 255);
        aqua[i].regulTemp.pid.SetMode(AUTOMATIC);
    }

    

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

    RTC.read();
    //setPIDparams();

    tempoSensorRead.interval = 500;
    tempoRegul.interval = 100;
    tempoSendValues.interval = 5000;
    tempoSensorRead.debut = millis() + 2000;

    tempoReadFlow.interval = 5000;

    Serial.println("START");

    aqua[0].regulTemp.consigne = 14.0;
    aqua[1].regulTemp.consigne = 22.0;
    aqua[2].regulTemp.consigne = 25.0;
    aqua[0].regulpH.consigne = 6.0;
    aqua[1].regulpH.consigne = 7.0;
    aqua[2].regulpH.consigne = 8.0;


    


}


// the loop function runs over and over again until power down or reset
void loop() {
    readSensors();


    for (int i = 0; i < 3; i++) {
        //pid.Compute();

        aqua[i].regulationpH();
        if (aqua[i].temperature > aqua[i].regulTemp.consigne) aqua[i].regulationTemperature(false);
        else aqua[i].regulationTemperature(true);

    }


    webSocket.loop();
    //sendData();
}

void sendData() {
    StaticJsonDocument<jsonDocSize> doc;
    if (elapsed(&tempoSendValues)) {
        Serial.println("SEND DATA");
        for (int i = 0; i < 3; i++) {
            aqua[i].serializeData(RTC.getTime(), aqua[i].id, buffer);
            Serial.println(buffer);
            webSocket.sendTXT(buffer);
        }
    }

}

void sendParams() {
    StaticJsonDocument<jsonDocSize> doc;
        Serial.println("SEND PARAMS");
        for (int i = 0; i < 3; i++) {
            aqua[i].serializeParams(RTC.getTime(), aqua[i].id, buffer);
            Serial.println(buffer);
            webSocket.sendTXT(buffer);
        }

}


void readJSON(char* json) {
    StaticJsonDocument<jsonDocSize> doc;
    char buffer[bufferSize];
    Serial.print("payload received:"); Serial.println(json);
    //deserializeJson(doc, json);

    DeserializationError error = deserializeJson(doc, json);

    if (error) {
        Serial.print(F("deserializeJson() failed: "));
        Serial.println(error.f_str());
        return;
    }

    uint8_t command = doc["cmd"];
    uint8_t destID = doc["PLCID"];
    uint8_t senderID = doc["sID"];

    uint32_t time = doc["time"];
    if (time > 0) RTC.setTime(time);
    if (destID == PLCID) {
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
            for (int i = 0; i < 3; i++) {
                aqua[i].deserializeParams(doc);
                aqua[i].save();
            }
            break;
        case CALIBRATE_SENSOR:
            /*
            TODO
            */
            Serial.println(F("CALIB REQ received"));
            calib.sensorID = doc[F("sensorID")];
            calib.calibParam = doc[F("calibParam")];
            calib.value = doc[F("value")];

            Serial.print(F("calib.sensorID:")); Serial.println(calib.sensorID);
            Serial.print(F("calib.calibParam:")); Serial.println(calib.calibParam);
            Serial.print(F("calib.value:")); Serial.println(calib.value);
            /*if (condID == CONDID) {
                calib.calibRequested = true;
            }*/
            break;
        default:
            //webSocket.sendTXT(F("wrong request"));
            break;
        }
    }
}


void readSensors() {
    if (elapsed(&tempoSensorRead)) {

        if (state == 0 && calib.calibRequested) {
            calib.calibRequested = false;
            calib.calibEnCours = true;
        }
        if (calib.calibEnCours) {
            //calibrateSensor();
        }
        else {
            if (sensorIndex < 3) { // HAMILTON: indexes O to 2 are mesocosms
                mbSensor.query.u8id = sensorIndex + 1;
                if (pHSensor) {

                    if (mbSensor.readPH(&master,&aqua[sensorIndex].pH)) {
                        aqua[sensorIndex].readFlow(1);
                        Serial.print("pH" + String(sensorIndex + 1) + ":"); Serial.println(aqua[sensorIndex].pH);
                        Serial.print("consigne:"); Serial.println(aqua[sensorIndex].regulpH.consigne);
                        Serial.print("sortie PID:"); Serial.println(aqua[sensorIndex].regulpH.sortiePID);
                        pHSensor = false;
                    }
                }
                else {
                    if (mbSensor.readTemp(&master, &aqua[sensorIndex].temperature)) {
                        Serial.print("Temp"+String(sensorIndex+1)+":"); Serial.println(aqua[sensorIndex].temperature);
                        Serial.print("consigne:"); Serial.println(aqua[sensorIndex].regulTemp.consigne);
                        Serial.print("sortie PID:"); Serial.println(aqua[sensorIndex].regulTemp.sortiePID);
                        sensorIndex++;
                        if (sensorIndex == 3) sensorIndex = 0;
                        pHSensor = true;
                    }
                }
            }
            /*else {
                switch (sensorIndex) {
                case 3:
                    mbSensor.query.u8id = 10;//PODOC
                    if (state == 0) {
                        if (mbSensor.requestValues(&master)) {
                            state = 1;
                        }
                    }
                    else if (mbSensor.readValues(&master, &aqua[0].O2)) {
                        Serial.print(F("oxy %:")); Serial.println(aqua[0].O2);
                        state = 0;
                        sensorIndex++;
                    }
                    break;
                case 4:
                    mbSensor.query.u8id = 11;//PODOC
                    if (state == 0) {
                        if (mbSensor.requestValues(&master)) {
                            state = 1;
                        }
                    }
                    else if (mbSensor.readValues(&master, &aqua[1].O2)) {
                        Serial.print(F("oxy %:")); Serial.println(aqua[1].O2);
                        state = 0;
                        sensorIndex++;
                    }
                    break;
                case 5:
                    mbSensor.query.u8id = 12;//PODOC
                    if (state == 0) {
                        if (mbSensor.requestValues(&master)) {
                            state = 1;
                        }
                    }
                    else if (mbSensor.readValues(&master, &aqua[2].O2)) {
                        Serial.print(F("oxy %:")); Serial.println(aqua[2].O2);
                        state = 0;
                        sensorIndex=0;
                    }
                    break;
                }

            }*/
        }
    }
}

int HamiltonCalibStep = 0;

/*void calibrateSensor() {
    Serial.println("CALIBRATE PH");
    Serial.print("calib.value:"); Serial.println(calib.value);

    Serial.print("HamiltonCalibStep:"); Serial.println(HamiltonCalibStep);
    aqua[calib.sensorID].Hamilton.query.u8id = calib.sensorID + 1;
    Serial.print("Hamilton.query.u8id:"); Serial.println(aqua[calib.sensorID].Hamilton.query.u8id);
    if (calib.calibParam == 99) {
        if (state == 0) {
            if (aqua[calib.sensorID].Hamilton.factoryReset(&master)) state = 1;
        }
        else {
            calib.calibEnCours = false;
            state = 0;
        }
    }
    else {
        HamiltonCalibStep = aqua[calib.sensorID].Hamilton.calibrate(calib.value, HamiltonCalibStep, &master);
        if (HamiltonCalibStep == 4) {
            HamiltonCalibStep = 0;
            calib.calibEnCours = false;
        }
    }

}*/