/*********
  Rui Santos
  Complete project details at https://RandomNerdTutorials.com/esp32-esp8266-input-data-html-form/

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files.

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.
*********/

#include <Arduino.h>
#ifdef ESP32
#include <WiFi.h>
#include <AsyncTCP.h>
#else
#include <ESP8266WiFi.h>
#include <ESPAsyncTCP.h>
#endif
#include <ESPAsyncWebServer.h>

#include <analogWrite.h>
#include <ESP32PWM.h>
#include <ESP32Servo.h>
#include <ESP32Tone.h>

byte servoPin = 4;
Servo servo;


int s = 1500; // Set signal value, which should be between 1100 and 1900

AsyncWebServer server(80);

// REPLACE WITH YOUR NETWORK CREDENTIALS
const char* ssid = "FLUME";
const char* password = "password";

const char* PARAM_INPUT_1 = "val";

// HTML web page to handle 3 input fields (input1, input2, input3)
const char index_html[] PROGMEM = R"rawliteral(
<!DOCTYPE HTML><html><head>
  <title>ESP Input Form</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  </head><body>
  <form action="/get">
    input1: <input type="text" name="val">
    <input type="submit" value="Submit">
  </form><br>
</body></html>)rawliteral";

void notFound(AsyncWebServerRequest* request) {
    request->send(404, "text/plain", "Not found");
}

void setup() {
    Serial.begin(115200);
    WiFi.mode(WIFI_AP);
    WiFi.softAP(ssid, password);

    Serial.println();
    Serial.print("IP Address: ");
    Serial.println(WiFi.softAPIP());

    // Send web page with input fields to client
    server.on("/", HTTP_GET, [](AsyncWebServerRequest* request) {
        request->send_P(200, "text/html", index_html);
        });

    // Send a GET request to <ESP_IP>/get?input1=<inputMessage>
    server.on("/get", HTTP_GET, [](AsyncWebServerRequest* request) {
        String inputMessage;
    String inputParam;
    // GET input1 value on <ESP_IP>/get?input1=<inputMessage>
    if (request->hasParam(PARAM_INPUT_1)) {
        inputMessage = request->getParam(PARAM_INPUT_1)->value();
        s = inputMessage.toInt();
        inputParam = PARAM_INPUT_1;
    }
    else {
        inputMessage = "No message sent";
        inputParam = "none";
    }
        
    Serial.println(inputMessage);
    request->send_P(200, "text/html", index_html);
        });
    server.onNotFound(notFound);
    server.begin();


    Serial.begin(115200);
    servo.attach(servoPin);

    servo.writeMicroseconds(1500); // send "stop" signal to ESC.

    delay(7000); // delay to allow the ESC to recognize the stopped signal
}
void loop() {


    if (s >= 1100 && s <= 1900)	servo.writeMicroseconds(s); // Send signal to ESC.
}