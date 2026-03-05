#include <Servo.h>

Servo myservo;  // create servo object to control a servo
// twelve servo objects can be created on most boards

int pos = 0;    // variable to store the servo position

void setup() {
    myservo.attach(3);  // attaches the servo on pin 9 to the servo object
    delay(1000);
    myservo.writeMicroseconds(1500);
    delay(1000);
}

void loop() {
    myservo.writeMicroseconds(1900);
}