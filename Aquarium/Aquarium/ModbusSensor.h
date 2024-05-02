#pragma once

class ModbusSensor {
public:
    union u_tag {
        uint16_t b[2];
        float fval;
    } u;


    bool querySent = false;
    byte status[5];



    uint16_t data[16];
    modbus_t query;
    ModbusSensor() {}

    ModbusSensor(uint8_t slaveAddress) {


        query.u8id = slaveAddress; // slave address
        query.u8fct = 6; // function code (this one is registers read)
        query.u16RegAdd = 1; // start address in slave
        query.u16CoilsNo = 1; // number of elements (coils or registers) to read
        query.au16reg = data; // pointer to a memory array in the Arduino
        data[0] = 5;


        /*
        512: offset temperature
        514: pente temperature
        516: offset oxy (0%) **** offset gamme 1
        518: **** pente gamme 1
        520: **** offset gamme 2
        522: pente oxy (100%) **** pente gamme 2
        524: **** offset gamme 3
        526: **** pente gamme 3
        528: **** offset gamme 4
        530: **** pente gamme 4
        532: **** offset TU
        534: **** pente TU
        */

        /*
        638: validation calibration param 0 (temperature)
        654: validation calibration param 1
        670: validation calibration param 2
        686: validation calibration param 3
        702: validation calibration param 4
        718: validation calibration param 5
        */
        //for (int i = 0; i < 12; i++) calibrationAddresses[i] = 512 + 2 * i;
        //for (int i = 0; i < 6; i++) validationAddresses[i] = 638 + 16 * i;
    }


    void setQuery(uint8_t fct, uint16_t RegAdd, uint16_t CoilsNb) {
        query.u8fct = fct; // function code (this one is registers read)
        query.u16RegAdd = RegAdd; // start address in slave
        query.u16CoilsNo = CoilsNb; // number of elements (coils or registers) to read
    }

    void setQueryW() {
        setQuery(6, 1, 5);
        data[0] = 0;
        data[1] = 1;
        data[2] = 2;
        data[3] = 0;
        data[0] = 31;
    }
    void setQueryR() {
        setQuery(3, 83, 10);
        //data[0] = 5;
    }
    void setQueryCalibration(int offset) {
        setQuery(16, offset, 2);
    }
    void setQueryCalValidation() {
        setQuery(16, 638, 16);
    }

    bool configMesure(ModbusRtu* master) {
        setQuery(16, 165, 5);
        data[0] = 0;
        data[1] = 1024;
        data[2] = 1024;
        data[3] = 1024;
        data[4] = 1024;
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }


