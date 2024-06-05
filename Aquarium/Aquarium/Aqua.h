#pragma once
#include <EEPROMex.h>

#include "ModbusSensor.h"
#include <PID_v1.h>

float tempAmbiante = 18;
float tempChaud = 24;
float tempFroid = 5;


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

    double meanPIDOutput=255;
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

    bool control; //Si l'aquarium est un controle ou bien s'il doit etre r�gul�
    bool previousMode;

    Regul regulTemp, regulpH;

    bool toggleCO2Valve;

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


        int address = id - 9; while (address <= 0) address += 3;

        
        debit = 0;
    };

    bool load() {

    };
    bool save() {

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


    double regulationTemperature(bool chaud) {

        if (chaud) {
            //Serial.println("chaud");
            analogWrite(pinV3VF, 255);
            regulTemp.pid.SetControllerDirection(REVERSE);
            regulTemp.pid.Compute();
        }
        else {
            //Serial.println("froid");
            analogWrite(pinV3VC, 255);
            regulTemp.pid.SetControllerDirection(DIRECT);
            regulTemp.pid.Compute();

        }
        /*Serial.println("consigne:" + String(regulTemp.consigne));
        Serial.println("sortie:" + String(regulTemp.sortiePID));
        Serial.println("kp:" + String(regulTemp.Kp));
        Serial.println("ki:" + String(regulTemp.Ki));
        Serial.println("kd:" + String(regulTemp.Kd));*/
        regulTemp.sortiePID_pc = (int)map(regulTemp.sortiePID, 50, 255, 0, 100);
        if (regulTemp.sortiePID_pc < 0) regulTemp.sortiePID_pc = 0;
                return regulTemp.sortiePID;
    }


    int regulationpH() {
        int dutyCycle = 0;

            regulpH.pid.Compute();

            regulpH.sortiePID_pc = (int)regulpH.sortiePID;

            dutyCycle = regulpH.sortiePID;
            //dutyCycle = 50;
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
        doc[F("controle")] = control;
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

        const char* scontrole = doc[F("controle")];
        if (strcmp(scontrole, "true") == 0 || strcmp(scontrole, "True") == 0)control = true;
        else control = false;
        tempAmbiante = doc[F("tempAmbiante")];
        tempChaud = doc[F("tempChaud")];
        tempFroid = doc[F("tempFroid")];
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

        Serial.println("Temp CHaud:" + String(tempChaud));
        Serial.println("Temp Froid:" + String(tempFroid));
        Serial.println("Temp Ambiant:" + String(tempAmbiante));

    }

};


