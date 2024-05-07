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
IPAddress ip(192, 168, 1, 160 + PLCID);

WebSocketsClient webSocket;
ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI

char buffer[600];


typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;

tempo tempoSensorRead;



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



    RTC.read();
    //setPIDparams();

    Serial.println("START");
    tempoSensorRead.interval = 1000;
}


// the loop function runs over and over again until power down or reset
void loop() {

    if (elapsed(&tempoSensorRead.debut, tempoSensorRead.interval)) {

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
        Serial.print("TEMP FROID:"); Serial.println(PACFroid.temperature));

    }
    
}

bool elapsed(unsigned long* previousMillis, unsigned long interval) {
    if (*previousMillis == 0) {
        *previousMillis = millis();
    }
    else {
        if ((unsigned long)(millis() - *previousMillis) >= interval) {
            *previousMillis = 0;
            return true;
        }
    }
    return false;
}