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


const char scmd[] = "cmd";
const char sPLCID[] = "PLCID";
const char stime[] = "time";

const char stemp[] = "temp";
const char spression[] = "pression";
const char sdebit[] = "debit";
const char sdata[] = "data";
const char rTemp[] = "rTemp";
const char rPression[] = "rPression";
const char scons[] = "cons";
const char sPID_pc[] = "sPID_pc";

const char sKp[] = "Kp";
const char sKi[] = "Ki";
const char sKd[] = "Kd";
const char saForcage[] = "aForcage";
const char sconsForcage[] = "consForcage";


char buffer[500];
const size_t jsonDocSize = 512;
const int bufferSize = 500;

const byte PLCID = 5;

/***** PIN ASSIGNMENTS *****/
const byte PIN_DEBITMETRE[3] = { 60,61,62 };//Chaud, froid, ambiant
const byte PIN_PRESSION[3] = { 54,56,55 };//Chaud, froid, ambiant
const byte PIN_V3VC = 9;
const byte PIN_V3VF = 8;
const byte PIN_V2VF = 6;
const byte PIN_V2VA = 5;
const byte PIN_V2VC = 4;
const byte PIN_TEMP_PAC_F = 57;
const byte PIN_TEMP_PAC_C = 58;
const byte PIN_NIVEAU = 59;

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

const char* SERVER_IP = "192.168.73.14";

WebSocketsClient webSocket;
ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI


typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;

tempo tempoSensorRead;
tempo tempoSendValues;

