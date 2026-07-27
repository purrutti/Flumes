#pragma once
#include <EEPROMex.h>

#include "ModbusSensor.h"
#include <PID_v1.h>

float tempAmbiante = 18;
float tempChaud = 24;
float tempFroid = 5;
float pHAmbiant = 8;


const char* scmd = "cmd";
const char* sID = "AquaID";
const char* sPLCID = "PLCID";
const char* stime = "time";

const char* soxy = "oxy";
const char* spH = "pH";
const char* stemp = "temp";
const char* sdata = "data";
const char* rTemp = "rTemp";
const char* rpH = "rpH";

const char* scons = "cons";
const char* sPID_pc = "sPID_pc";
const char* sdebit = "debit";

const char* sKp = "Kp";
const char* sKi = "Ki";
const char* sKd = "Kd";
const char* saForcage = "aForcage";
const char* sconsForcage = "consForcage";

char buffer[500];
const size_t jsonDocSize = 512;
const int bufferSize = 500;


typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;


tempo tempoCO2ValvePWM_on;
tempo tempoCO2ValvePWM_off;

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

    bool useOffset;

    Regul() {

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

        EEPROM.updateInt(add, useOffset); add += sizeof(int);
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

        useOffset = EEPROM.readInt(add); add += sizeof(int);
        return add;
    }
};

class Aqua {
public:
    byte PLCID;
    byte id;
    byte pinDebitmetre;
    byte pinV3VC;
    byte pinV3VF;
    byte pinCO2;

    double debit;
    double temperature;
    double pH;
    double O2;

    int state; //Si l'aquarium est un controle ou bien s'il doit etre r�gul�
    bool previousMode;

    Regul regulTemp, regulpH;

    bool toggleCO2Valve;

    int startAddress;

    Aqua() {
    };
    Aqua(byte _PLCID, byte _id, byte _pinDebitmetre, byte _pinV3VC, byte _pinV3VF, byte _CO2) {
        regulpH = Regul();
        regulTemp = Regul();
        id = _id;
        PLCID = _PLCID;
        pinDebitmetre = _pinDebitmetre;
        pinV3VC = _pinV3VC;
        pinV3VF = _pinV3VF;
        pinCO2 = _CO2;

        regulpH.Kp = 0.2;
        regulpH.Ki = 50;
        regulpH.Kd = 0;
        regulTemp.Kp = 5;
        regulTemp.Ki = 1;
        regulTemp.Kd = 500;


        //int address = id - 9; while (address <= 0) address += 3;

        
        debit = 0;
    };

    int load() {

        Serial.println("LOAD Start Address " + String(id) + ":" + String(startAddress));
        int add = startAddress;
        add = regulTemp.load(add);
        add = regulpH.load(add);

        //PLCID = EEPROM.readInt(add); add += sizeof(int);
        //id = EEPROM.readInt(add); add += sizeof(int);
        state = EEPROM.readInt(add); add += sizeof(int);


        return add;

    };
    int save() {
        Serial.println("SAVE Start Address " + String(id) + ":" + String(startAddress));
        int add = startAddress;
        add = regulTemp.save(add);
        add = regulpH.save(add);

        //EEPROM.updateInt(add, PLCID); add += sizeof(int);
        //EEPROM.updateInt(add, id); add += sizeof(int);
        EEPROM.updateInt(add, state); add += sizeof(int);

        return add;

    };

    float readFlow(int lissage) {

        int ana = analogRead(pinDebitmetre); // 0-1023 value corresponding to 0-5 V corresponding to 0-20 mA

       // Serial.print("debit ana:"); Serial.println(ana);
        int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
        //Serial.print("debit mA:"); Serial.println(ana);
        double ancientDebit = debit;
        debit = (0.625 * (mA - 400)) / 100.0; // flowrate in l/mn
        //debit = (lissage * debit + (100.0 - lissage) * ancientDebit) / 100.0;
        if (debit < 0) debit = 0;
        Serial.print("debit:"); Serial.println(debit);
        return debit;
    }


    // Les vannes 3 voies sont pilotées en 2-10V alors qu'analogWrite ne fournit que du 0-10V :
    // toute commande 0-100% doit donc être ramenée dans l'intervalle utile [2V;10V].
    static const int analogMin = 51;   // ~2V sur une sortie analogWrite 0-10V (51/255*10V)
    static const int analogMax = 255;  // 10V

    double regulationTemperature() {

        if (regulTemp.autorisationForcage) {
            double percent = regulTemp.consigneForcage; // 0-100
            regulTemp.sortiePID_pc = percent;

            int analogOut = analogMin + (int)(percent / 100.0 * (analogMax - analogMin));
            analogWrite(pinV3VC, analogOut);
            analogWrite(pinV3VF, analogOut);
            return percent;
        }

        regulTemp.pid.Compute(); // sortiePID dans [-100, 100] (%)

        bool chaud = (regulTemp.sortiePID >= 0);
        double intensite = abs(regulTemp.sortiePID); // 0-100% : 0 = vanne fermée, 100 = vanne ouverte à fond
        regulTemp.sortiePID_pc = regulTemp.sortiePID;

        // Vanne active : plus l'intensité demandée est forte, plus la tension de commande est basse
        // (vannes "reverse acting" : 2V = ouverte à fond, 10V = fermée).
        int analogActif = analogMax - (int)(intensite / 100.0 * (analogMax - analogMin));

        if (chaud) {
            analogWrite(pinV3VC, analogActif);
            analogWrite(pinV3VF, analogMax); // vanne froide fermée
        }
        else {
            analogWrite(pinV3VF, analogActif);
            analogWrite(pinV3VC, analogMax); // vanne chaude fermée
        }

        return regulTemp.sortiePID;
    }


