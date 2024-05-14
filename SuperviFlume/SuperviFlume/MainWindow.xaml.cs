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

namespace WebSocketServerExample
{
    public class TrameJson
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int cmd { get; set; }
        [JsonProperty("AquaID", Required = Required.Default)]
        public int ID { get; set; }
        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }
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
    }

    public class DataItem
    {
        [JsonProperty("CondID", Required = Required.Default)]
        public int ConditionID { get; set; }

        [JsonProperty("temperature", Required = Required.Default)]
        public double Temperature { get; set; }

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

        [JsonProperty("control", Required = Required.Default)]
        public bool control { get; set; }


        [JsonProperty("debit", Required = Required.Default)]
        public double debit { get; set; }
        [JsonProperty("temp", Required = Required.Default)]
        public double temperature { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }
        [JsonProperty("oxy", Required = Required.Default)]
        public double oxy { get; set; }
        [JsonProperty("rTemp", Required = Required.Default)]
        public Regul regulTemp { get; set; }
        [JsonProperty("rpH", Required = Required.Default)]
        public Regul regulpH { get; set; }
        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }

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
        public int consigneForcage { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double offset { get; set; }

    }

    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private ConcurrentDictionary<Guid, WebSocket> _webSockets;

        public ObservableCollection<Aquarium> aquariums { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        string token = ConfigurationManager.AppSettings["InfluxDBToken"].ToString();
        string bucket = ConfigurationManager.AppSettings["InfluxDBBucket"].ToString();
        string org = ConfigurationManager.AppSettings["InfluxDBOrg"].ToString();

        CancellationTokenSource cts = new CancellationTokenSource();

        InfluxDBClient client;

        MasterData md;

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

                client = InfluxDBClientFactory.Create("http://localhost:8086", token.ToCharArray());

                ServerStatusLabel.Content = "Server Stopped";
                DataContext = this; // Set DataContext to this instance
                aquariums = new ObservableCollection<Aquarium>();
                md = new MasterData();

                for (int i = 0; i < 20; i++)
                {
                    Aquarium a = new Aquarium();
                    a.ID = i + 1;
                    a.regulpH = new Regul();
                    a.regulTemp = new Regul();
                    aquariums.Add(a);
                }
                AquariumsDataGrid.ItemsSource = aquariums;
            }
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://172.16.36.190:81/");
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

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {


            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            var webSocket = webSocketContext.WebSocket;

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
        }




        public async Task ReadData(string data, WebSocket ws)
        {
            try
            {
                TrameJson t = JsonConvert.DeserializeObject<TrameJson>(data);

                switch (t.cmd)
                {
                    case 0://REQ PARAMS ==> send params to aqua
                        String s = JsonConvert.SerializeObject(aquariums[t.ID - 1]);
                        var buffer = Encoding.UTF8.GetBytes(s);
                        await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;
                    case 1://REQ DATA ==> irrelevant
                        break;
                    case 2://SEND PARAMS ==> receive params from aqua
                    case 3://SEND DATA ==> receive data from aqua
                        Aquarium a = JsonConvert.DeserializeObject<Aquarium>(data);
                        Dispatcher.Invoke(() =>
                        {
                            aquariums[a.ID - 1] = a;
                            // Reset the ItemsSource of the DataGrid to trigger UI refresh
                            AquariumsDataGrid.ItemsSource = null;
                            AquariumsDataGrid.ItemsSource = aquariums;
                        });
                        break;
                    case 4://CALIBRATE SENSOR  ==> irrelevant
                        break;
                    
                    case 6://SEND DATA ==> receive data from aqua
                        MasterData md = JsonConvert.DeserializeObject<MasterData>(data);
                        /*Dispatcher.Invoke(() =>
                        {
                            aquariums[a.ID - 1] = a;
                            // Reset the ItemsSource of the DataGrid to trigger UI refresh
                            AquariumsDataGrid.ItemsSource = null;
                            AquariumsDataGrid.ItemsSource = aquariums;
                        });*/
                        break;
                    case 7://Request from Frontend ==> send all data to frontend
                        String s2 = JsonConvert.SerializeObject(aquariums);
                        var buffer2 = Encoding.UTF8.GetBytes(s2);
                        await ws.SendAsync(new ArraySegment<byte>(buffer2), WebSocketMessageType.Text, true, CancellationToken.None);
                        break;
                }
            }
            catch (Exception e)
            {

            }
        }


        private void AppendToTextBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageTextBox.AppendText(message + Environment.NewLine);
                MessageTextBox.ScrollToEnd();
            });
        }


        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            _listener.Stop();
            ServerStatusLabel.Content = "Server Stopped";
        }
        private void RefreshDataGrid()
        {
            // Reset the ItemsSource of the DataGrid to trigger UI refresh
            AquariumsDataGrid.ItemsSource = null;
            AquariumsDataGrid.ItemsSource = aquariums;
        }


        private void saveData()
        {
            try
            {
                DateTime dt = DateTime.Now.ToUniversalTime();
                string filePath = ConfigurationManager.AppSettings["dataFileBasePath"].ToString() + "_" + dt.ToString("yyyy-MM-dd") + ".csv";
                filePath = filePath.Replace('\\', '/');
                saveToFile(filePath, dt);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error writing data: " + e.Message, "Error saving data");
            }

        }


        private async Task writeDataPointAsync(int AquaId, string field, double value, DateTime dt)
        {
            string tag;
            var point = PointData
              .Measurement("Flumes")
              .Tag("Aquarium", AquaId.ToString())
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
                    header += "Aqua["; header += i; header += "]_02;";
                    header += "Aqua["; header += i; header += "]_consigne_Temp;";
                    header += "Aqua["; header += i; header += "]_sortiePID_Temp;";
                    header += "Aqua["; header += i; header += "]_consigne_pH;";
                    header += "Aqua["; header += i; header += "]_sortiePID_pH;";
                }
                header += "\n";
                System.IO.File.WriteAllText(filePath, header);
            }

            string data = dt.ToString(); ; data += ";";

            for(int i = 0; i < 2; i++)
            {

                writeDataPointAsync(md.Data[i].ConditionID, "pression", md.Data[i].Pression, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "temperature", md.Data[i].Temperature, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "debit", md.Data[i].Debit, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "regulTemp.consigne", md.Data[i].RTemp.consigne, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "regulTemp.sortiePID", md.Data[i].RTemp.sortiePID_pc, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "regulPression.consigne", md.Data[i].RPression.consigne, dt);
                writeDataPointAsync(md.Data[i].ConditionID, "regulPression.sortiePID", md.Data[i].RPression.sortiePID_pc, dt);
            }

            writeDataPointAsync(md.Data[2].ConditionID, "pression", md.Data[2].Pression, dt);
            writeDataPointAsync(md.Data[2].ConditionID, "debit", md.Data[2].Debit, dt);
            writeDataPointAsync(md.Data[2].ConditionID, "regulPression.consigne", md.Data[2].RPression.consigne, dt);
            writeDataPointAsync(md.Data[2].ConditionID, "regulPression.sortiePID", md.Data[2].RPression.sortiePID_pc, dt);

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

                writeDataPointAsync(i + 1, "debit", aquariums[i].debit, dt);
                writeDataPointAsync(i + 1, "temperature", aquariums[i].temperature, dt);
                writeDataPointAsync(i + 1, "pH", aquariums[i].pH, dt);
                writeDataPointAsync(i + 1, "02", aquariums[i].oxy, dt);
                writeDataPointAsync(i + 1, "regulTemp.consigne", aquariums[i].regulTemp.consigne, dt);
                writeDataPointAsync(i + 1, "regulTemp.sortiePID", aquariums[i].regulTemp.sortiePID_pc, dt);
                writeDataPointAsync(i + 1, "regulpH.consigne", aquariums[i].regulpH.consigne, dt);
                writeDataPointAsync(i + 1, "regulpH.sortiePID", aquariums[i].regulpH.sortiePID_pc, dt);

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
