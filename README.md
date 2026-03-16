# Flumes

Système de supervision et de régulation des aquariums et flumes de la **Station de Biologie Marine de Roscoff** (CNRS – LOV).

---

## Vue d'ensemble

Le système est composé de plusieurs automates Arduino communiquant via WebSocket avec une application de supervision Windows. Chaque automate gère un sous-ensemble de l'installation et envoie ses données en JSON au serveur central.

```
┌─────────────────────────────────────────────────────────────────────┐
│  Aquariums PLC ×4 (Arduino Mega)  ──── Ethernet ────┐               │
│  MasterFlumes PLC  (Arduino Mega)  ─── Ethernet ────┤─ WebSocket ──►│ SuperviFlume_v2
│  Flumes ESP (ESP32 / ESP8266)      ─── WiFi ─────────┘              │  (PC Windows)
└─────────────────────────────────────────────────────────────────────┘
                                                              │
                                              ┌───────────────┴───────────────┐
                                          InfluxDB                      Notifications
                                           + CSV                           Slack
```

---

## Composants

### `Aquarium/` — Automate aquarium (Arduino Mega)

Quatre cartes identiques (PLCID 1 à 4), chacune supervisant **3 aquariums** (12 au total).

| Élément | Détail |
|---|---|
| **Capteurs** | pH et température via sondes Hamilton (Modbus RTU RS-485) ; débit (4-20 mA) |
| **Actionneurs** | Vannes 3 voies eau chaude/froide (PWM) ; injection CO₂ (tout-ou-rien) |
| **Régulation** | PID température (chaud ou froid selon consigne) ; PID pH (REVERSE) |
| **Persistance** | Paramètres PID et consignes sauvegardés en EEPROM |
| **Réseau** | Ethernet Wiznet, client WebSocket (bibliothèque WebSocketsClient) |

Adressage IP statique : `172.16.36.20X` (X = PLCID).

---

### `MasterFlumes/` — Automate eau générale (Arduino Mega)

Une carte unique (PLCID 5) supervisant l'arrivée d'eau commune à l'installation.

| Circuit | Capteurs | Actionneurs | Régulation |
|---|---|---|---|
| **Eau chaude** | Pression (4-20 mA), Débit (4-20 mA), T° PAC (4-20 mA) | Vanne V3V (PWM), Vanne V2V (PWM) | PID température (PAC chaud) + PID pression |
| **Eau froide** | Pression, Débit, T° PAC | Vanne V3V, Vanne V2V | PID température (PAC froid, REVERSE) + PID pression |
| **Eau ambiante** | Pression, Débit, pH et T° ambiants (Hamilton Modbus) | Vanne V2V | PID pression — bloqué si alarme niveau |

Fonctions supplémentaires :
- **Alarme niveau** : coupure automatique de la vanne ambiante si flotteur déclenché
- Persistance des paramètres en EEPROM

---

### `ESP01WEBSERVER/` — Contrôleur de courant flume (ESP32 / ESP8266)

Serveur web embarqué pour le contrôle du moteur brushless (ESC) de chaque flume via un signal servo PWM.

| Élément | Détail |
|---|---|
| **Réseau** | Point d'accès WiFi dédié (SSID : `FLUME`) |
| **Interface** | Page HTML simple : saisie de la valeur de commande (1100–1900 µs) |
| **Commande** | Signal servo sur pin 4 via `ESP32Servo` |

---

### `SuperviFlume_v2/` — Application de supervision (Windows WPF, .NET 4.7.2)

Application centrale recevant et affichant les données de tous les automates en temps réel.

**Principales fonctionnalités :**

- Supervision des **12 aquariums** et **8 flumes** (température, pH, O₂, débit, vitesse)
- État par appareil : `DISABLED` / `CONTROL` / `TREATMENT` avec grisement visuel
- Onglet **eau générale** : circuits chaud, froid et ambiant (T, pression, débit, PID)
- **Consignes** modifiables depuis l'interface (envoi JSON au PLC via broadcast WebSocket)
- **Calibration** des capteurs Hamilton depuis l'interface
- **Gestion des alarmes** :
  - Température (écart > delta par rapport à la consigne)
  - pH (écart > delta par rapport à la consigne)
  - O₂, débit, vitesse (seuil minimum)
  - Indicateur ⚠ sur le label de la mesure en défaut (rouge = alarme, orange = warning)
  - Notification **Slack** par Incoming Webhook à chaque nouvelle alarme
- Export **CSV** (fichier journalier) et **InfluxDB**

---

## Protocole de communication

Les automates et l'application échangent des messages JSON via WebSocket. Les commandes sont identifiées par un champ `cmd` :

| Code | Direction | Description |
|---|---|---|
| `0` | PC → PLC | Demande de paramètres |
| `1` | PC → PLC | Demande de données |
| `2` | PC → PLC / PLC → PC | Envoi de paramètres |
| `3` | PLC → PC | Envoi de données mesurées |
| `4` | PC → PLC | Commande de calibration capteur |
| `5` | PC → PLC | Demande de données générales (MasterFlumes) |
| `6` | PLC → PC | Envoi de données générales (MasterFlumes) |

Chaque trame inclut un `PLCID` (identifiant automate) et un horodatage Unix.

---

## Configuration

Les paramètres réseau et applicatifs de SuperviFlume_v2 se configurent dans `SuperviFlume_v2/SuperviFlume_v2/SuperviFlume_v2/App.config` :

| Clé | Description |
|---|---|
| `serverUrl` | Adresse d'écoute du serveur WebSocket |
| `dataLogInterval` | Intervalle de sauvegarde CSV/InfluxDB (minutes) |
| `dataFileBasePath` | Dossier de sortie CSV |
| `InfluxDBUrl` / `InfluxDBToken` / `InfluxDBBucket` / `InfluxDBOrg` | Paramètres InfluxDB |
| `SlackWebhookUrl` | URL du webhook Incoming Slack |

> **Sécurité** : ne pas versionner les tokens et webhooks — les renseigner uniquement en local.

---

## Prérequis

| Composant | Prérequis |
|---|---|
| Automates Arduino | Arduino IDE ≥ 2.x ; bibliothèques : `WebSocketsClient`, `ModbusRtu`, `ArduinoJson`, `EEPROMex`, `PID_v1`, `TimeLib`, `Ethernet` |
| ESP32 | Arduino IDE avec support ESP32/ESP8266 ; `ESPAsyncWebServer`, `ESP32Servo` |
| SuperviFlume_v2 | Windows 10+, .NET Framework 4.7.2, Visual Studio 2019+ |
| Optionnel | InfluxDB v2 (export données) ; workspace Slack (notifications) |
