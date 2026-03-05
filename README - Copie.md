# Système de Pilotage d'Aquariums et Flumes Expérimentaux

## Vue d'ensemble

Ce projet implémente une solution complète de pilotage automatisé pour des aquariums et flumes expérimentaux destinés à la recherche scientifique. Le système permet la régulation précise de multiples paramètres environnementaux (température, pH, débit, pression, vitesse de courant) pour maintenir des conditions expérimentales contrôlées.

### Architecture générale

Le système est composé de plusieurs modules interconnectés :

1. **Contrôleurs Arduino** : Automates industriels (IndustrialShields) qui pilotent les capteurs et actionneurs
2. **Serveur WebSocket C#** : SuperviFlume - serveur central de communication et stockage de données
3. **Interface Web** : Frontend hébergé sur https://github.com/purrutti/flumeswebsite
4. **Base de données** : InfluxDB pour l'historisation des données

### Communication

- **Protocole** : WebSocket (port 81) pour la communication temps réel
- **Format** : JSON pour l'échange de données
- **Modbus RTU** : Communication avec les capteurs Hamilton (pH/température) et PODOC (O2)

---

## Composants du système

### 1. Aquarium (Aquarium.ino)

**Localisation** : `Aquarium/Aquarium/Aquarium.ino`

**Description** : Gère 3 aquariums avec régulation automatique de température et pH.

#### Fonctionnalités principales

**Capteurs et mesures** :
- Capteurs Hamilton pH/température via Modbus (ID 1-3)
- Capteurs PODOC oxygène dissous (ID 10-12)
- Débitmètres analogiques (3 unités)

**Régulation température** :
- Contrôleur PID pour maintenir la température de consigne
- Utilise 2 vannes 3 voies (V3V) par aquarium : chaud (PIN_V3VC) et froid (PIN_V3VF)
- Température de référence : eau ambiante (18°C), chaude (24°C), froide (5°C)
- Sélection automatique chaud/froid selon consigne

**Régulation pH** :
- Contrôleur PID inversé (REVERSE) pour injection CO2
- Électrovanne CO2 avec modulation PWM (cycle 10s)
- Référence pH ambiant : 8.0

**Pins utilisées** :
```cpp
PIN_DEBITMETRE[3] = {54, 55, 56}
PIN_V3VC[3] = {4, 6, 9}       // Vannes 3 voies chaud
PIN_V3VF[3] = {5, 8, 7}       // Vannes 3 voies froid
PIN_CO2[3] = {36, 37, 38}     // Électrovannes CO2
```

**Configuration réseau** :
- PLCID : 7
- IP : 172.16.36.207
- Serveur : 192.168.73.14:81

#### Fonctions principales

**`setup()`** :
- Initialise Ethernet, WebSocket, Modbus (9600 baud)
- Configure les PIDs pour température et pH
- Charge les paramètres depuis EEPROM

**`loop()`** :
- Lecture cyclique des capteurs Modbus
- Calcul et application des régulations PID
- Envoi périodique des données (5s)

**`readSensors()`** :
- Séquence de lecture : pH puis température pour chaque aquarium
- Lecture O2 des capteurs PODOC (séquence request/read)
- Gestion de la calibration si demandée

**`regulTemp(int aquaID)`** :
- Détermine mode chaud/froid selon consigne vs température ambiante
- Calcule sortie PID avec limites 50-255
- Applique PWM sur la vanne appropriée

**`regulationpH()`** (classe Aqua) :
- Calcule sortie PID (0-100%)
- Génère PWM logiciel pour électrovanne CO2 (cycle 10s)

**Communication JSON** :
- `REQ_PARAMS (0)` : Requête paramètres
- `SEND_PARAMS (2)` : Envoi paramètres (consignes, Kp, Ki, Kd)
- `SEND_DATA (3)` : Envoi données temps réel
- `SEND_MASTER_DATA (6)` : Réception données maître (températures référence)

---

### 2. MasterFlumes (MasterFlumes.ino)

**Localisation** : `MasterFlumes/MasterFlumes/MasterFlumes.ino`

