using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Configuration;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Markup;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using RestSharp.Serializers;
using System.Reflection;
using System.Windows.Media;
using NodaTime.Calendars;
using System.Windows.Controls;
using System.Windows.Input;

namespace WebSocketServerExample
{
    public class PreserveExistingPropertiesConverter<T> : JsonConverter<T> where T : class, new()
    {
        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            if (!hasExistingValue)
                existingValue = new T();

            foreach (var property in typeof(T).GetProperties())
            {
                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (attribute != null)
                {
                    var jsonPropertyName = attribute.PropertyName;
                    if (jObject.ContainsKey(jsonPropertyName))
                    {
                        var value = jObject[jsonPropertyName].ToObject(property.PropertyType, serializer);
                        property.SetValue(existingValue, value);
                    }
                }
                else if (jObject.ContainsKey(property.Name))
                {
                    var value = jObject[property.Name].ToObject(property.PropertyType, serializer);
                    property.SetValue(existingValue, value);
                }
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, T value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }


        public static class JsonHelper
        {
            public static T DeserializePreservingExisting<T>(string json, T existingObject = null) where T : class, new()
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new PreserveExistingPropertiesConverter<T>() }
                };

                var serializer = Newtonsoft.Json.JsonSerializer.Create(settings);
                var jObject = JObject.Parse(json);

                if (existingObject == null)
                    existingObject = new T();

                serializer.Populate(jObject.CreateReader(), existingObject);

