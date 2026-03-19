using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace SuperviFlume_v2
{
    // ── Paramètres globaux des alarmes ────────────────────────────────────────
    public class AlarmSettings
    {
        public bool   TemperatureEnabled { get; set; }
        public double TemperatureDelta   { get; set; } = 2.0;

        public bool   PHEnabled { get; set; }
        public double PHDelta   { get; set; } = 0.5;

        public bool   O2Enabled { get; set; }
        public double O2Min     { get; set; } = 80.0;

        public bool   FlowrateEnabled { get; set; }
        public double FlowrateMin     { get; set; } = 1.0;

        public bool   SpeedEnabled { get; set; }
        public double SpeedMin     { get; set; } = 0.1;
    }

    // ── Alarme individuelle ───────────────────────────────────────────────────
    public class Alarme
    {
        public int      AquaID       { get; set; }
        public string   Libelle      { get; set; }
        public string   Variant      { get; set; }   // "alarm" | "warning"
        public int      Comparaison  { get; set; }   // 0=upper 1=lower 2=both

        public TimeSpan Delay        { get; set; }
        public double   Threshold    { get; set; }
        public double   Delta        { get; set; }
        public double   Value        { get; set; }

        public bool     Enabled      { get; set; }
        public bool     Triggered    { get; set; }
        public bool     Raised       { get; set; }
        public bool     Acknowledged { get; set; }
        public bool     MustSend     { get; set; }

        public DateTime DTTriggered    { get; set; }
        public DateTime DTRaised       { get; set; }
        public DateTime DTAcknowledged { get; set; }

        public Alarme(int id, string libelle, int comp, TimeSpan delay, string variant)
        {
            AquaID = id; Libelle = libelle; Comparaison = comp; Delay = delay; Variant = variant;
        }

        public void SetAlarm(bool enabled, double threshold, double delta, double value)
        {
            Enabled = enabled; Threshold = threshold; Delta = delta; Value = value;
        }

        // Retourne true si l'alarme est levée (raised)
        public bool CheckAndRaise()
        {
            if (!Enabled) { Triggered = false; Raised = false; return false; }

            bool cond = false;
            if (Comparaison == 0 || Comparaison == 2) cond |= Value > Threshold + Delta;
            if (Comparaison == 1 || Comparaison == 2) cond |= Value < Threshold - Delta;

            if (cond && !Triggered) { Triggered = true; DTTriggered = DateTime.Now; }
            if (!cond)              { Triggered = false; Raised = false; Acknowledged = false; }

            if (Triggered && !Raised && DateTime.Now >= DTTriggered + Delay)
            { Raised = true; DTRaised = DateTime.Now; MustSend = true; }

            return Raised;
        }

        public void Acknowledge()
        {
            Acknowledged = true; DTAcknowledged = DateTime.Now;
            Triggered = false;
            Raised = false;

        }
    }

    // ── Gestionnaire d'alarmes ─────────────────────────────────────────────────
    public class AlarmManager
    {
        private static readonly string SettingsFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "alarm_settings.json");

        private static readonly HttpClient _http = new HttpClient();

        private readonly List<Alarme> _alarms = new List<Alarme>();

        public IReadOnlyList<Alarme> Alarms => _alarms;

        public ObservableCollection<Alarme> ActiveAlarms { get; } =
            new ObservableCollection<Alarme>();

        public AlarmSettings Settings { get; set; }

        public AlarmManager()
        {
            Settings = LoadSettings();
            BuildAlarmList();
        }

        private void BuildAlarmList()
        {
            var delay = TimeSpan.FromSeconds(5);

            for (int i = 1; i <= 12; i++)
            {
                _alarms.Add(new Alarme(i, $"Temperature Aqua {i}",  2, delay, "alarm"));
                _alarms.Add(new Alarme(i, $"pH Aqua {i}",           2, delay, "warning"));
                _alarms.Add(new Alarme(i, $"O2 Aqua {i}",           1, delay, "warning"));
                _alarms.Add(new Alarme(i, $"Flowrate Aqua {i}",     1, delay, "warning"));
            }

            for (int i = 1; i <= 8; i++)
            {
                int devId = i + 12;
                _alarms.Add(new Alarme(devId, $"Temperature Flume {i}", 2, delay, "alarm"));
                _alarms.Add(new Alarme(devId, $"pH Flume {i}",          2, delay, "warning"));
                _alarms.Add(new Alarme(devId, $"O2 Flume {i}",          1, delay, "warning"));
                _alarms.Add(new Alarme(devId, $"Flowrate Flume {i}",    1, delay, "warning"));
                _alarms.Add(new Alarme(devId, $"Speed Flume {i}",       1, delay, "warning"));
            }
        }

        // Appelé depuis le thread UI après chaque mise à jour de données
        public void Evaluate(IList<Aquarium> aquariums)
        {
            foreach (var alarm in _alarms)
            {
                int idx = alarm.AquaID - 1;
                if (idx < 0 || idx >= aquariums.Count) continue;

                var aq  = aquariums[idx];
                bool ena = aq.state > 0;

                if (alarm.Libelle.StartsWith("Temperature"))
                    alarm.SetAlarm(ena && Settings.TemperatureEnabled,
                        aq.regulTemp?.consigne ?? 0, Settings.TemperatureDelta, aq.temperature);
                else if (alarm.Libelle.StartsWith("pH"))
                    alarm.SetAlarm(ena && Settings.PHEnabled,
                        aq.regulpH?.consigne ?? 7, Settings.PHDelta, aq.pH);
                else if (alarm.Libelle.StartsWith("O2"))
                    alarm.SetAlarm(ena && Settings.O2Enabled,
                        Settings.O2Min, 0, aq.oxy);
                else if (alarm.Libelle.StartsWith("Flowrate"))
                    alarm.SetAlarm(ena && Settings.FlowrateEnabled,
                        Settings.FlowrateMin, 0, aq.debit);
                else if (alarm.Libelle.StartsWith("Speed"))
                    alarm.SetAlarm(ena && Settings.SpeedEnabled,
                        Settings.SpeedMin, 0, aq.debitCircul);

                alarm.CheckAndRaise();
            }

            SyncActiveAlarms();
        }

        private void SyncActiveAlarms()
        {
            // Retire les alarmes retombées
            foreach (var a in ActiveAlarms.Where(x => !x.Raised).ToList())
                ActiveAlarms.Remove(a);

            // Ajoute les nouvelles alarmes levées
            foreach (var a in _alarms.Where(x => x.Raised))
                if (!ActiveAlarms.Contains(a))
                    ActiveAlarms.Add(a);

            // Envoie les notifications Slack pour les nouvelles alarmes
            foreach (var a in _alarms.Where(x => x.MustSend))
            {
                a.MustSend = false;
                _ = SendSlackAsync(a);
            }
        }

        private async System.Threading.Tasks.Task SendSlackAsync(Alarme alarm)
        {
            try
            {
                string url = ConfigurationManager.AppSettings["SlackWebhookUrl"];
                if (string.IsNullOrEmpty(url)) return;

                string emoji  = alarm.Variant == "alarm" ? ":rotating_light:" : ":warning:";
                string text   = $"{emoji} *{alarm.Libelle}* — valeur : {alarm.Value:F2}, seuil : {alarm.Threshold:F2}";
                string json   = JsonConvert.SerializeObject(new { text });

                await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        public AlarmSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var s = JsonConvert.DeserializeObject<AlarmSettings>(
                                File.ReadAllText(SettingsFile));
                    if (s != null) return s;
                }
            }
            catch { }
            return new AlarmSettings();
        }

        public void SaveSettings()
        {
            try
            {
                File.WriteAllText(SettingsFile,
                    JsonConvert.SerializeObject(Settings, Formatting.Indented));
            }
            catch { }
        }
    }
}
