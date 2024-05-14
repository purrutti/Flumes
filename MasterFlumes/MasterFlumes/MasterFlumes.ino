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
#include <PID_v1.h>


const char PROGMEM scmd[] = "cmd";
const char PROGMEM sPLCID[] = "PLCID";
const char PROGMEM stime[] = "time";

const char PROGMEM stemp[] = "temp";
const char PROGMEM spression[] = "pression";
const char PROGMEM sdebit[] = "debit";
const char PROGMEM sdata[] = "data";
const char PROGMEM rTemp[] = "rTemp";
const char PROGMEM rPression[] = "rPression";
const char PROGMEM scons[] = "cons";
const char PROGMEM sPID_pc[] = "sPID_pc";

const char PROGMEM sKp[] = "Kp";
const char PROGMEM sKi[] = "Ki";
const char PROGMEM sKd[] = "Kd";
const char PROGMEM saForcage[] = "aForcage";
const char PROGMEM sconsForcage[] = "consForcage";


char buffer[500];
const size_t jsonDocSize = 512;
const int bufferSize = 500;

const byte PLCID = 5;

/***** PIN ASSIGNMENTS *****/
const byte PIN_DEBITMETRE[3] = { 60,61,62 };//Chaud, froid, ambiant
const byte PIN_PRESSION[3] = { 54,56,55 };//Chaud, froid, ambiant
const byte PIN_V3VC = 9;
const byte PIN_V3VF = 8;
const byte PIN_V2VF = 4;
const byte PIN_V2VA = 5;
const byte PIN_V2VC = 6;
const byte PIN_TEMP_PAC_F = 57;
const byte PIN_TEMP_PAC_C = 58;

/*const byte PIN_DEBITMETRE_1 = 54;
const byte PIN_DEBITMETRE_2 = 55;
const byte PIN_DEBITMETRE_3 = 56;

const byte PIN_V3V_1C = 4;
const byte PIN_V3V_1F = 5;
const byte PIN_V3V_2C = 6;
const byte PIN_V3V_2F = 8;
const byte PIN_V3V_3C = 9;
const byte PIN_V3V_3F = 7;
const byte PIN_CO2_1 = 36;
const byte PIN_CO2_2 = 37;
const byte PIN_CO2_3 = 38;*/
/***************************/

// Enter a MAC address for your controller below.
// Newer Ethernet shields have a MAC address printed on a sticker on the shield
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, PLCID };

// Set the static IP address to use if the DHCP fails to assign
IPAddress ip(172, 16, 36, 200 + PLCID);

const char* SERVER_IP = "172.16.36.190";

WebSocketsClient webSocket;
ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI


typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;

tempo tempoSensorRead;
tempo tempoSendValues;



class Regul {
public:

    double sortiePID;
    double consigne;
    double Kp;
    double Ki;
    double Kd;
    double sortiePID_pc;
    bool autorisationForcage;
    int consigneForcage;
    double offset;
    PID pid;
    int startAddress;

    int save(int startAddress) {
        int add = startAddress;
        EEPROM.updateDouble(add, consigne); add += sizeof(double);
        EEPROM.updateDouble(add, Kp); add += sizeof(double);
        EEPROM.updateDouble(add, Ki); add += sizeof(double);
        EEPROM.updateDouble(add, Kd); add += sizeof(double);
        EEPROM.updateDouble(add, offset); add += sizeof(double);

        EEPROM.updateInt(add, autorisationForcage); add += sizeof(int);
        EEPROM.updateInt(add, consigneForcage); add += sizeof(int);
        return add;
    }

    int load(int startAddress) {
        int add = startAddress;
        consigne = EEPROM.readDouble(add); add += sizeof(double);
        Kp = EEPROM.readDouble(add); add += sizeof(double);
        Ki = EEPROM.readDouble(add); add += sizeof(double);
        Kd = EEPROM.readDouble(add); add += sizeof(double);
        offset = EEPROM.readDouble(add); add += sizeof(double);

        autorisationForcage = EEPROM.readInt(add); add += sizeof(int);
        consigneForcage = EEPROM.readInt(add); add += sizeof(int);
        return add;
    }
};