**Description** : Système de conditionnement centralisé de l'eau pour les flumes. Gère 3 circuits d'eau (chaud, froid, ambiant) avec régulation de pression et température via pompes à chaleur (PAC).

#### Fonctionnalités principales

**Système de conditionnement** :

**3 circuits d'eau** :
1. **Eau chaude** : Température régulée par PAC chaud (consigne 30°C)
2. **Eau froide** : Température régulée par PAC froid (consigne 12°C)
3. **Eau ambiante** : Pression régulée, température ambiante + mesures pH

**Capteurs** :
- 3 capteurs de pression analogiques 4-20mA (0-4 bars)
- 3 débitmètres analogiques 4-20mA (0-15 l/min)
- 2 sondes de température PAC 4-20mA (0-50°C)
- 1 capteur Hamilton pH/température (Modbus ID 1) pour eau ambiante
- 1 détecteur de niveau (alarme)

**Actionneurs** :
- 3 vannes 2 voies proportionnelles PWM (V2V) pour régulation pression
- 2 vannes 3 voies PWM pour PAC (échange thermique)

**Pins utilisées** :
```cpp
PIN_PRESSION[3] = {54, 56, 55}      // Capteurs pression
PIN_DEBITMETRE[3] = {60, 61, 62}    // Débitmètres
PIN_V2VC = 6, PIN_V2VF = 4, PIN_V2VA = 5  // Vannes 2V
PIN_V3VC = 9, PIN_V3VF = 8          // Vannes 3V PAC
PIN_TEMP_PAC_C = 58, PIN_TEMP_PAC_F = 57
PIN_NIVEAU = 59                      // Alarme niveau
```

**Configuration réseau** :
- PLCID : 5
- IP : 172.16.36.205

#### Régulations

**Régulation de pression** (classe Condition) :
- PID DIRECT pour maintenir pression minimale (0.2 bar par défaut)
- Sortie PWM 50-255 sur vannes 2 voies
- Paramètres PID : Kp=100, Ki=10, Kd=20
- Sécurité niveau d'eau : coupe eau ambiante si alarme

**Régulation température PAC** (classe PAC) :
- PAC chaud : PID DIRECT (Kp=50, Ki=1, Kd=20)
- PAC froid : PID REVERSE (Kp=50, Ki=1, Kd=20)
- Sortie PWM 50-255 sur vannes 3 voies
- Lecture température 4-20mA → 0-50°C

#### Fonctions principales

**`readPressure(int lissage)`** :
- Lecture analogique → conversion mA → bars
- Lissage exponentiel selon paramètre (100 = fort lissage)
- Mapping : 400-2000 mA → 0-4 bars

**`readFlow(int lissage)`** :
- Lecture analogique → mA → l/min
- Formule : débit = (9.375 * (mA - 394)) / 100.0
- Plage : 0-15 l/min

**`checkNiveau()`** :
- Vérifie détecteur de niveau
- Si alarme : coupe vanne eau ambiante, active alarmeNiveau

**`readMBSensors()`** :
- Lecture cyclique pH puis température du capteur ambiant
- Capteur Modbus Hamilton ID 1

**Communication JSON** :
- `SEND_MASTER_DATA (6)` : Envoi données 3 conditions
- Structure : température, pression, débit + régulations pour chaque circuit

---

### 3. Flume (Flume.ino)

**Localisation** : `Flume/Flume/Flume/Flume.ino`

**Description** : Gère 4 flumes expérimentaux avec régulation avancée température, pH, débit et mesure de vitesse de courant.

#### Particularités

**Régulation température hybride** :
- Flumes 1-3 : Vannes 3 voies PWM classiques
- Flume 4 : DAC (Digital-to-Analog Converter) GP8403 I2C 0-10V
- PID bidirectionnel : sortie -255 à +255 (négatif=froid, positif=chaud)
- Support offset température : consigne = tempAmbiante + offset

**Capteurs** :
- Hamilton pH/température (ID 1-4)
- PODOC O2 (ID 10-13)
- Débitmètres analogiques
- Capteurs de vitesse analogiques (4 unités)

