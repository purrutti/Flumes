#include <analogWrite.h>
#include <ESP32PWM.h>
#include <ESP32Servo.h>
#include <ESP32Tone.h>

byte servoPin = 2;
Servo servo;

void setup() {
	Serial.begin(115200);
	servo.attach(servoPin);

	servo.writeMicroseconds(1500); // send "stop" signal to ESC.

	delay(7000); // delay to allow the ESC to recognize the stopped signal
}
int s = 1500; // Set signal value, which should be between 1100 and 1900

void loop() {
	
	if (Serial.available() > 0) {
		s = Serial.readString().toInt();
		Serial.print("received: "); Serial.println(s);
	}
	if (s >= 1100 && s <= 1900)	servo.writeMicroseconds (s); // Send signal to ESC.
}