class Condition {
public:
    Regul regulPression;
    double pression;
    double debit;

    byte pinPression;
    byte pinDebit;

    byte pinV2V;

    Condition(byte pPression, byte pDebit, byte pV2V) {
        pinPression = pPression;
        pinDebit = pDebit;
        pinV2V = pV2V;
    }


    float readPressure(int lissage) {
        int ana = analogRead(pinPression); // 0-1023 value corresponding to 0-10 V corresponding to 0-20 mA
        //if using 330 ohm resistor so 20mA = 6.6V
        //int ana2 = ana * 10 / 6.6;
        int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
        int mbars = map(mA, 400, 2000, 0, 4000); //map to milli amps with 2 extra digits
        double anciennePression = pression;
        pression = ((double)mbars) / 1000.0; // pressure in bars
        pression = (lissage * pression + (100.0 - lissage) * anciennePression) / 100.0;
        return pression;
    }

    float readFlow(int lissage) {

        int ana = analogRead(pinDebit); // 0-1023 value corresponding to 0-5 V corresponding to 0-20 mA

        // Serial.print("debit ana:"); Serial.println(ana);
        int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
        //Serial.print("debit mA:"); Serial.println(ana);
        double ancientDebit = debit;
        debit = (9.375 * (mA - 400)) / 100.0; // flowrate in l/mn
        //debit = (lissage * debit + (100.0 - lissage) * ancientDebit) / 100.0;
        if (debit < 0) debit = 0;
        return debit;
    }
};
Condition eauChaude = Condition(PIN_PRESSION[0], PIN_DEBITMETRE[0], PIN_V2VC);
Condition eauFroide = Condition(PIN_PRESSION[1], PIN_DEBITMETRE[1], PIN_V2VF);
Condition eauAmbiante = Condition(PIN_PRESSION[2], PIN_DEBITMETRE[2], PIN_V2VA);

class PAC {
public:
    Regul regulTemp;
    double temperature;

    byte pinTemperature;
    byte pinV3V;

    PAC(byte pTemp, byte pV3V) {
        pinTemperature = pTemp;
        pinV3V = pV3V;
    }

    double readTemp() {
        int ana = analogRead(pinTemperature); // 0-1023 value corresponding to 0-10 V corresponding to 0-20 mA
        //if using 330 ohm resistor so 20mA = 6.6V
        //int ana2 = ana * 10 / 6.6;
        int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
        int t = map(mA, 400, 2000, 0, 5000); //map to 0-50.00°C
        double temp = ((double)t) / 100.0;
        return temp;
    }
};
PAC PACChaud = PAC(PIN_TEMP_PAC_C, PIN_V3VC);
PAC PACFroid = PAC(PIN_TEMP_PAC_F, PIN_V3VF);

enum {
    REQ_PARAMS = 0,
    REQ_DATA = 1,
    SEND_PARAMS = 2,
    SEND_DATA = 3,
    CALIBRATE_SENSOR = 4,
    REQ_MASTER_DATA = 5,
    SEND_MASTER_DATA = 6
};