**Pins utilisées** :
```cpp
PIN_DEBITMETRE[4] = {54, 55, 56, 57}
PIN_V3VC[4] = {4, 6, 9, 0}
PIN_V3VF[4] = {5, 8, 7, 1}
PIN_CO2[4] = {36, 37, 38, 39}
PIN_VITESSE[4] = {22, 23, 24, 25}
```

**DAC GP8403** :
- Adresse I2C : 0x5F
- 2 canaux 0-10V (OUT0=chaud, OUT1=froid)
- Résolution : 15 bits (0-10000 = 0-10V)

#### Fonctions spécifiques

**`regulTemp(int flumeID)`** :
- Calcul PID bidirectionnel
- Si sortie < 0 : activation circuit froid
- Si sortie > 0 : activation circuit chaud
- Flume 4 : contrôle DAC au lieu de PWM
- Mapping sortie : -255/+255 → 0-10V DAC ou PWM inversé

**`readSpeed()`** :
- Lecture capteur de vitesse analogique
- Stockage dans flume[i].vitesse

**Calibration capteurs** :
- Support calibration Hamilton (pH) et PODOC (O2)
- Procédure multi-étapes via Modbus
- Factory reset disponible (paramètre 99)

**Sauvegarde EEPROM** :
- Paramètres PID, consignes, offsets
- État (contrôle/régulation)
- Adresses mémoire séquentielles par flume

---

### 4. ESP32_MoteurFlumes (ESP32_MoteurFlumes.ino)

**Localisation** : `ESP32_MoteurFlumes/ESP32_MoteurFlumes/ESP32_MoteurFlumes.ino`

**Description** : Contrôleur ESP32 pour piloter 8 moteurs brushless (ESC) de flumes via serveur web.

#### Caractéristiques

**Réseau** :
- Mode Access Point : SSID "flumesmotors" / Password "flumesmotors"
- mDNS : flumemotors.local
- Serveur HTTP asynchrone (AsyncWebServer port 80)

**Contrôle moteurs** :
- 8 moteurs via ESC (Electronic Speed Controller)
- Protocole Servo : 1500μs = arrêt, 1500-1900μs = vitesse
- Bibliothèque ESP32Servo

**Pins PWM** :
```cpp
pwmPins[8] = {2, 4, 12, 13, 14, 15, 25, 26}
```

#### Interface web

**Page HTML Bootstrap** :
- Tableau avec 8 lignes (Flume 13-20)
- Toggle switch ON/OFF par moteur
- Slider consigne 0-100%
- Bouton Submit pour appliquer

**Fonctions** :

**`setup()`** :
- Initialise WiFi AP et mDNS
- Attache servos sur 8 pins
- Envoie signal arrêt (1500μs) à tous les ESC
- Configure routes HTTP

**`generateRows()`** :
- Génère HTML dynamique pour tableau
- État toggle et consigne de chaque flume

**Route `/submit` POST** :
- Récupère paramètres toggle0-7 et consigne0-7
- Calcule PWM : map(consigne, 0-100, 1500-1900μs)
- Si OFF : force 1500μs (arrêt)
- Applique servo.writeMicroseconds()

---

### 5. SuperviFlume - Serveur C# (MainWindow.xaml.cs)

**Localisation** : `SuperviFlume/SuperviFlume/MainWindow.xaml.cs`

**Description** : Serveur WebSocket central en C# WPF qui orchestre la communication entre tous les contrôleurs Arduino et l'interface web.

#### Architecture

**Framework** : .NET WPF avec HttpListener pour WebSocket

**Base de données** :
- InfluxDB (localhost:8086)
- Organisation, bucket et token configurés dans App.config
- Écriture asynchrone des points de données

**Modèle de données** :

**Classe `Aquarium`** :
```csharp
- ID, PLCID : identifiants
- debit, debitCircul : débits
- temperature, pH, oxy : mesures
- regulTemp, regulpH : objets Regul avec consignes et PID
- lastUpdated : timestamp dernière mise à jour
```

**Classe `MasterData`** :
```csharp
- Command, PLCID, Time
- List<DataItem> : 3 conditions (chaud, froid, ambiant)
- Chaque DataItem : température, pression, débit, régulations
```

#### Fonctionnalités