double minPression=0.15;



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
    Regul() {
        Kp = 20;
        Ki = 10;
        Kd = 5;
        consigne = 1.0;
    }

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
        regulPression = Regul();

        regulPression.Kp = 100;
        regulPression.Ki = 10;
        regulPression.Kd = 20;
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
        Serial.print("debit ana:"); Serial.println(ana);
        Serial.print("debit mA:"); Serial.println(mA);
        double ancientDebit = debit;
        debit = (9.375 * (mA - 394)) / 100.0; // flowrate in l/mn
        Serial.print("debit l/mn:"); Serial.println(debit);
        //debit = (lissage * debit + (100.0 - lissage) * ancientDebit) / 100.0;
        if (debit < 0) debit = 0;
        return debit;
    }

    double regulationPression(double mesure) {

            regulPression.pid.Compute();
            regulPression.sortiePID_pc = (int)(regulPression.sortiePID * 100 / 255);

            analogWrite(pinV2V, (int)regulPression.sortiePID);
        
        return regulPression.sortiePID;
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


        regulTemp.Kp = 50;
        regulTemp.Ki = 1;
        regulTemp.Kd = 20;
    }

    double readTemp() {
        int ana = analogRead(pinTemperature); // 0-1023 value corresponding to 0-24 V , 0-10V corresponding to 0-20 mA
        //Serial.print("ana:"); Serial.println(ana);
        //if using 500 ohm resistor so 20mA = 10V
        int V = map(ana, 0, 1023, 0, 100); //map to milli amps with 2 extra digits
        //Serial.print("V:"); Serial.println(V);
        int mA = map(V, 0, 100, 0, 2000); //map to milli amps with 2 extra digits
        //Serial.print("mA:"); Serial.println(mA);
        int t = map(mA, 400, 2000, 0, 5000); //map to 0-50.00°C
       // Serial.print("temp:"); Serial.println(t/100.0);
        double temp = ((double)t) / 100.0;
        temperature = temp;
        return temp;
    }

    double regulTemperature() {

        regulTemp.pid.Compute();
        regulTemp.sortiePID_pc = (int)(regulTemp.sortiePID * 100 / 255);

        analogWrite(pinV3V, (int)regulTemp.sortiePID);

        return regulTemp.sortiePID;
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
bool alarmeNiveau = false;
bool checkNiveau() {
    if (digitalRead(PIN_NIVEAU)) {
        alarmeNiveau = true;
        analogWrite(PIN_V2VA, LOW);
    }else alarmeNiveau = false;
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


    //analogWrite(PIN_V3VC, 100);
    //analogWrite(PIN_V3VF, 200);

   


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
    webSocket.onEvent(webSocketEvent);


    RTC.read();
    //setPIDparams();

    Serial.println("START");
    tempoSensorRead.interval = 1000;
    tempoSendValues.interval = 1000;
    eauAmbiante.regulPression.consigne = 50.0;
    eauAmbiante.regulPression.Kp = 50;
    eauAmbiante.regulPression.Ki = 1;
    eauAmbiante.regulPression.Kd = 20;
    eauChaude.regulPression.pid = PID((double*)&eauChaude.pression, (double*)&eauChaude.regulPression.sortiePID, (double*)&eauChaude.regulPression.consigne, eauChaude.regulPression.Kp, eauChaude.regulPression.Ki, eauChaude.regulPression.Kd, DIRECT);
    eauFroide.regulPression.pid = PID((double*)&eauFroide.pression, (double*)&eauFroide.regulPression.sortiePID, (double*)&eauFroide.regulPression.consigne, eauFroide.regulPression.Kp, eauFroide.regulPression.Ki, eauFroide.regulPression.Kd, DIRECT);
    eauAmbiante.regulPression.pid = PID((double*)&eauAmbiante.debit, (double*)&eauAmbiante.regulPression.sortiePID, (double*)&eauAmbiante.regulPression.consigne, eauAmbiante.regulPression.Kp, eauAmbiante.regulPression.Ki, eauAmbiante.regulPression.Kd, DIRECT);
    eauChaude.regulPression.pid.SetOutputLimits(50, 255);
    eauChaude.regulPression.pid.SetMode(AUTOMATIC);
    eauFroide.regulPression.pid.SetOutputLimits(50, 255);
    eauFroide.regulPression.pid.SetMode(AUTOMATIC);
    eauAmbiante.regulPression.pid.SetOutputLimits(50, 255);
    eauAmbiante.regulPression.pid.SetSampleTime(1000);
    eauAmbiante.regulPression.pid.SetMode(AUTOMATIC);

    PACChaud.regulTemp.pid = PID((double*)&PACChaud.temperature, (double*)&PACChaud.regulTemp.sortiePID, (double*)&PACChaud.regulTemp.consigne, PACChaud.regulTemp.Kp, PACChaud.regulTemp.Ki, PACChaud.regulTemp.Kd, DIRECT);
    PACFroid.regulTemp.pid = PID((double*)&PACFroid.temperature, (double*)&PACFroid.regulTemp.sortiePID, (double*)&PACFroid.regulTemp.consigne, PACFroid.regulTemp.Kp, PACFroid.regulTemp.Ki, PACFroid.regulTemp.Kd, REVERSE);
    PACChaud.regulTemp.consigne = 25.0;
    PACFroid.regulTemp.consigne = 14.0;

    PACChaud.regulTemp.pid.SetOutputLimits(50, 255);
    PACChaud.regulTemp.pid.SetMode(AUTOMATIC);
    PACFroid.regulTemp.pid.SetOutputLimits(50, 255);
    PACFroid.regulTemp.pid.SetMode(AUTOMATIC);

}


// the loop function runs over and over again until power down or reset
void loop() {
    checkNiveau();
    webSocket.loop();
    if (elapsed(&tempoSensorRead)) {

        eauChaude.readFlow(1);
        eauFroide.readFlow(1);
        eauAmbiante.readFlow(1);

        eauChaude.readPressure(1);
        eauFroide.readPressure(1);
        eauAmbiante.readPressure(1);
        minPression = max(min(eauChaude.pression, eauFroide.pression), 0.15);

        eauFroide.regulPression.consigne = minPression;
        eauChaude.regulPression.consigne = minPression;

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

    if (!alarmeNiveau) {
        eauAmbiante.regulationPression(eauAmbiante.debit);
    }
    eauChaude.regulationPression(eauChaude.pression);
    eauFroide.regulationPression(eauFroide.pression);

    PACChaud.regulTemperature();
    PACFroid.regulTemperature();
    
}


void sendData() {
    //if (elapsed(&tempoSendValues)) {
        Serial.println("SEND DATA");
            serializeData(RTC.getTime(), PLCID, buffer);
            Serial.println(buffer);
            webSocket.sendTXT(buffer);
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

void reqParams() {
    Serial.println("REQ PARAMS");
    webSocket.sendTXT("{\"cmd\":0,\"AquaID\":0}");

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
    uint8_t senderID = doc["AquaID"];

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
            deserializeParams(doc);
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

    doc[scmd] = SEND_MASTER_DATA;
    doc[sPLCID] = String(sender);


    JsonArray data = doc[sdata].to<JsonArray>();

    JsonObject data_0 = data.add<JsonObject>();
    data_0["CondID"] = "1";

    data_0[stemp] = serialized(String((int)(PACChaud.temperature * 100 + 0.5) / 100.0, 2));
    data_0[spression] = serialized(String((int)(eauChaude.pression * 100 + 0.5) / 100.0, 2));
    data_0[sdebit] = serialized(String((int)(eauChaude.debit * 100 + 0.5) / 100.0, 2));

    JsonObject data_0_rTemp = data_0[rTemp].to<JsonObject>();
    data_0_rTemp[scons] = PACChaud.regulTemp.consigne;
    data_0_rTemp[sPID_pc] = PACChaud.regulTemp.sortiePID_pc;

    JsonObject data_0_rPression = data_0[rPression].to<JsonObject>();
    data_0_rPression[scons] = eauChaude.regulPression.consigne;
    data_0_rPression[sPID_pc] = eauChaude.regulPression.sortiePID_pc;

    JsonObject data_1 = data.add<JsonObject>();
    data_1["CondID"] = "2";
    data_1[stemp] = serialized(String((int)(PACFroid.temperature * 100 + 0.5) / 100.0, 2));
    data_1[spression] = serialized(String((int)(eauFroide.pression * 100 + 0.5) / 100.0, 2));
    data_1[sdebit] = serialized(String((int)(eauFroide.debit * 100 + 0.5) / 100.0, 2));

    JsonObject data_1_rTemp = data_1[rTemp].to<JsonObject>();
    data_1_rTemp[scons] = PACFroid.regulTemp.consigne;
    data_1_rTemp[sPID_pc] = PACFroid.regulTemp.sortiePID_pc;

    JsonObject data_1_rPression = data_1[rPression].to<JsonObject>();
    data_1_rPression[scons] = eauFroide.regulPression.consigne;
    data_1_rPression[sPID_pc] = eauFroide.regulPression.sortiePID_pc;

    JsonObject data_2 = data.add<JsonObject>();
    data_2["CondID"] = "3";
    data_2[spression] = serialized(String((int)(eauAmbiante.pression * 100 + 0.5) / 100.0, 2));
    data_2[sdebit] = serialized(String((int)(eauAmbiante.debit * 100 + 0.5) / 100.0, 2));

    JsonObject data_2_rPression = data_2[rPression].to<JsonObject>();
    data_2_rPression[scons] = eauAmbiante.regulPression.consigne;
    data_2_rPression[sPID_pc] = eauAmbiante.regulPression.sortiePID_pc;
    doc[stime] = timeString;


    serializeJson(doc, buffer, bufferSize);

    return true;
}

bool serializeParams(uint32_t timeString, uint8_t sender, char* buffer) {
    StaticJsonDocument<300> doc;
    //Serial.println(F("SEND PARAMS"));

    doc[scmd] = SEND_MASTER_DATA;
    doc[sPLCID] = String(sender);
    doc[stime] = timeString;


    JsonArray data = doc[sdata].to<JsonArray>();

    JsonObject data_0 = data.add<JsonObject>();
    data_0["CondID"] = "1";

    JsonObject data_0_rTemp = data_0[rTemp].to<JsonObject>();
    data_0_rTemp[scons] = PACChaud.regulTemp.consigne;
    data_0_rTemp[sKp] = PACChaud.regulTemp.Kp;
    data_0_rTemp[sKi] = PACChaud.regulTemp.Ki;
    data_0_rTemp[sKd] = PACChaud.regulTemp.Kd;
    if (PACChaud.regulTemp.autorisationForcage) data_0_rTemp[saForcage] = "true";
    else data_0_rTemp[saForcage] = "false";
    data_0_rTemp[sconsForcage] = PACChaud.regulTemp.consigneForcage;

    JsonObject data_0_rPression = data_0[rPression].to<JsonObject>(); 
    data_0_rPression[scons] = eauChaude.regulPression.consigne;
    data_0_rPression[sKp] = eauChaude.regulPression.Kp;
    data_0_rPression[sKi] = eauChaude.regulPression.Ki;
    data_0_rPression[sKd] = eauChaude.regulPression.Kd;
    if (eauChaude.regulPression.autorisationForcage) data_0_rPression[saForcage] = "true";
    else data_0_rPression[saForcage] = "false";
    data_0_rPression[sconsForcage] = eauChaude.regulPression.consigneForcage;

    JsonObject data_1 = data.add<JsonObject>();
    data_1["CondID"] = "2";
    JsonObject data_1_rTemp = data_0[rTemp].to<JsonObject>();
    data_1_rTemp[scons] = PACFroid.regulTemp.consigne;
    data_1_rTemp[sKp] = PACFroid.regulTemp.Kp;
    data_1_rTemp[sKi] = PACFroid.regulTemp.Ki;
    data_1_rTemp[sKd] = PACFroid.regulTemp.Kd;
    if (PACFroid.regulTemp.autorisationForcage) data_1_rTemp[saForcage] = "true";
    else data_1_rTemp[saForcage] = "false";
    data_1_rTemp[sconsForcage] = PACFroid.regulTemp.consigneForcage;

    JsonObject data_1_rPression = data_0[rPression].to<JsonObject>();
    data_1_rPression[scons] = eauFroide.regulPression.consigne;
    data_1_rPression[sKp] = eauFroide.regulPression.Kp;
    data_1_rPression[sKi] = eauFroide.regulPression.Ki;
    data_1_rPression[sKd] = eauFroide.regulPression.Kd;
    if (eauFroide.regulPression.autorisationForcage) data_1_rPression[saForcage] = "true";
    else data_1_rPression[saForcage] = "false";
    data_1_rPression[sconsForcage] = eauFroide.regulPression.consigneForcage;

    JsonObject data_2 = data.add<JsonObject>();
    data_2["CondID"] = "3";
    JsonObject data_2_rPression = data_0[rPression].to<JsonObject>();
    data_2_rPression[scons] = eauAmbiante.regulPression.consigne;
    data_2_rPression[sKp] = eauAmbiante.regulPression.Kp;
    data_2_rPression[sKi] = eauAmbiante.regulPression.Ki;
    data_2_rPression[sKd] = eauAmbiante.regulPression.Kd;
    if (eauAmbiante.regulPression.autorisationForcage) data_2_rPression[saForcage] = "true";
    else data_2_rPression[saForcage] = "false";
    data_2_rPression[sconsForcage] = eauAmbiante.regulPression.consigneForcage;

    serializeJson(doc, buffer, bufferSize);
}

void deserializeParams(StaticJsonDocument<jsonDocSize> doc) {

    JsonArray data = doc["data"];

    for (int i = 0; i < 3; i++) {
        JsonObject data_0 = data[i];
        int condID = data_0["CondID"];
        switch (condID) {
        case 1:
            PACChaud.regulTemp.consigne = data_0[rTemp][scons];
            PACChaud.regulTemp.Kp = data_0[rTemp][sKp];
            PACChaud.regulTemp.Ki = data_0[rTemp][sKi];
            PACChaud.regulTemp.Kd = data_0[rTemp][sKd];
            PACChaud.regulTemp.autorisationForcage = data_0[rTemp][saForcage];
            PACChaud.regulTemp.consigneForcage = data_0[rTemp][sconsForcage];

            //eauChaude.regulPression.consigne = data_0[rPression][scons];
            eauChaude.regulPression.consigne = minPression;
            eauChaude.regulPression.Kp = data_0[rPression][sKp];
            eauChaude.regulPression.Ki = data_0[rPression][sKi];
            eauChaude.regulPression.Kd = data_0[rPression][sKd];
            eauChaude.regulPression.autorisationForcage = data_0[rPression][saForcage];
            eauChaude.regulPression.consigneForcage = data_0[rPression][sconsForcage];
            break;
        case 2:

            PACFroid.regulTemp.consigne = data_0[rTemp][scons];
            PACFroid.regulTemp.Kp = data_0[rTemp][sKp];
            PACFroid.regulTemp.Ki = data_0[rTemp][sKi];
            PACFroid.regulTemp.Kd = data_0[rTemp][sKd];
            PACFroid.regulTemp.autorisationForcage = data_0[rTemp][saForcage];
            PACFroid.regulTemp.consigneForcage = data_0[rTemp][sconsForcage];


            //eauFroide.regulPression.consigne = data_0[rPression][scons];
            eauFroide.regulPression.consigne = minPression;
            eauFroide.regulPression.Kp = data_0[rPression][sKp];
            eauFroide.regulPression.Ki = data_0[rPression][sKi];
            eauFroide.regulPression.Kd = data_0[rPression][sKd];
            eauFroide.regulPression.autorisationForcage = data_0[rPression][saForcage];
            eauFroide.regulPression.consigneForcage = data_0[rPression][sconsForcage];

            break; 
        case 3:

            eauAmbiante.regulPression.consigne = data_0[rPression][scons];
            eauAmbiante.regulPression.Kp = data_0[rPression][sKp];
            eauAmbiante.regulPression.Ki = data_0[rPression][sKi];
            eauAmbiante.regulPression.Kd = data_0[rPression][sKd];
            eauAmbiante.regulPression.autorisationForcage = data_0[rPression][saForcage];
            eauAmbiante.regulPression.consigneForcage = data_0[rPression][sconsForcage];
            break;
        }
    }
    //save();


}