    bool requestValues(ModbusRtu* master)
    {
        setQueryW();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool readValues(ModbusRtu* master,double *value)
    {
        setQueryR();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                u.b[0] = data[3];
                u.b[1] = data[2];
                *value = u.fval;
                for (int i = 0; i < 16; i++) data[i] = 0;
                return 1;
            }
        }
        return 0;
    }

    bool calibrateCoeff(float value, int offset, ModbusRtu* master)
    {
        setQueryCalibration(offset);
        u.fval = value;
        data[0] = u.b[1];
        data[1] = u.b[0];

        Serial.println("calibfrate coeff");
        Serial.print("sensor address:"); Serial.println(query.u8id);
        Serial.print("Offset:"); Serial.println(query.u16RegAdd);
        Serial.print("coils number:"); Serial.println(query.u16CoilsNo);
        Serial.print("function:"); Serial.println(query.u8fct);

        if (!querySent) {
            master->query(query);
            querySent = true;
            Serial.println("query sent");
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                Serial.println("query OK");
                return 1;
            }
        }
        return 0;
    }

    bool factoryReset(ModbusRtu* master)
    {
        Serial.println("factory reset");
        setQuery(16, 2, 1);
        data[0] = 31;

        if (!querySent) {
            master->query(query);
            querySent = true;
            Serial.println("query sent");
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                Serial.println("query OK");
                return 1;
            }
        }
        return 0;
    }

    bool validateCalibration(int offset, ModbusRtu* master)
    {
        setQueryCalValidation();
        setQuery(16, offset, 16);
        for (int i = 0; i < 16; i++) data[i] = 0;
        data[0] = 'P';
        data[1] = 'i';
        data[2] = 'e';
        data[3] = 'r';
        data[4] = 'r';
        data[5] = 'e';

        data[8] = 32; //minutes
        data[9] = 17; //heures
        data[10] = 31; //jour
        data[11] = 12; //mois
        data[12] = 2020; //année

        if (!querySent) {
            master->query(query);
            querySent = true;
            Serial.println("query sent");
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;

                for (int i = 0; i < 16; i++) data[i] = 0;
                return 1;
            }
        }
        return 0;
    }


    /*
    FOR HAMILTON SENSORS
    */

    void setQuerypH() {
        setQuery(3, 2089, 10);
    }
    void setQueryTemp() {
        setQuery(3, 2409, 10);
    }
    void setQuerySetLevel() {
        setQuery(16, 4287, 4);
        query.au16reg[0] = 48;
        query.au16reg[1] = 0;
        query.au16reg[2] = 31182;
        query.au16reg[3] = 244;
    }
    void setQueryCalibrationCommand() {
        setQuery(16, 5339, 2);
    }
    void setQueryCalibrationValue() {
        setQuery(16, 5321, 2);
    }
    void setQueryCalibrationStatus() {
        setQuery(16, 5317, 6);
    }

    bool setLevel(ModbusRtu* master)
    {
        setQuerySetLevel();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool readPH(ModbusRtu* master,double * value)
    {
        setQuerypH();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                u.b[0] = data[2];
                u.b[1] = data[3];
                if(u.fval>0)*value = u.fval;
                for (int i = 0; i < 16; i++) data[i] = 0;
                querySent = false;
                return 1;

            }
        }

        return 0;

    }

    bool readTemp(ModbusRtu* master, double*value)
    {
        setQueryTemp();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                u.b[0] = data[2];
                u.b[1] = data[3];
                if (u.fval > 0)*value = u.fval;
                for (int i = 0; i < 16; i++) data[i] = 0;
                querySent = false;
                return 1;
            }
        }

        return 0;

    }
    /*
    int cmd: 1 = initial measurment
    2 = cancel an active calbration
    3 = restore standard calbration
    4 = restore product calibration*/
    bool sendCalibrationCommand(int cmd, ModbusRtu* master)
    {
        setQueryCalibrationCommand();
        data[0] = cmd;
        data[1] = 0;
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool sendCalibrationValue(float value, ModbusRtu* master)
    {
        setQueryCalibrationValue();
        u.fval = value;
        data[0] = u.b[0];
        data[1] = u.b[1];
        //Serial.print("calb value:"); Serial.print(dataCalibrationValue[0]); Serial.print(","); Serial.print(dataCalibrationValue[1]);
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool getCalibrationStatus(ModbusRtu* master)
    {
        setQueryCalibrationStatus();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                for (int i = 0; i < 6; i++) {
                    Serial.print("status "); Serial.print(i); Serial.print(":"); Serial.println(data[i]);
                }
                return 1;
            }
        }
        return 0;
    }

    int calibrate(float value, int step, ModbusRtu* master) {
        Serial.println(F("CALIB HAMILTON"));
        switch (step) {
        case 0:
            //PAS NECESSAIRE??
            if (setLevel(master)) {
                step++;
                Serial.println(F("set Level S OK"));
            }
            break;
        case 1:
            if (sendCalibrationCommand(1, master)) {//1 = request calibration; 2 = cancel calibration
                step++;
                Serial.println(F("send calib OK"));
            }
            break;
        case 2:
            if (sendCalibrationValue(value, master)) {
                step++;
                Serial.println(F("send calib value OK"));
            }
            break;
        case 3:
            if (getCalibrationStatus(master)) {
                step++;
                Serial.println(F("get status OK"));
            }
            break;
        }
        return step;
    }

    bool HamiltonFactoryReset(ModbusRtu* master)
    {
        Serial.println(F("factory reset"));
        setQuery(16, 8191, 2);
        data[0] = 911;

        if (!querySent) {
            master->query(query);
            querySent = true;
            Serial.println(F("query sent"));
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                Serial.println(F("query OK"));
                return 1;
            }
        }
        return 0;
    }

};