**Serveur WebSocket** :
- Écoute : 172.16.253.82:81
- Accepte connexions multiples simultanées
- Buffer 1024 octets pour messages

**Gestion des commandes** (`ReadData`) :

| Commande | Description |
|----------|-------------|
| 0 | REQ_PARAMS : Envoie paramètres d'un aquarium au client |
| 1 | REQ_DATA : Non utilisé |
| 2 | SEND_PARAMS : Réception paramètres depuis Arduino |
| 3 | SEND_DATA : Réception données temps réel depuis Arduino |
| 4 | CALIBRATE_SENSOR : Non utilisé |
| 6 | SEND_MASTER_DATA : Réception données Master |
| 7 | Requête frontend : Envoie tous les aquariums au frontend |

**Stockage InfluxDB** (`writeDataPointAsync`) :
- Measurement : "Flumes"
- Tag : Aquarium ID ou Condition ID
- Fields : température, pH, débit, consignes, sorties PID, etc.
- Precision : secondes

**Sauvegarde CSV** (`saveToFile`) :
- Fichier quotidien : dataFileBasePath_YYYY-MM-DD.csv
- Headers automatiques pour 12 aquariums
- Ligne par intervalle (configurable dans App.config)

**Transfert FTP** (`ftpTransfer`) :
- Upload fichiers CSV vers serveur FTP
- Credentials dans App.config

**Tâche périodique** (`InitializeAsync`) :
- Intervalle configurable (dataLogInterval en minutes)
- Exécute saveData() périodiquement
- Utilise CancellationToken pour arrêt propre

#### DataGrid WPF

**ObservableCollection** :
- 20 aquariums pré-initialisés
- Binding bidirectionnel avec interface
- Rafraîchissement automatique lors de réception données

**Sécurité** :
- Vérifie qu'une seule instance est lancée (Process.GetProcessesByName)
- Fermeture automatique si doublon détecté

---

## Protocole de communication JSON

### Structures de messages

**Requête paramètres** :
```json
{
  "cmd": 0,
  "AquaID": 1,
  "PLCID": 7
}
```

**Envoi paramètres** :
```json
{
  "cmd": 2,
  "PLCID": 7,
  "AquaID": 1,
  "time": 1234567890,
  "state": 1,
  "rTemp": {
    "cons": 20.5,
    "Kp": 5.0,
    "Ki": 1.0,
    "Kd": 500.0,
    "aForcage": "false",
    "consForcage": 0,
    "useOffset": true,
    "offset": 2.0
  },
  "rpH": {
    "cons": 7.5,
    "Kp": 0.2,
    "Ki": 50.0,
    "Kd": 0.0,
    "aForcage": "false",
    "consForcage": 0
  }
}
```

**Envoi données** :
```json
{
  "cmd": 3,
  "PLCID": 7,
  "AquaID": 1,
  "time": 1234567890,
  "temp": 20.25,
  "pH": 7.45,
  "oxy": 85.3,
  "debit": 2.15,
  "rTemp": {
    "cons": 20.5,
    "sPID_pc": 45
  },
  "rpH": {
    "cons": 7.5,
    "sPID_pc": 23
  }
}
```

**Données Master** :
```json
{
  "cmd": 6,
  "PLCID": 5,
  "time": 1234567890,
  "data": [
    {
      "CondID": 1,
      "temperature": 30.2,
      "pression": 1.25,
      "debit": 8.5,
      "rTemp": {"cons": 30.0, "sPID_pc": 65},
      "rPression": {"cons": 0.2, "sPID_pc": 55}
    },
    {
      "CondID": 2,
      "temperature": 12.1,
      "pression": 1.22,
      "debit": 7.8,
      "rTemp": {"cons": 12.0, "sPID_pc": 42},
      "rPression": {"cons": 0.2, "sPID_pc": 51}
    },
    {
      "CondID": 3,
      "temperature": 18.5,
      "pH": 8.12,
      "pression": 0.95,
      "debit": 5.2,
      "rPression": {"cons": 0.2, "sPID_pc": 38}
    }
  ]
}
```

