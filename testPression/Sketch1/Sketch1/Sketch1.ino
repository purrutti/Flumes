/*
 Name:		Sketch1.ino
 Created:	29/11/2023 16:01:47
 Author:	pierr
*/

#define PIN 56

// the setup function runs once when you press reset or power the board
void setup() {
	Serial.begin(115200);
	pinMode(PIN, INPUT);
}

// the loop function runs over and over again until power down or reset
void loop() {


	int ana = analogRead(PIN); // 0-1023 value corresponding to 0-10 V corresponding to 0-20 mA
	//if using 330 ohm resistor so 20mA = 6.6V
	//int ana2 = ana * 10 / 6.6;
	int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
	int mbars = map(mA, 400, 2000, 0, 4000); //map to milli amps with 2 extra digits

	float pression = ((double)mbars) / 1000.0; // pressure in bars



	Serial.print("points:");
	Serial.println(ana);
	Serial.print("mA:");
	Serial.println(mA);
	Serial.print("bar:");
	Serial.println(pression);
	delay(1000);
}


float readPressure(int lissage, uint8_t pin, double pression) {
	int ana = analogRead(pin); // 0-1023 value corresponding to 0-10 V corresponding to 0-20 mA
	//if using 330 ohm resistor so 20mA = 6.6V
	//int ana2 = ana * 10 / 6.6;
	int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
	int mbars = map(mA, 400, 2000, 0, 4000); //map to milli amps with 2 extra digits
	double anciennePression = pression;
	pression = ((double)mbars) / 1000.0; // pressure in bars
	pression = (lissage * pression + (100.0 - lissage) * anciennePression) / 100.0;
	return pression;
}