void webSocketEvent(WStype_t type, uint8_t* payload, size_t lenght) {
    Serial.println(" WEBSOCKET EVENT:");
    Serial.println(type);
    switch (type) {
    case WStype_DISCONNECTED:
        Serial.println(" Disconnected!");
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


// the setup function runs once when you press reset or power the board
void setup() {
    Serial.begin(115200);

    
    
    Serial.println("START SETUP");
    for (int i = 0; i < 3; i++) {

        Serial.print("PRESSION " + String(i) + ":"); Serial.println(analogRead(PIN_PRESSION[i]));
        Serial.print("DEBIT " + String(i) + ":"); Serial.println(analogRead(PIN_DEBITMETRE[i]));

        
    }
    analogWrite(PIN_V2VF, 255);//Froid
    analogWrite(PIN_V2VA, 255);//Ambiant
    analogWrite(PIN_V2VC, 255);//Chaud


    analogWrite(PIN_V3VC, 100);
    analogWrite(PIN_V3VF, 200);

   


    Ethernet.begin(mac, ip);
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


    RTC.read();
    //setPIDparams();

    Serial.println("START");
    tempoSensorRead.interval = 1000;
    tempoSendValues.interval = 1000;
}


// the loop function runs over and over again until power down or reset
void loop() {
    webSocket.loop();
    if (elapsed(&tempoSensorRead)) {

        eauChaude.readFlow(1);
        eauFroide.readFlow(1);
        eauAmbiante.readFlow(1);

        eauChaude.readPressure(1);
        eauFroide.readPressure(1);
        eauAmbiante.readPressure(1);

        PACChaud.readTemp();
        PACFroid.readTemp();

        Serial.print("Pression Chaud:"); Serial.println(eauChaude.pression);
        Serial.print("Pression Froid:"); Serial.println(eauFroide.pression);
        Serial.print("Pression Ambiant:"); Serial.println(eauAmbiante.pression);


        Serial.print("Debit Chaud:"); Serial.println(eauChaude.debit);
        Serial.print("Debit Froid:"); Serial.println(eauFroide.debit);
        Serial.print("Debit Ambiant:"); Serial.println(eauAmbiante.debit);

        Serial.print("TEMP CHAUD:"); Serial.println(PACChaud.temperature);
        Serial.print("TEMP FROID:"); Serial.println(PACFroid.temperature);

        sendData();

    }
    
}


void sendData() {
    StaticJsonDocument<jsonDocSize> doc;
    //if (elapsed(&tempoSendValues)) {
        Serial.println("SEND DATA");
        for (int i = 0; i < 3; i++) {
            serializeData(RTC.getTime(), PLCID, buffer);
            Serial.println(buffer);
            webSocket.sendTXT(buffer);
        }
    //}

}

bool elapsed(tempo* t) {
    if (t->debut == 0) {
        t->debut = millis();
    }
    else {
        if ((unsigned long)(millis() - t->debut) >= t->interval) {
            t->debut = 0;
            return true;
        }
    }
    return false;
}


void sendParams() {
    StaticJsonDocument<jsonDocSize> doc;
    Serial.println("SEND PARAMS");
    for (int i = 0; i < 3; i++) {
        //serializeParams(RTC.getTime(), PLCID, buffer);
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
                //deserializeParams(doc);
                //save();
            }
            break;
        default:
            //webSocket.sendTXT(F("wrong request"));
            break;
        }
    }
}