**Calibration capteur** :
```json
{
  "cmd": 4,
  "PLCID": 7,
  "sensorID": 1,
  "calibParam": 0,
  "value": 7.01
}
```
- `calibParam: 0` = calibration point bas
- `calibParam: 1` = calibration point haut
- `calibParam: 99` = factory reset

---

## Configuration réseau

### Adresses IP

| Contrôleur | PLCID | IP | Fonction |
|------------|-------|-----|----------|
| MasterFlumes | 5 | 172.16.36.205 | Conditionnement eau |
| Aquarium | 7 | 172.16.36.207 | 3 aquariums |
| Flume | 7 | 172.16.36.207 | 4 flumes |
| SuperviFlume | - | 172.16.253.82:81 | Serveur WebSocket |
| ESP32 Motors | - | flumemotors.local | Contrôle moteurs |

### Modbus RTU

**Configuration** :
- Vitesse : 9600 baud
- Port : Serial3 (pin 46 direction)
- Timeout : 1000ms

**Adresses capteurs** :
- Hamilton pH/Temp : ID 1-13 (selon système)
- PODOC O2 : ID 10-13

---

## Déploiement et utilisation

### Prérequis

**Arduino/IndustrialShields** :
- Arduino IDE ou Visual Micro
- Bibliothèques :
  - Ethernet
  - WebSockets
  - ModbusRtu
  - TimeLib
  - EEPROMex
  - ArduinoJson
  - RTC
  - PID_v1

**ESP32** :
- ESP32 Arduino Core
- Bibliothèques :
  - ESP32Servo
  - ESPAsyncWebServer
  - ESPmDNS

**SuperviFlume C#** :
- Visual Studio 2019+
- .NET Framework 4.7.2
- NuGet packages :
  - Newtonsoft.Json
  - InfluxDB.Client

**InfluxDB** :
- InfluxDB 2.x installé sur serveur
- Créer organisation, bucket et token d'accès

### Installation

**1. Configuration InfluxDB** :

```bash
# Créer organisation et bucket
influx org create -n votre_org
influx bucket create -n flumes_data -o votre_org
influx auth create --org votre_org --all-access
```

**2. Configuration SuperviFlume** :

Éditer `App.config` :
```xml
<appSettings>
  <add key="InfluxDBToken" value="votre_token_ici"/>
  <add key="InfluxDBBucket" value="flumes_data"/>
  <add key="InfluxDBOrg" value="votre_org"/>
  <add key="dataLogInterval" value="5"/>
  <add key="dataFileBasePath" value="C:/Data/flumes"/>
  <add key="ftpUsername" value="user"/>
  <add key="ftpPassword" value="pass"/>
  <add key="ftpDir" value="ftp.example.com/data"/>
</appSettings>
```

**3. Compilation Arduino** :

- Ouvrir chaque projet .ino dans Arduino IDE
- Sélectionner carte IndustrialShields appropriée
- Vérifier PLCID et SERVER_IP dans le code
- Compiler et téléverser

**4. Compilation ESP32** :

- Ouvrir ESP32_MoteurFlumes.ino
- Sélectionner carte ESP32 Dev Module
- Téléverser

**5. Lancement** :

1. Démarrer InfluxDB
2. Lancer SuperviFlume.exe
3. Cliquer "Start Server"
4. Alimenter les contrôleurs Arduino/ESP32
5. Vérifier connexions WebSocket dans SuperviFlume

### Utilisation quotidienne

**Modification des consignes** :

Via l'interface web (flumeswebsite) ou directement dans le DataGrid SuperviFlume :
- Consignes température, pH
- Paramètres PID (Kp, Ki, Kd)
- Mode forcage manuel

**Calibration capteurs** :

1. Immerger capteur dans solution étalon
2. Envoyer commande calibration (cmd: 4)
3. Attendre stabilisation
4. Valider ou annuler

**Monitoring** :

- DataGrid SuperviFlume : temps réel
- InfluxDB/Grafana : historique et graphiques
- Fichiers CSV : export quotidien

**Alarmes** :

- Alarme niveau eau : coupe eau ambiante automatiquement
- Perte connexion WebSocket : affichée dans SuperviFlume
- Timeout Modbus : retry automatique