    int regulationpH(double mesurepH) {
        int dutyCycle = 0;

        if (regulpH.autorisationForcage) {
            dutyCycle = regulpH.consigneForcage;
            regulpH.sortiePID_pc = dutyCycle;
        }
        else {
            regulpH.pid.Compute();
            if (regulpH.consigne < mesurepH) {
                regulpH.sortiePID_pc = (int)regulpH.sortiePID;

                dutyCycle = regulpH.sortiePID;
                //dutyCycle = 50;

            }
            else {

                regulpH.sortiePID_pc = 0.0;

                dutyCycle = 0;
            }
        }

            unsigned long cycleDuration = 10000;
            tempoCO2ValvePWM_on.interval = dutyCycle * cycleDuration / 100;
            tempoCO2ValvePWM_off.interval = cycleDuration - tempoCO2ValvePWM_on.interval;;
            if (tempoCO2ValvePWM_on.interval == 0) toggleCO2Valve = false;
            else if (tempoCO2ValvePWM_off.interval == 0) toggleCO2Valve = true;
            else if (toggleCO2Valve) {
                if (elapsed(&tempoCO2ValvePWM_on)) {
                    tempoCO2ValvePWM_off.debut = millis();
                    toggleCO2Valve = false;
                }
            }
            else {
                if (elapsed(&tempoCO2ValvePWM_off)) {
                    tempoCO2ValvePWM_on.debut = millis();
                    toggleCO2Valve = true;
                }
            }

           
        digitalWrite(pinCO2, toggleCO2Valve);
        return dutyCycle;
    }



    bool serializeData(uint32_t timeString, uint8_t sender, char* buffer) {
        //Serial.println("SENDDATA");
        //DynamicJsonDocument doc(512);

        StaticJsonDocument<512> doc;

        doc[scmd] = 3;
        doc[sPLCID] = String(sender);
        doc[sID] = String(id);
        doc[stemp] = serialized(String((int)(temperature*100+0.5)/100.0,2));
        doc[spH] = serialized(String((int)(pH * 100 + 0.5) / 100.0));
        doc[soxy] = serialized(String((int)(O2 * 100 + 0.5) / 100.0));
        doc[sdebit] = serialized(String((int)(debit * 100 + 0.5) / 100.0));


        //Serial.print(F("CONDID:")); Serial.println(condID);
        //Serial.print(F("socketID:")); Serial.println(socketID);
        doc[stime] = timeString;

        JsonObject regulT = doc.createNestedObject(rTemp);
        regulT[scons] = regulTemp.consigne;
        regulT[sPID_pc] = regulTemp.sortiePID_pc;

        JsonObject regulp = doc.createNestedObject(rpH);
        regulp[scons] = regulpH.consigne;
        regulp[sPID_pc] = regulpH.sortiePID_pc;

        serializeJson(doc, buffer, bufferSize);
        return true;
    }

    bool serializeParams(uint32_t timeString, uint8_t sender, char* buffer) {
        StaticJsonDocument<300> doc;
        //Serial.println(F("SEND PARAMS"));

        doc[scmd] = 2;
        doc[sPLCID] = String(sender);
        doc[sID] = String(id);
        doc[stime] = timeString;
        doc[F("state")] = state;
        /*doc["mesureTemp"] = Hamilton[3].temp_sensorValue;
        doc["mesurepH"] = Hamilton[3].pH_sensorValue;*/

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

        state = doc[F("state")];
        JsonObject regulp = doc[rpH];
        regulpH.consigne = regulp[scons]; // 24.2
        regulpH.Kp = regulp[sKp]; // 2.1
        regulpH.Ki = regulp[sKi]; // 2.1
        regulpH.Kd = regulp[sKd]; // 2.1
        regulpH.autorisationForcage = regulp[saForcage];
        regulpH.consigneForcage = regulp[sconsForcage]; // 2.1
        regulpH.useOffset = regulp[F("useOffset")];
        regulpH.offset = regulp[F("offset")];

        JsonObject regulT = doc[rTemp];

        regulTemp.consigne = regulT[scons]; // 24.2
        regulTemp.Kp = regulT[sKp]; // 2.1
        regulTemp.Ki = regulT[sKi]; // 2.1
        regulTemp.Kd = regulT[sKd]; // 2.1
        regulTemp.autorisationForcage = regulT[saForcage];
        regulTemp.consigneForcage = regulT[sconsForcage]; // 2.1

        regulTemp.useOffset = regulT[F("useOffset")];
        regulTemp.offset = regulT[F("offset")];


    }

};


