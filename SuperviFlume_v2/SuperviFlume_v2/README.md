# SuperviFlume v2

Application WPF de supervision en temps réel des aquariums et flumes de la **Station de Biologie Marine de Roscoff** (CNRS).

---

## Architecture

```
SuperviFlume_v2/
├── MainWindow.xaml(.cs)          – Interface principale (3 onglets)
├── Models.cs                     – Modèles de données (Aquarium, MasterData…)
├── WebSocketServer.cs            – Serveur HTTP/WebSocket (réception JSON du PLC)
├── Alarms.cs                     – Moteur d'alarmes (AlarmManager, Alarme, AlarmSettings)
├── AlarmsWindow.xaml(.cs)        – Fenêtre de paramétrage et visualisation des alarmes
├── SetpointsWindow.xaml(.cs)     – Fenêtre de saisie des consignes
├── SensorCalibration.xaml(.cs)   – Fenêtre de calibration des capteurs
├── GeneralInletParamsWindow.xaml – Fenêtre des paramètres d'arrivée d'eau générale
└── LogWindow.xaml(.cs)           – Journal des messages WebSocket
```

---

## Fonctionnalités

### Supervision temps réel
- **12 aquariums** et **8 flumes** mis à jour à chaque réception de trame WebSocket
- Affichage par appareil : débit (Q), température (T), pH, O₂, vitesse de courant (flumes)
- Consignes et sorties PID température et pH
- **État par appareil** : `DISABLED` / `CONTROL` / `TREATMENT`
  - Les appareils désactivés sont affichés en grisé
- Onglet eau générale : circuits froid, chaud et ambiant (T, pression, débit, PID)

### Gestion des alarmes
- Alarmes configurables indépendamment pour chaque type de mesure :

| Mesure      | Type       | Logique              |
|-------------|------------|----------------------|
| Température | Alarm      | Écart > delta (°C) par rapport à la consigne |
| pH          | Warning    | Écart > delta par rapport à la consigne |
| O₂          | Warning    | Valeur < seuil minimum (%) |
| Débit       | Warning    | Valeur < seuil minimum (L/mn) |
| Vitesse     | Warning    | Valeur < seuil minimum (m/s) — flumes uniquement |

- **Délai de déclenchement** : 5 secondes de condition continue avant levée
- **Auto-reset** : l'alarme retombe automatiquement dès que la condition disparaît
- **Indicateur visuel** : le label de la mesure en défaut affiche `⚠` en rouge (alarm) ou orange (warning) dans la fenêtre principale
- **Notification Slack** : envoi d'un message via Incoming Webhook à chaque nouvelle alarme levée
- **Acquittement** manuel depuis la fenêtre AlarmsWindow
- Paramètres persistés dans `alarm_settings.json` (dossier de l'exécutable)

### Envoi de données
- **CSV** : export périodique (intervalle configurable) dans un fichier journalier
- **InfluxDB** : écriture en line protocol vers une instance locale
- **Consignes** : envoi de commandes JSON vers le PLC via WebSocket broadcast

---

## Configuration (`App.config`)

| Clé                | Description                                      | Valeur par défaut         |
|--------------------|--------------------------------------------------|---------------------------|
| `serverUrl`        | Adresse d'écoute du serveur WebSocket            | `http://192.168.73.14:81/`|
| `dataLogInterval`  | Intervalle de sauvegarde CSV/InfluxDB (minutes)  | `1`                       |
| `dataFileBasePath` | Dossier de sortie des fichiers CSV               | `C:\Users\...\data\`      |
| `InfluxDBUrl`      | URL de l'instance InfluxDB                       | `http://localhost:8086`   |
| `InfluxDBToken`    | Token d'authentification InfluxDB                | *(à renseigner)*          |
| `InfluxDBBucket`   | Bucket InfluxDB cible                            | `Flumes`                  |
| `InfluxDBOrg`      | Organisation InfluxDB                            | `LOV`                     |
| `SlackWebhookUrl`  | URL du webhook Incoming Slack                    | *(à renseigner)*          |

> **Sécurité** : ne pas versionner les tokens et webhooks. Renseigner `SlackWebhookUrl` et `InfluxDBToken` directement dans `App.config` en local, sans commiter.

---

## Prérequis

- Windows 10+
- .NET Framework 4.7.2
- Visual Studio 2019 ou supérieur
- InfluxDB v2 (optionnel, pour l'export de données)
- Un workspace Slack avec un Incoming Webhook configuré (optionnel)

## Compilation

Ouvrir `SuperviFlume_v2.sln` dans Visual Studio, restaurer les packages NuGet, puis compiler en Debug ou Release.