---

## Maintenance et dépannage

### Diagnostic

**Problème connexion WebSocket** :

1. Vérifier IP serveur dans code Arduino (SERVER_IP)
2. Vérifier firewall Windows sur port 81
3. Tester avec telnet : `telnet 172.16.253.82 81`

**Problème Modbus** :

1. Vérifier câblage RS485 (A, B, GND)
2. Vérifier terminaisons 120Ω
3. Vérifier alimentation capteurs 24V
4. Tester vitesse baud rate (9600)

**Régulation instable** :

1. Réduire Kp (gain proportionnel)
2. Augmenter temps intégral (réduire Ki)
3. Vérifier capteurs et actionneurs
4. Vérifier consigne réaliste

### Logs et debug

**Arduino Serial Monitor** :
- Vitesse : 115200 baud
- Affiche connexion, données capteurs, erreurs Modbus

**SuperviFlume MessageTextBox** :
- Affiche tous les messages WebSocket reçus
- Timestamp automatique

**InfluxDB logs** :
- Vérifier écriture des points : `influx query 'from(bucket:"flumes_data") |> range(start: -1h)'`

### Sauvegardes

**EEPROM Arduino** :
- Paramètres PID et consignes sauvegardés automatiquement
- Restauration au redémarrage

**Base de données** :
- Sauvegarder bucket InfluxDB régulièrement
- Export CSV quotidien automatique

---

## Structure des fichiers

```
Flumes/
├── Aquarium/
│   └── Aquarium/
│       ├── Aquarium.ino          # Contrôle 3 aquariums
│       ├── Aqua.h                # Classe Aqua + Regul
│       └── ModbusSensor.h        # Interface capteurs Modbus
├── MasterFlumes/
│   └── MasterFlumes/
│       ├── MasterFlumes.ino      # Conditionnement eau
│       └── ModbusSensor.h        # Interface capteurs Modbus
├── Flume/
│   └── Flume/Flume/
│       ├── Flume.ino             # Contrôle 4 flumes
│       ├── Flume.h               # Classe Flume + Regul
│       └── ModbusSensor.h        # Interface capteurs Modbus
├── ESP32_MoteurFlumes/
│   └── ESP32_MoteurFlumes/
│       └── ESP32_MoteurFlumes.ino # Contrôle 8 moteurs ESC
├── SuperviFlume/
│   └── SuperviFlume/
│       ├── MainWindow.xaml.cs    # Serveur WebSocket principal
│       ├── MainWindow.xaml       # Interface WPF
│       └── App.config            # Configuration InfluxDB/FTP
└── testServo/                    # Projets de test
    testPression/
    testESC/
```

---

## Évolutions futures

### Améliorations proposées

1. **Sécurité** :
   - Authentification WebSocket
   - Chiffrement TLS/SSL
   - Gestion utilisateurs et permissions

2. **Redondance** :
   - Double serveur SuperviFlume (failover)
   - Stockage local sur Arduino (SD card)

3. **Monitoring** :
   - Dashboard Grafana
   - Alertes email/SMS
   - Logs centralisés (ELK stack)

4. **Calibration** :
   - Assistant calibration dans interface web
   - Planification calibrations périodiques
   - Historique calibrations

5. **Analyses** :
   - Calculs statistiques (moyenne, écart-type)
   - Détection anomalies (ML)
   - Rapports automatiques

---

## Auteur et licence

**Auteur** : Pierre (CNRS)

**Date de création** : 2024

**Licence** : Usage interne recherche

---

## Support

Pour toute question ou problème :
1. Consulter les logs Arduino (Serial Monitor)
2. Vérifier MessageTextBox SuperviFlume
3. Consulter documentation capteurs (Hamilton, PODOC)
4. Contacter l'équipe technique

---

## Références

- **Frontend web** : https://github.com/purrutti/flumeswebsite
- **InfluxDB** : https://docs.influxdata.com/
- **Arduino PID Library** : https://playground.arduino.cc/Code/PIDLibrary/
- **Hamilton Modbus** : Documentation technique capteurs pH
- **IndustrialShields** : https://www.industrialshields.com/
