#include <WiFi.h>
#include <ESPAsyncWebServer.h>

#include <ESP32PWM.h>
#include <ESP32Servo.h>
#include <ESP32Tone.h>

// Remplacez par vos informations Wi-Fi
const char* ssid = "WIFI-VF";
const char* password = "L@Vielle4Ge";


AsyncWebServer server(80);

// Variables pour stocker les états et les consignes
bool flumeState[8] = { false, false, false, false, false, false, false, false };
int flumeConsigne[8] = { 0, 0, 0, 0, 0, 0, 0, 0 };

// Utilisation des pins PWM connues pour être compatibles
const int pwmPins[8] = { 2, 4, 12, 13, 14, 15, 25, 26 };

// Page HTML avec Bootstrap et CSS personnalisé
const char* index_html = R"rawliteral(
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Flumes Motor control</title>
  <link href="https://maxcdn.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css" rel="stylesheet">
  <style>
    .switch {
      position: relative;
      display: inline-block;
      width: 60px;
      height: 34px;
    }
    .switch input {
      opacity: 0;
      width: 0;
      height: 0;
    }
    .slider {
      position: absolute;
      cursor: pointer;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-color: #ccc;
      transition: .4s;
    }
    .slider:before {
      position: absolute;
      content: "";
      height: 26px;
      width: 26px;
      left: 4px;
      bottom: 4px;
      background-color: white;
      transition: .4s;
    }
    input:checked + .slider {
      background-color: #2196F3;
    }
    input:checked + .slider:before {
      transform: translateX(26px);
    }
    .slider.round {
      border-radius: 34px;
    }
    .slider.round:before {
      border-radius: 50%;
    }
    body {
      background-color: #f8f9fa;
    }
    .container {
      background-color: #ffffff;
      border-radius: 8px;
      box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
      padding: 20px;
    }
    h2 {
      margin-bottom: 20px;
    }
    .table {
      margin-top: 20px;
    }
    .btn {
      margin-top: 20px;
    }
  </style>
</head>
<body>
  <div class="container mt-5">
    <h2>ESP32 Control Panel</h2>
    <form action="/submit" method="post">
      <table class="table table-bordered">
        <thead class="thead-light">
          <tr>
            <th scope="col">Flume</th>
            <th scope="col">Toggle</th>
            <th scope="col">Consigne (%)</th>
          </tr>
        </thead>
        <tbody>
          %ROWS%
        </tbody>
      </table>
      <button type="submit" class="btn btn-primary">Submit</button>
    </form>
  </div>
  <script src="https://code.jquery.com/jquery-3.5.1.slim.min.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/@popperjs/core@2.5.4/dist/umd/popper.min.js"></script>
  <script src="https://maxcdn.bootstrapcdn.com/bootstrap/4.5.2/js/bootstrap.min.js"></script>
</body>
</html>
)rawliteral";

Servo servo[8];

void setup() {
    Serial.begin(115200);

    // Connexion au Wi-Fi
    WiFi.begin(ssid, password);
    while (WiFi.status() != WL_CONNECTED) {
        delay(1000);
        Serial.println("Connexion au Wi-Fi...");
    }
    Serial.println("Connecté au Wi-Fi");
    Serial.println(WiFi.localIP());

    // Initialisation des pins PWM
    for (int i = 0; i < 8; i++) {

        servo[i].attach(pwmPins[i]);

        servo[i].writeMicroseconds(1500); // send "stop" signal to ESC.
        //ledcAttachPin(pwmPins[i], i); // Canaux de 0 à 7
        //ledcSetup(i, 5000, 11); // Fréquence de 5kHz, résolution de 11 bits
        //ledcWrite(i, 1100); // Initialisation à 1500
    }

    delay(1000); // delay to allow the ESC to recognize the stopped signal

    // Serveur web
    server.on("/", HTTP_GET, [](AsyncWebServerRequest* request) {
        String html = index_html;
        html.replace("%ROWS%", generateRows());
        request->send(200, "text/html", html);
        });

    server.on("/submit", HTTP_POST, [](AsyncWebServerRequest* request) {
        for (int i = 0; i < 8; i++) {
            String toggleParam = "toggle" + String(i);
            String consigneParam = "consigne" + String(i);

            if (request->hasParam(toggleParam, true)) {
                Serial.println(request->getParam(toggleParam, true)->value());
                flumeState[i] = request->getParam(toggleParam, true)->value() == "on";
            }
            else {
                flumeState[i] = false;
                Serial.println("off");

            }
            if (request->hasParam(consigneParam, true)) {
                flumeConsigne[i] = request->getParam(consigneParam, true)->value().toInt();
            }

            int pwmValue;
            if (flumeState[i]) {
                pwmValue = map(flumeConsigne[i], 0, 100, 1500, 1900);
            }
            else {
                pwmValue = 1500;
            }
            servo[i].writeMicroseconds(pwmValue);
            //ledcWrite(i, pwmValue); // Mise à jour de la PWM
            Serial.println("pwm " + String(i) + " set to " + String(pwmValue));
        }
        String html = index_html;
        html.replace("%ROWS%", generateRows());
        request->send(200, "text/html", html);
        });

    server.begin();
}

void loop() {
}

// Fonction pour générer les lignes du tableau dynamiquement
String generateRows() {
    String rows = "";
    for (int i = 0; i < 8; i++) {
        rows += "<tr>";
        rows += "<td>Flume " + String(i + 13) + "</td>";
        rows += "<td><label class='switch'><input type='checkbox' name='toggle" + String(i) + "' " + (flumeState[i] ? "checked" : "") + "><span class='slider round'></span></label></td>";
        rows += "<td><input type='number' class='form-control' name='consigne" + String(i) + "' min='0' max='100' value='" + String(flumeConsigne[i]) + "'></td>";
        rows += "</tr>";
    }
    return rows;
}