                return existingObject;
            }
        }
        public class TrameJson
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int cmd { get; set; }
        [JsonProperty("AquaID", Required = Required.Default)]
        public int ID { get; set; }
        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }
    }
    public class dataRequest
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int cmd { get; set; }
        [JsonProperty("startDate", Required = Required.Default)]
        public DateTime From { get; set; }
        [JsonProperty("endDate", Required = Required.Default)]
        public DateTime To { get; set; }
    }
    public class MasterData
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int Command { get; set; }

        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }

        [JsonProperty("data", Required = Required.Default)]
        public List<DataItem> Data { get; set; }

        [JsonProperty("time", Required = Required.Default)]
        public long Time { get; set; }

        public MasterData()
        {
            Data = new List<DataItem>();
        }

        
    }

    public class DataItem
    {
        [JsonProperty("CondID", Required = Required.Default)]
        public string ConditionID { get; set; }

        [JsonProperty("temp", Required = Required.Default)]
        public double Temperature { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }

        [JsonProperty("pression", Required = Required.Default)]
        public double Pression { get; set; }

        [JsonProperty("debit", Required = Required.Default)]
        public double Debit { get; set; }

        [JsonProperty("rTemp", Required = Required.Default)]
        public Regul RTemp { get; set; }

        [JsonProperty("rPression", Required = Required.Default)]
        public Regul RPression { get; set; }
    }
    public class Aquarium
    {
        [JsonProperty("AquaID", Required = Required.Default)]
        public int ID { get; set; }
        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }

        [JsonProperty("state", Required = Required.Default)]
        public int state { get; set; }


        [JsonProperty("debit", Required = Required.Default)]
        public double debit { get; set; }
        [JsonProperty("temp", Required = Required.Default)]
        public double temperature { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }
        [JsonProperty("oxy", Required = Required.Default)]
        public double oxy { get; set; }
        [JsonProperty("debitCircul", Required = Required.Default)]
        public double vitesse { get; set; }
        [JsonProperty("rTemp", Required = Required.Default)]
        public Regul regulTemp { get; set; }
        [JsonProperty("rpH", Required = Required.Default)]
        public Regul regulpH { get; set; }
        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }
    /*public class Flume
    {
        [JsonProperty("AquaID", Required = Required.Default)]
        public int ID { get; set; }
        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }

        [JsonProperty("state", Required = Required.Default)]
        public int state { get; set; }


        [JsonProperty("debit", Required = Required.Default)]
        public double debit { get; set; }
        [JsonProperty("vitesse", Required = Required.Default)]
        public double vitesse { get; set; }
        [JsonProperty("consingeVitesse", Required = Required.Default)]
        public double consigneVitesse { get; set; }
        [JsonProperty("temp1", Required = Required.Default)]
        public double temperature1 { get; set; }
        [JsonProperty("pH1", Required = Required.Default)]
        public double pH1 { get; set; }
        [JsonProperty("temp2", Required = Required.Default)]
        public double temperature2 { get; set; }
        [JsonProperty("pH2", Required = Required.Default)]
        public double pH2 { get; set; }
        [JsonProperty("oxy", Required = Required.Default)]
        public double oxy { get; set; }
        [JsonProperty("rTemp", Required = Required.Default)]
        public Regul regulTemp { get; set; }
        [JsonProperty("rpH", Required = Required.Default)]
        public Regul regulpH { get; set; }
        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }*/

    public class Regul
    {
        [JsonProperty(Required = Required.Default)]
        public double sortiePID { get; set; }
        [JsonProperty("cons", Required = Required.Default)]
        public double consigne { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double Kp { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double Ki { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double Kd { get; set; }
        [JsonProperty("sPID_pc", Required = Required.Default)]
        public double sortiePID_pc { get; set; }
        [JsonProperty(Required = Required.Default)]
        public bool autorisationForcage { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double consigneForcage { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double offset { get; set; }
        [JsonProperty(Required = Required.Default)]
        public bool chaudFroid { get; set; }//true = chaud
        [JsonProperty(Required = Required.Default)]
        public bool useOffset { get; set; }//true = chaud

    }

    public class AlarmSettings
    {
        public bool waterPressureEnabled { get; set; }
        public double waterPressureValue { get; set; }
        public bool flowRatesEnabled { get; set; }
        public double flowRatesValue1 { get; set; }
        public double flowRatesValue2 { get; set; }

        public bool temperatureEnabled { get; set; }
        public double temperatureValue { get; set; }
        public bool pHEnabled { get; set; }
        public double pHValue { get; set; }
        public bool o2Enabled { get; set; }
        public double o2Value { get; set; }
        public bool speedEnabled { get; set; }
        public double speedValue { get; set; }
    }

    public class Alarme
    {
        public string libelle { get; set; }
        public int AquaID { get; set; }
        public DateTime dtTriggered { get; set; }
        public DateTime dtRaised { get; set; }
        public DateTime dtAcknowledged { get; set; }
        public TimeSpan delay { get; set; }
        public double threshold { get; set; }
        public double delta { get; set; }
        public double value { get; set; }
        public bool enabled { get; set; }
        public bool triggered { get; set; }
        public bool raised { get; set; }
        public bool acknowledged { get; set; }
        public int comparaison { get; set; }
        public bool mustSend { get; set; }
        public string variant { get; set; }
        public Alarme(int ID, string l, int comp, TimeSpan d, string s)
        {
            AquaID = ID;
            libelle = l;
            comparaison = comp;
            mustSend = false;
            variant = s;
            delay = d;
        }
        public Alarme()
        {
        }

        public void setAlarm(bool ena, double th, double measure)
        {
            enabled = ena;
            threshold = th;
            value = measure;
        }
        public bool checkAndRaise() // raise alarm if value is upperThan threshold
        {
            if (!enabled) return false;
            bool upperThan, lowerThan;
            switch (comparaison)
            {
                case 0:

                    upperThan = true;
                    lowerThan = false;
                    break;
                case 1:
                    upperThan = false;
                    lowerThan = true;
                    break;
                case 2:
                    upperThan = true;
                    lowerThan = true;
                    break;
                default:
                    upperThan = false;
                    lowerThan = false;
                    break;
            }


            bool t = false;
            if (triggered) t = true;
            if (!triggered && upperThan && value >= (threshold + delta))
            {
                dtTriggered = DateTime.Now;
                t = true;
            }
            if (!triggered && lowerThan && value <= (threshold - delta))
            {
                dtTriggered = DateTime.Now;
                t = true;
            }
            triggered = t;

            if (!raised && triggered && dtTriggered.Add(delay) < DateTime.Now)
            {
                raised = true;
                dtRaised = DateTime.Now;
                //sendSlackMessage(dtTriggered.ToString() + ":" + this.libelle + string.Format(" Measure = {0:0.00}, ", value) + string.Format("Setpoint = {0:0.00}", threshold));
                mustSend = true;


            }
            if (raised) return true;
            return false;
        }
    }


    /*public void sendSlackMessage(String msg)
    {
        string TOKEN = Properties.Settings.Default["SlackToken"].ToString();  // token from last step in section above
        var slackClient = new SlackTaskClient(TOKEN);

        slackClient.PostMessageAsync(Properties.Settings.Default["SlackChannelID"].ToString(), msg);
    }

    public bool checkAndRaise(bool val, bool th) // raise alarm if value is upperThan threshold
    {
        if (!enabled) return false;


        if (!triggered && val != th)
        {
            triggered = true;
            dtTriggered = DateTime.Now;
        }
        else triggered = false;

        if (!raised && triggered && dtTriggered.Add(delay) > DateTime.Now)
        {
            raised = true;
            dtRaised = DateTime.Now;
            sendSlackMessage(this.libelle + " triggered at:" + dtTriggered.ToString());

        }
        if (raised) return true;
        return false;
    }*/


    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private ConcurrentDictionary<Guid, WebSocket> _webSockets;

        private ConcurrentDictionary<int, TaskCompletionSource<String>> _responseTasks = new ConcurrentDictionary<int, TaskCompletionSource<String>>();


        public ObservableCollection<Alarme> alarms { get; set; }
        public ObservableCollection<Aquarium> aquariums { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        public AlarmSettings alarmSettings { get; set; }

        string token = ConfigurationManager.AppSettings["InfluxDBToken"].ToString();
        string bucket = ConfigurationManager.AppSettings["InfluxDBBucket"].ToString();
        string org = ConfigurationManager.AppSettings["InfluxDBOrg"].ToString();

        CancellationTokenSource cts = new CancellationTokenSource();

        InfluxDBClient client;

        MasterData md;

        double debitTotal = 0;

        private bool isUserScrolling = false;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public MainWindow()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Flumes Application is already running. Only one instance of this application is allowed");
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                InitializeComponent();
                InitializeAsync();

                MessageScrollViewer.ScrollChanged += MessageScrollViewer_ScrollChanged;
                MessageScrollViewer.PreviewMouseDown += MessageScrollViewer_PreviewMouseDown;
                MessageScrollViewer.PreviewMouseUp += MessageScrollViewer_PreviewMouseUp;



                client = InfluxDBClientFactory.Create("http://localhost:8086", token.ToCharArray());
                _webSockets = new ConcurrentDictionary<Guid, WebSocket>();

                ServerStatusLabel.Content = "Server Stopped";
                DataContext = this; // Set DataContext to this instance
                aquariums = new ObservableCollection<Aquarium>();
                md = new MasterData();
                alarms = new ObservableCollection<Alarme>();

                alarmSettings = LoadFromConfig();
                alarms.Add(new Alarme(0,"Hot Water Pressure",2,TimeSpan.FromSeconds(5),"alarm"));
                alarms.Add(new Alarme(0, "Cold Water Pressure", 2, TimeSpan.FromSeconds(5), "alarm"));
                alarms.Add(new Alarme(0, "Ambient Water Flowrate", 2, TimeSpan.FromSeconds(5), "alarm"));
                alarms.Add(new Alarme(0, "Hot Water Temperature", 2, TimeSpan.FromSeconds(5), "alarm"));
                alarms.Add(new Alarme(0, "Cold Water Temperature", 2, TimeSpan.FromSeconds(5), "alarm"));
                alarms.Add(new Alarme(0, "Drain Overflow", 2, TimeSpan.FromSeconds(5), "alarm"));

                for (int i = 0; i < 20; i++)
                {
                    Aquarium a = new Aquarium();
                    a.ID = i + 1;
                    a.regulpH = new Regul();
                    a.regulTemp = new Regul();
                    //a.state = 0;
                    aquariums.Add(a);

                    alarms.Add(new Alarme(a.ID,"Temperature Aqua " + a.ID, 2, TimeSpan.FromSeconds(5), "alarm"));
                    alarms.Add(new Alarme(a.ID, "pH Aqua " + a.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(a.ID, "O2 Aqua " + a.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(a.ID, "Flowrate Aqua " + a.ID, 2, TimeSpan.FromSeconds(5), "warning"));

                }
                /*for (int i = 12; i <= 20; i++)
                {
                    Flume f = new Flume();
                    f.ID = i ;
                    f.pH1 = 7.2; f.pH2 = 7.4;
                    f.temperature1 = 21.4;f.temperature2 = 22.4;
                    //f.state = 0;
                    f.consigneVitesse = 12.4;
                    f.vitesse = 11.8;
                    f.debit = 7.7;
                    f.oxy = 102.3;
                    f.regulpH = new Regul();
                    f.regulTemp = new Regul();
                    f.regulpH.consigne = 8.1;
                    f.regulpH.sortiePID_pc = 21;
                    f.regulTemp.consigne = 22.5;
                    f.regulTemp.sortiePID_pc = 45.6;
                    f.regulTemp.chaudFroid = true;
                    flumes.Add(f);
                    alarms.Add(new Alarme(f.ID, "Temperature 1 Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "pH 1 Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "Temperature 2 Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "pH 2 Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "O2 Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "Speed Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                    alarms.Add(new Alarme(f.ID, "Flowrate Flume " + f.ID, 2, TimeSpan.FromSeconds(5), "warning"));
                }*/
                AquariumsDataGrid.ItemsSource = aquariums;
                MasterDataDataGrid.ItemsSource = md.Data;
            }
        }
        private void MessageScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isUserScrolling = true;
        }

        private void MessageScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            // We'll determine if we should reset isUserScrolling in the ScrollChanged event
        }

        private void MessageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // If the user scrolled to the end, allow auto-scrolling again
            if (MessageScrollViewer.VerticalOffset == MessageScrollViewer.ScrollableHeight)
            {
                isUserScrolling = false;
            }
        }

        private void AppendToTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageTextBox.AppendText(message + Environment.NewLine);

                // Keep only the last 30 lines
                var lines = MessageTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 30)
                {
                    MessageTextBox.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - 30));
                }

                // Only scroll if the user hasn't interacted with the ScrollViewer
                if (!isUserScrolling)
                {
                    MessageScrollViewer.ScrollToEnd();
                }
            });
        }

        public void setAlarms()
        {
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("Temperature")))
            {
                alarm.delta = alarmSettings.temperatureValue;
                if (alarm.AquaID > 0)
                {
                    bool ena = false;
                    if (aquariums[alarm.AquaID - 1].state > 0) ena = alarmSettings.temperatureEnabled;
                    alarm.setAlarm(ena, aquariums[alarm.AquaID - 1].regulTemp.consigne, aquariums[alarm.AquaID - 1].temperature);
                }
                else
                {
                    //TODO
                    /*alarm.enabled = alarmSettings.temperatureEnabled;
                    alarm.setAlarm(true, md.Data[0].RTemp.consigne, md.Data[0].Temperature);
                    alarm.setAlarm(true, md.Data[1].RTemp.consigne, md.Data[1].Temperature);*/
                }
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("pH")))
            {
                alarm.delta = alarmSettings.pHValue; 
                if (alarm.AquaID > 0)
                {
                    bool ena = false;
                    if (aquariums[alarm.AquaID - 1].state > 0) ena = alarmSettings.temperatureEnabled;
                    alarm.setAlarm(ena, aquariums[alarm.AquaID - 1].regulTemp.consigne, aquariums[alarm.AquaID - 1].temperature);
                }
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("02")))
            {
                alarm.delta = alarmSettings.o2Value;
                if (alarm.AquaID > 0)
                {
                    bool ena = false;
                    if (aquariums[alarm.AquaID - 1].state > 0) ena = alarmSettings.o2Enabled;
                    alarm.setAlarm(ena, aquariums[alarm.AquaID - 1].regulTemp.consigne, aquariums[alarm.AquaID - 1].oxy);
                }
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("Speed")))
            {
                alarm.delta = alarmSettings.speedValue;
                if (alarm.AquaID > 12)
                {
                    bool ena = false;
                    if (aquariums[alarm.AquaID - 13].state > 0) ena = alarmSettings.speedEnabled;
                    alarm.setAlarm(ena, 4, aquariums[alarm.AquaID - 13].vitesse);
                }
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("Pressure")))
            {
                /*alarm.delta = alarmSettings.waterPressureValue;
                alarm.enabled = alarmSettings.waterPressureEnabled;
                alarm.setAlarm(true, 4, flumes[alarm.AquaID - 13].vitesse);*/
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("Flowrate")))
            {
                alarm.delta = alarmSettings.flowRatesValue2;
                if (alarm.AquaID > 0)
                {
                    bool ena = false;
                    if (aquariums[alarm.AquaID - 1].state > 0) ena = alarmSettings.flowRatesEnabled;
                    alarm.setAlarm(ena, alarmSettings.flowRatesValue1, aquariums[alarm.AquaID - 1].debit);
                }
                else
                {
                    /*alarm.enabled = alarmSettings.flowRatesEnabled;
                    alarm.setAlarm(true, md.Data[2].RPression.consigne, md.Data[2].Debit);*/
                }
            }
            foreach (var alarm in alarms.Where(a => a.libelle.Contains("Drain Overflow")))
            {
                alarm.enabled = true;
            }            
        }


        public static AlarmSettings LoadFromConfig()
        {
            return new AlarmSettings
            {
                waterPressureEnabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.WaterPressureEnabled"] ?? "false"),
                waterPressureValue = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.WaterPressureValue"] ?? "0"),
                flowRatesEnabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.FlowRatesEnabled"] ?? "false"),
                flowRatesValue1 = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.FlowRatesValue1"] ?? "0"),
                flowRatesValue2 = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.FlowRatesValue2"] ?? "0"),
                temperatureEnabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.TemperatureEnabled"] ?? "false"),
                temperatureValue = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.TemperatureValue"] ?? "0"),
                pHEnabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.PHEnabled"] ?? "false"),
                pHValue = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.PHValue"] ?? "0"),
                o2Enabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.O2Enabled"] ?? "false"),
                o2Value = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.O2Value"] ?? "0"),
                speedEnabled = bool.Parse(ConfigurationManager.AppSettings["AlarmSettings.SpeedEnabled"] ?? "false"),
                speedValue = double.Parse(ConfigurationManager.AppSettings["AlarmSettings.SpeedValue"] ?? "0")
            };
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://192.168.73.14:81/");
            _listener.Prefixes.Add("http://192.168.73.14/getCSV/");
            _listener.Start();
            ServerStatusLabel.Content = "Server started";

            try
            {
                while (true)
                {

                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(() => HandleWebSocketAsync(context));
                    }
                    else if (context.Request.HttpMethod == "OPTIONS" || (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/getCSV"))
                    {
                        await HandleGetCSVRequest(context);
                    }
                    else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        
                    }
                }
            }
            catch (Exception ex) { }

            //await AcceptWebSocketClientsAsync(_cts.Token);
        }
        private async Task HandleGetCSVRequest(HttpListenerContext context)
        {
            // Add CORS headers
            context.Response.AddHeader("Access-Control-Allow-Origin", "http://192.168.73.14:3000");
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
            context.Response.AddHeader("Access-Control-Allow-Credentials", "true");

            // If it's an OPTIONS request (preflight), just return OK
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                context.Response.Close();
                return;
            }
            // Read the request body
            var queryParams = System.Web.HttpUtility.ParseQueryString(context.Request.Url.Query);

            if (!DateTime.TryParse(queryParams["startDate"], out DateTime startDate) ||
                !DateTime.TryParse(queryParams["endDate"], out DateTime endDate))
            {
                context.Response.StatusCode = 400;
                byte[] errorMessage = Encoding.UTF8.GetBytes("Invalid date format. Use YYYY-MM-DD.");
                await context.Response.OutputStream.WriteAsync(errorMessage, 0, errorMessage.Length);
                context.Response.Close();
                return;
            }

            // Generate the combined CSV content
            string combinedCsv = GenerateCombinedCsv(startDate, endDate);

            // Create a zip file containing the combined CSV
            byte[] zipBytes = CreateZipFile(combinedCsv);

            // Send the response
            context.Response.ContentType = "application/zip";
            context.Response.AddHeader("Content-Disposition", "attachment; filename=combined.zip");
            context.Response.ContentLength64 = zipBytes.Length;
            await context.Response.OutputStream.WriteAsync(zipBytes, 0, zipBytes.Length);
            context.Response.Close();
        }

        private string GenerateCombinedCsv(DateTime startDate, DateTime endDate)
        {
            List<string> csvFiles = GetRelevantCsvFiles(startDate, endDate);
            StringBuilder combinedCsv = new StringBuilder();

            bool isFirstFile = true;
            foreach (string file in csvFiles)
            {
                string[] lines = System.IO.File.ReadAllLines(file);

                if (isFirstFile)
                {
                    // For the first file, include all lines (including the header)
                    combinedCsv.AppendLine(string.Join(Environment.NewLine, lines));
                    isFirstFile = false;
                }
                else
                {
                    // For subsequent files, skip the first line (header) and add the rest
                    if (lines.Length > 1)
                    {
                        combinedCsv.AppendLine(string.Join(Environment.NewLine, lines.Skip(1)));
                    }
                }
            }

            return combinedCsv.ToString();
        }

        private List<string> GetRelevantCsvFiles(DateTime startDate, DateTime endDate)
        {
            string baseFilePath = ConfigurationManager.AppSettings["dataFileBasePath"].ToString();
            List<string> relevantFiles = new List<string>();

            for (DateTime dt = startDate; dt <= endDate; dt = dt.AddDays(1))
            {
                string filePath = baseFilePath + "data_" + dt.ToString("yyyy-MM-dd") + ".csv";
                if (System.IO.File.Exists(filePath))
                {
                    relevantFiles.Add(filePath);
                }
            }

            return relevantFiles;
        }

        private byte[] CreateZipFile(string content)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("combined.csv");
                    using (StreamWriter writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(content);
                    }
                }
                return ms.ToArray();
            }
        }

    public class DateRange
    {
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
        {

            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            var webSocket = webSocketContext.WebSocket;
            var id = Guid.NewGuid();

            _webSockets.TryAdd(id, webSocket); // Ajouter le WebSocket à la collection


            var buffer = new byte[1024];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        AppendToTextBox($"Received: {message}");
                        await ReadData(message, webSocket);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                _webSockets.TryRemove(id, out _); // Retirer le WebSocket de la collection
            }
        }
        public async Task BroadcastMessageAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = _webSockets.Values.Select(async webSocket =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    Task sendTask = webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                     //webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    sendTask.GetAwaiter().GetResult();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }




        public async Task ReadData(string data, WebSocket ws)
        {
            string broadcastMessage;
            String s="";
            byte[] buffer;
            if (!data.Contains("Connected"))
            {
                try
                {
                    TrameJson t = JsonConvert.DeserializeObject<TrameJson>(data);
                    if (t.PLCID > 5)
                    {

                    }

                    switch (t.cmd)
                    {
                        case 0://REQ PARAMS ==> send params to aqua or frontend
                            if (t.ID == 0)//Master params
                            {
                                if (md.Data != null)
                                {

                                    var response = new
                                    {
                                        cmd = 2,
                                        AquaID = 0,
                                        md.PLCID,
                                        md.Data
                                    };

                                    s = JsonConvert.SerializeObject(response);
                                }

                            }
                            else
                                if(md.Data != null)
                                {

                                    var response = new
                                    {
                                        cmd = 2,
                                        AquaID = aquariums[t.ID - 1].ID,
                                        aquariums[t.ID - 1].PLCID,
                                        aquariums[t.ID - 1].state,
                                        aquariums[t.ID - 1].debit,
                                        aquariums[t.ID - 1].temperature,
                                        aquariums[t.ID - 1].pH,
                                        aquariums[t.ID - 1].oxy,
                                        rTemp = aquariums[t.ID - 1].regulTemp,
                                        rpH = aquariums[t.ID - 1].regulpH,
                                    };

                                    s = JsonConvert.SerializeObject(response);
                                }

                            

                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 1://REQ DATA ==> irrelevant
                            break;
                        case 2://SEND PARAMS ==> receive params from aqua
                            if (t.ID <= 0)
                            {
                                md = JsonConvert.DeserializeObject<MasterData>(data);
                                //broadcastMessage = JsonConvert.SerializeObject(md);

                                var resp = new
                                {
                                    cmd = 2,
                                    AquaID = 0,
                                    md.Data[0].ConditionID,
                                    md.PLCID,
                                    md.Time,
                                    md.Data
                                };

                                broadcastMessage = JsonConvert.SerializeObject(resp);
                                await BroadcastMessageAsync(broadcastMessage);
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        aquariums[t.ID - 1] = JsonHelper.DeserializePreservingExisting<Aquarium>(data, aquariums[t.ID - 1]);

                                    }
                                    catch (Exception ex) { }
                                });

                                var response = new
                                {
                                    cmd = 2,
                                    AquaID = aquariums[t.ID - 1].ID,
                                    aquariums[t.ID - 1].PLCID,
                                    aquariums[t.ID - 1].state,
                                    aquariums[t.ID - 1].debit,
                                    aquariums[t.ID - 1].temperature,
                                    aquariums[t.ID - 1].pH,
                                    aquariums[t.ID - 1].oxy,
                                    rTemp = aquariums[t.ID - 1].regulTemp,
                                    rpH = aquariums[t.ID - 1].regulpH,
                                };

                                broadcastMessage = JsonConvert.SerializeObject(response);
                                await BroadcastMessageAsync(broadcastMessage);
                            }
                            

                            break;
                        case 3://SEND DATA ==> receive data from aqua
                               //Aquarium a = JsonConvert.DeserializeObject<Aquarium>(data);

                            
                                    Aquarium a = JsonHelper.DeserializePreservingExisting<Aquarium>(data);
                            

                                        
                                    Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        aquariums[a.ID - 1] = JsonHelper.DeserializePreservingExisting(data, aquariums[a.ID - 1]);
                                        if (md.Data != null)
                                        {
                                            if (md.Data[2].Temperature > a.regulTemp.consigne) a.regulTemp.chaudFroid = false;
                                            else a.regulTemp.chaudFroid = true;
                                        }
                                        // Reset the ItemsSource of the DataGrid to trigger UI refresh
                                        AquariumsDataGrid.ItemsSource = null;
                                        AquariumsDataGrid.ItemsSource = aquariums;

                                        debitTotal = 0;
                                        foreach (var aq in aquariums)
                                        {
                                            debitTotal += aq.debit;
                                        }
                                        labelDebittotal.Content = "Débit Total: " + debitTotal.ToString("F2");

                                        var alarm = alarms.FirstOrDefault(x => x.AquaID == t.ID);
                                        checkAlarm(alarm);
                                    }
                                    catch (Exception ex) { }
                                });
                            break;
                        case 4://CALIBRATE SENSOR  ==> irrelevant
                            break;

                        case 6://SEND DATA ==> receive masterdata 
                            md = JsonConvert.DeserializeObject<MasterData>(data);

                            //md.Data[2].Temperature = 19.2;
                            //md = JsonHelper.DeserializePreservingExisting<MasterData>(data,md);
                           //md.Data[2].Temperature = 21;

                            Dispatcher.Invoke(() =>
                            {
                                
                                MasterDataDataGrid.ItemsSource = null;
                                MasterDataDataGrid.ItemsSource = md.Data;
                            });

                            var requestMessage = JsonConvert.SerializeObject(new { cmd = 6, AquaID = t.ID, tempAmbiante = md.Data[2].Temperature, pHAmbiant = md.Data[2].pH, tempChaud = md.Data[0].Temperature, tempFroid = md.Data[1].Temperature });
                            await BroadcastMessageAsync(requestMessage);

                            setAlarms();
                            /*Dispatcher.Invoke(() =>
                            {
                                aquariums[a.ID - 1] = a;
                                // Reset the ItemsSource of the DataGrid to trigger UI refresh
                                AquariumsDataGrid.ItemsSource = null;
                                AquariumsDataGrid.ItemsSource = aquariums;
                            });*/
                            break;
                        case 7://Request from Frontend ==> send all aquarium data to frontend
                            var subset = aquariums.Take(11).ToList();
                            s = JsonConvert.SerializeObject(subset);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 8://Request from Frontend ==> send all flumes data to frontend

                            var subset2 = aquariums.Skip(12).Take(8).ToList();
                            s = JsonConvert.SerializeObject(subset2);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 9://Request from Frontend ==> send masterdata to frontend
                            s = JsonConvert.SerializeObject(md);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 10://Get alarms
                            var r = new
                            {
                                cmd = 10,
                                alarms
                            };
                            s = JsonConvert.SerializeObject(r);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 11://Front end sends alarms settings
                            alarmSettings = JsonHelper.DeserializePreservingExisting<AlarmSettings>(data);
                            SaveToConfig();
                            setAlarms();
                            /*s = JsonConvert.SerializeObject(alarmSettings);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);*/
                            break;
                        case 12://cmd=12 qd une alarme sonne ou qd une alarme est acknowledged
                            Alarme al = JsonHelper.DeserializePreservingExisting<Alarme>(data);
                            var existingAlarme = alarms.FirstOrDefault(A => A.libelle == al.libelle);
                            if (existingAlarme != null && al.acknowledged)
                            {
                                int index = alarms.IndexOf(existingAlarme);
                                alarms[index].acknowledged = false;

                                alarms[index].triggered = false;
                                alarms[index].raised = false;
                                alarms[index].dtAcknowledged = DateTime.Now;
                                Alarme ala = alarms[index];
                                var resp = new
                                {
                                    cmd = 12,
                                    ala
                                };
                                s = JsonConvert.SerializeObject(resp);
                                buffer = Encoding.UTF8.GetBytes(s);
                                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            
                            break;
                        case 13://Front end request alarms settings
                            var re = new
                            {
                                cmd = 13,
                                alarmSettings
                            };
                            s = JsonConvert.SerializeObject(re);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                    }
                }
                catch (Exception e)
                {

                }
            }
            else //"Connected"
            {
                string message = "{\"cmd\":0}";
                buffer = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            
        }


        private async void checkAlarm(Alarme a)
        {
            if (a.checkAndRaise())
            {
                if (a.mustSend)
                {
                    var response = new
                    {
                        cmd = 12,
                        a.AquaID,
                        a.libelle,
                        a.delta,
                        a.dtAcknowledged,
                        a.dtRaised,
                        a.dtTriggered,
                        a.raised,
                        a.acknowledged,
                        a.triggered,
                        a.value,
                        a.threshold,
                        a.variant
                    };

                    var broadcastMessage = JsonConvert.SerializeObject(response);
                    await BroadcastMessageAsync(broadcastMessage);
                    a.mustSend = false;
                    //a.mustSend = true;
                }

            }
            
        }

        public void SaveToConfig()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            config.AppSettings.Settings["AlarmSettings.WaterPressureEnabled"].Value = alarmSettings.waterPressureEnabled.ToString();
            config.AppSettings.Settings["AlarmSettings.WaterPressureValue"].Value = alarmSettings.waterPressureValue.ToString();
            config.AppSettings.Settings["AlarmSettings.FlowRatesEnabled"].Value = alarmSettings.flowRatesEnabled.ToString();
            config.AppSettings.Settings["AlarmSettings.FlowRatesValue1"].Value = alarmSettings.flowRatesValue1.ToString();
            config.AppSettings.Settings["AlarmSettings.FlowRatesValue2"].Value = alarmSettings.flowRatesValue2.ToString();
            config.AppSettings.Settings["AlarmSettings.TemperatureEnabled"].Value = alarmSettings.temperatureEnabled.ToString();
            config.AppSettings.Settings["AlarmSettings.TemperatureValue"].Value = alarmSettings.temperatureValue.ToString();
            config.AppSettings.Settings["AlarmSettings.PHEnabled"].Value = alarmSettings.pHEnabled.ToString();
            config.AppSettings.Settings["AlarmSettings.PHValue"].Value = alarmSettings.pHValue.ToString();
            config.AppSettings.Settings["AlarmSettings.O2Enabled"].Value = alarmSettings.o2Enabled.ToString();
            config.AppSettings.Settings["AlarmSettings.O2Value"].Value = alarmSettings.o2Value.ToString();
            config.AppSettings.Settings["AlarmSettings.SpeedEnabled"].Value = alarmSettings.speedEnabled.ToString();
            config.AppSettings.Settings["AlarmSettings.SpeedValue"].Value = alarmSettings.speedValue.ToString();

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }





        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            _listener.Stop();
            ServerStatusLabel.Content = "Server Stopped";
        }


        private void saveData()
        {
            try
            {
                DateTime dt = DateTime.Now.ToUniversalTime();
                string filePath = ConfigurationManager.AppSettings["dataFileBasePath"].ToString() + "data_" + dt.ToString("yyyy-MM-dd") + ".csv";
                filePath = filePath.Replace('\\', '/');
                saveToFile(filePath, dt);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error writing data: " + e.Message, "Error saving data");
            }

        }


        private async Task writeDataPointAsync(string Tag, string AquaId, string field, double value, DateTime dt)
        {
            string tag;
            var point = PointData
              .Measurement("Flumes")
              .Tag(Tag, AquaId.ToString())
              .Field(field, value)
              .Timestamp(dt.ToUniversalTime(), WritePrecision.S);

            try
            {
                var writeApi = client.GetWriteApiAsync();
                await writeApi.WritePointAsync(point, bucket, org);

            }
            catch (Exception e)
            {

            }

        }


        private void saveToFile(string filePath, DateTime dt)
        {
            if (!System.IO.File.Exists(filePath))
            {
                //Write headers
                String header = "Time;";

                for (int i = 1; i <= 12; i++)
                {
                    header += "Aqua["; header += i; header += "]_debit;";
                    header += "Aqua["; header += i; header += "]_Temperature;";
                    header += "Aqua["; header += i; header += "]_pH;";
                    header += "Aqua["; header += i; header += "]_O2;";
                    header += "Aqua["; header += i; header += "]_consigne_Temp;";
                    header += "Aqua["; header += i; header += "]_sortiePID_Temp;";
                    header += "Aqua["; header += i; header += "]_consigne_pH;";
                    header += "Aqua["; header += i; header += "]_sortiePID_pH;";
                }
                for (int i = 13; i <= 20; i++)
                {
                    header += "Flume["; header += i; header += "]_debit;";
                    header += "Flume["; header += i; header += "]_Temperature;";
                    header += "Flume["; header += i; header += "]_pH;";
                    header += "Flume["; header += i; header += "]_O2;";
                    header += "Flume["; header += i; header += "]_consigne_Temp;";
                    header += "Flume["; header += i; header += "]_sortiePID_Temp;";
                    header += "Flume["; header += i; header += "]_consigne_pH;";
                    header += "Flume["; header += i; header += "]_sortiePID_pH;";
                    header += "Flume["; header += i; header += "]_Vitesse;";
                }
                header += "\n";
                System.IO.File.WriteAllText(filePath, header);
            }

            string data = dt.ToString(); ; data += ";";
           try
            {

                    writeDataPointAsync("Général","Eau Chaude", "pression", md.Data[0].Pression, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "temperature", md.Data[0].Temperature, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "debit", md.Data[0].Debit, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "regulTemp.consigne", md.Data[0].RTemp.consigne, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "regulTemp.sortiePID", md.Data[0].RTemp.sortiePID_pc, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "regulPression.consigne", md.Data[0].RPression.consigne, dt);
                    writeDataPointAsync("Général", "Eau Chaude", "regulPression.sortiePID", md.Data[0].RPression.sortiePID_pc, dt);

                writeDataPointAsync("Général", "Eau Froide", "pression", md.Data[1].Pression, dt);
                writeDataPointAsync("Général", "Eau Froide", "temperature", md.Data[1].Temperature, dt);
                writeDataPointAsync("Général", "Eau Froide", "debit", md.Data[1].Debit, dt);
                writeDataPointAsync("Général", "Eau Froide", "regulTemp.consigne", md.Data[1].RTemp.consigne, dt);
                writeDataPointAsync("Général", "Eau Froide", "regulTemp.sortiePID", md.Data[1].RTemp.sortiePID_pc, dt);
                writeDataPointAsync("Général", "Eau Froide", "regulPression.consigne", md.Data[1].RPression.consigne, dt);
                writeDataPointAsync("Général", "Eau Froide", "regulPression.sortiePID", md.Data[1].RPression.sortiePID_pc, dt);

                writeDataPointAsync("Général", "Eau Ambiante", "pression", md.Data[2].Pression, dt);
                writeDataPointAsync("Général", "Eau Ambiante", "debit", md.Data[2].Debit, dt);
                writeDataPointAsync("Général", "Eau Ambiante", "regulPression.consigne", md.Data[2].RPression.consigne, dt);
                writeDataPointAsync("Général", "Eau Ambiante", "regulPression.sortiePID", md.Data[2].RPression.sortiePID_pc, dt);
                writeDataPointAsync("Général", "Eau Ambiante", "temperature", md.Data[2].Temperature, dt);
                writeDataPointAsync("Général", "Eau Ambiante", "pH", md.Data[2].pH, dt);
            }
            catch(Exception ex) { }

            for (int i = 0; i < 12; i++)
            {
                data += aquariums[i].debit; data += ";";
                data += aquariums[i].temperature; data += ";";
                data += aquariums[i].pH; data += ";";
                data += aquariums[i].oxy; data += ";";
                data += aquariums[i].regulTemp.consigne; data += ";";
                data += aquariums[i].regulTemp.sortiePID_pc; data += ";";
                data += aquariums[i].regulpH.consigne; data += ";";
                data += aquariums[i].regulpH.sortiePID_pc; data += ";";

                writeDataPointAsync("Aquarium", (i + 1).ToString(), "debit", aquariums[i].debit, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "temperature", aquariums[i].temperature, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "pH", aquariums[i].pH, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "O2", aquariums[i].oxy, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "regulTemp.consigne", aquariums[i].regulTemp.consigne, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "regulTemp.sortiePID", aquariums[i].regulTemp.sortiePID_pc, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "regulpH.consigne", aquariums[i].regulpH.consigne, dt);
                writeDataPointAsync("Aquarium", (i + 1).ToString(), "regulpH.sortiePID", aquariums[i].regulpH.sortiePID_pc, dt);

            }

            for (int i = 12; i < 20; i++)
            {
                data += aquariums[i].debit; data += ";";
                data += aquariums[i].temperature; data += ";";
                data += aquariums[i].pH; data += ";";
                data += aquariums[i].oxy; data += ";";
                data += aquariums[i].regulTemp.consigne; data += ";";
                data += aquariums[i].regulTemp.sortiePID_pc; data += ";";
                data += aquariums[i].regulpH.consigne; data += ";";
                data += aquariums[i].regulpH.sortiePID_pc; data += ";";
                data += aquariums[i].vitesse; data += ";";

                writeDataPointAsync("Flume", (i + 13).ToString(), "debit", aquariums[i].debit, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "temperature 1", aquariums[i].temperature, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "pH 1", aquariums[i].pH, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "O2", aquariums[i].oxy, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "regulTemp.consigne", aquariums[i].regulTemp.consigne, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "regulTemp.sortiePID", aquariums[i].regulTemp.sortiePID_pc, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "regulpH.consigne", aquariums[i].regulpH.consigne, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "regulpH.sortiePID", aquariums[i].regulpH.sortiePID_pc, dt);
                writeDataPointAsync("Flume", (i + 13).ToString(), "vitesse", aquariums[i].vitesse, dt);

            }
            data += "\n";
            System.IO.File.AppendAllText(filePath, data);
        }

        private void ftpTransfer(string fileName)
        {
            string ftpUsername = ConfigurationManager.AppSettings["ftpUsername"].ToString();
            string ftpPassword = ConfigurationManager.AppSettings["ftpPassword"].ToString();
            string ftpDir = "ftp://" + ConfigurationManager.AppSettings["ftpDir"].ToString();

            string fn = fileName.Substring(fileName.LastIndexOf('/') + 1);
            ftpDir += fn;
            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
                client.UploadFile(ftpDir, WebRequestMethods.Ftp.UploadFile, fileName);
            }
        }

        private static async Task RunPeriodicAsync(Action onTick,
                                  TimeSpan dueTime,
                                  TimeSpan interval,
                                  CancellationToken token)
        {
            // Initial wait time before we begin the periodic loop.
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);

            // Repeat this loop until cancelled.
            while (!token.IsCancellationRequested)
            {
                // Call our onTick function.
                onTick?.Invoke();

                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
        }
        private async Task InitializeAsync()
        {
            int t;
            Int32.TryParse(ConfigurationManager.AppSettings["dataLogInterval"].ToString(), out t);
            var dueTime = TimeSpan.FromMinutes(t);
            var interval = TimeSpan.FromMinutes(t);

            // TODO: Add a CancellationTokenSource and supply the token here instead of None.
            await RunPeriodicAsync(saveData, dueTime, interval, cts.Token);
        }

    }
}