bool serializeData(uint32_t timeString, uint8_t sender, char* buffer) {
    //Serial.println("SENDDATA");
    //DynamicJsonDocument doc(512);

    StaticJsonDocument<512> doc;

    doc[scmd] = 3;
    doc[sPLCID] = String(sender);


    JsonArray data = doc[sdata].to<JsonArray>();

    JsonObject data_0 = data.add<JsonObject>();
    data_0["CondID"] = "1";
    data_0[stemp] = PACChaud.temperature;
    data_0[spression] = eauChaude.pression;
    data_0[sdebit] = eauChaude.debit;

    JsonObject data_0_rTemp = data_0[rTemp].to<JsonObject>();
    data_0_rTemp[scons] = PACChaud.regulTemp.consigne;
    data_0_rTemp[sPID_pc] = PACChaud.regulTemp.sortiePID_pc;

    JsonObject data_0_rPression = data_0[rPression].to<JsonObject>();
    data_0_rPression[scons] = eauChaude.regulPression.consigne;
    data_0_rPression[sPID_pc] = eauChaude.regulPression.sortiePID_pc;

    JsonObject data_1 = data.add<JsonObject>();
    data_1["CondID"] = "2";
    data_0[stemp] = PACFroid.temperature;
    data_0[spression] = eauFroide.pression;
    data_0[sdebit] = eauFroide.debit;

    JsonObject data_1_rTemp = data_1[rTemp].to<JsonObject>();
    data_0_rTemp[scons] = PACFroid.regulTemp.consigne;
    data_0_rTemp[sPID_pc] = PACFroid.regulTemp.sortiePID_pc;

    JsonObject data_1_rPression = data_1[rPression].to<JsonObject>();
    data_0_rPression[scons] = eauFroide.regulPression.consigne;
    data_0_rPression[sPID_pc] = eauFroide.regulPression.sortiePID_pc;

    JsonObject data_2 = data.add<JsonObject>();
    data_2["CondID"] = "3";
    data_0[spression] = eauAmbiante.pression;
    data_0[sdebit] = eauAmbiante.debit;

    JsonObject data_2_rPression = data_2[rPression].to<JsonObject>();
    data_0_rPression[scons] = eauAmbiante.regulPression.consigne;
    data_0_rPression[sPID_pc] = eauAmbiante.regulPression.sortiePID_pc;
    doc[stime] = timeString;


    serializeJson(doc, buffer, bufferSize);

    return true;
}
/*
bool serializeParams(uint32_t timeString, uint8_t sender, char* buffer) {
    StaticJsonDocument<300> doc;
    //Serial.println(F("SEND PARAMS"));

    doc[scmd] = 2;
    doc[sPLCID] = String(sender);
    doc[sID] = String(id);
    doc[stime] = timeString;

    JsonObject regulT = doc.createNestedObject(F("rTemp"));
    regulT[scons] = regulTemp.consigne;
    regulT[sKp] = regulTemp.Kp;
    regulT[sKi] = regulTemp.Ki;
    regulT[sKd] = regulTemp.Kd;
    if (this->regulTemp.autorisationForcage) regulT[saForcage] = "true";
    else regulT[saForcage] = "false";
    regulT[sconsForcage] = regulTemp.consigneForcage;

    JsonObject regulp = doc.createNestedObject(F("rpH"));
    regulp[scons] = regulpH.consigne;
    regulp[sKp] = regulpH.Kp;
    regulp[sKi] = regulpH.Ki;
    regulp[sKd] = regulpH.Kd;
    if (regulpH.autorisationForcage) regulp[saForcage] = "true";
    else regulp[saForcage] = "false";
    regulp[sconsForcage] = regulpH.consigneForcage;
    serializeJson(doc, buffer, bufferSize);
}

void deserializeParams(StaticJsonDocument<jsonDocSize> doc) {

    JsonObject regulp = doc[rpH];
    regulpH.consigne = regulp[scons]; // 24.2
    regulpH.Kp = regulp[sKp]; // 2.1
    regulpH.Ki = regulp[sKi]; // 2.1
    regulpH.Kd = regulp[sKd]; // 2.1
    const char* regulpH_autorisationForcage = regulp[saForcage];
    if (strcmp(regulpH_autorisationForcage, "true") == 0 || strcmp(regulpH_autorisationForcage, "True") == 0) regulpH.autorisationForcage = true;
    else regulpH.autorisationForcage = false;
    regulpH.consigneForcage = regulp[sconsForcage]; // 2.1

    JsonObject regulT = doc[rTemp];

    regulTemp.consigne = regulT[scons]; // 24.2
    regulTemp.Kp = regulT[sKp]; // 2.1
    regulTemp.Ki = regulT[sKi]; // 2.1
    regulTemp.Kd = regulT[sKd]; // 2.1
    const char* regulTemp_autorisationForcage = regulT[saForcage];
    if (strcmp(regulTemp_autorisationForcage, "true") == 0 || strcmp(regulTemp_autorisationForcage, "True") == 0) regulTemp.autorisationForcage = true;
    else regulTemp.autorisationForcage = false;
    regulTemp.consigneForcage = regulT[sconsForcage]; // 2.1

}
*/
