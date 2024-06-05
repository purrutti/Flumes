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
    public class Flume
    {
        [JsonProperty("AquaID", Required = Required.Default)]
        public int ID { get; set; }
        [JsonProperty("PLCID", Required = Required.Default)]
        public int PLCID { get; set; }

        [JsonProperty("control", Required = Required.Default)]
        public bool control { get; set; }


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
        public double consigneForcage { get; set; }
        [JsonProperty(Required = Required.Default)]
        public double offset { get; set; }
        [JsonProperty(Required = Required.Default)]
        public bool chaudFroid { get; set; }//true = chaud

    }

    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private ConcurrentDictionary<Guid, WebSocket> _webSockets;

        private ConcurrentDictionary<int, TaskCompletionSource<String>> _responseTasks = new ConcurrentDictionary<int, TaskCompletionSource<String>>();


        public ObservableCollection<Aquarium> aquariums { get; set; }
        public ObservableCollection<Flume> flumes { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        string token = ConfigurationManager.AppSettings["InfluxDBToken"].ToString();
        string bucket = ConfigurationManager.AppSettings["InfluxDBBucket"].ToString();
        string org = ConfigurationManager.AppSettings["InfluxDBOrg"].ToString();

        CancellationTokenSource cts = new CancellationTokenSource();

        InfluxDBClient client;

        MasterData md;

        double debitTotal = 0;

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
                _webSockets = new ConcurrentDictionary<Guid, WebSocket>();

                ServerStatusLabel.Content = "Server Stopped";
                DataContext = this; // Set DataContext to this instance
                aquariums = new ObservableCollection<Aquarium>();
                flumes = new ObservableCollection<Flume>();
                md = new MasterData();

                for (int i = 0; i < 12; i++)
                {
                    Aquarium a = new Aquarium();
                    a.ID = i + 1;
                    a.regulpH = new Regul();
                    a.regulTemp = new Regul();
                    aquariums.Add(a);
                }
                for (int i = 13; i <= 20; i++)
                {
                    Flume f = new Flume();
                    f.ID = i ;
                    f.pH1 = 7.2; f.pH2 = 7.4;
                    f.temperature1 = 21.4;f.temperature2 = 22.4;
                    f.control = false;
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
                }
                AquariumsDataGrid.ItemsSource = aquariums;
                MasterDataDataGrid.ItemsSource = md.Data;
            }
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://192.168.73.14:81/");
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
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
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

                    switch (t.cmd)
                    {
                        case 0://REQ PARAMS ==> send params to aqua
                            if (t.ID == 0)//Master params
                            {
                                if (md.Data != null)
                                {

                                    var response = new
                                    {
                                        cmd = 2,
                                        AquaID = 0,
                                        md,
                                    };

                                    s = JsonConvert.SerializeObject(response);
                                }

                            }
                            else if (t.ID<=12)//Aquarium
                            {
                                if (aquariums[t.ID - 1].ID == 4) aquariums[t.ID - 1].regulTemp.consigne = 16;
                                if (aquariums[t.ID - 1].ID >= 4 && aquariums[t.ID - 1].ID <= 6)
                                {
                                    aquariums[t.ID - 1].regulTemp.Kp = 5;
                                    aquariums[t.ID - 1].regulTemp.Ki = 1;
                                    aquariums[t.ID - 1].regulTemp.Kd = 500;
                                }
                                if(md.Data != null)
                                {

                                    var response = new
                                    {
                                        cmd = 2,
                                        AquaID = aquariums[t.ID - 1].ID,
                                        aquariums[t.ID - 1].PLCID,
                                        aquariums[t.ID - 1].control,
                                        aquariums[t.ID - 1].debit,
                                        aquariums[t.ID - 1].temperature,
                                        aquariums[t.ID - 1].pH,
                                        aquariums[t.ID - 1].oxy,
                                        rTemp = aquariums[t.ID - 1].regulTemp,
                                        rpH = aquariums[t.ID - 1].regulpH,
                                        tempAmbiante = md.Data[2].Temperature,
                                        tempChaud = md.Data[0].Temperature,
                                        tempFroid = md.Data[1].Temperature,
                                    };

                                    s = JsonConvert.SerializeObject(response);
                                }

                            }else if (t.ID <= 20)//Flumes
                            {

                            }
                            

                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 1://REQ DATA ==> irrelevant
                            break;
                        case 2://SEND PARAMS ==> receive params from aqua
                            //TODO: send to frontend;
                            Aquarium b = JsonConvert.DeserializeObject<Aquarium>(data);
                            if (data.Contains("rTemp"))
                            {
                                aquariums[b.ID - 1].regulTemp = b.regulTemp;
                            }
                            if (data.Contains("rpH"))
                            {
                                aquariums[b.ID - 1].regulpH = b.regulpH;
                            }
                            aquariums[b.ID - 1].control = b.control;
                            broadcastMessage = JsonConvert.SerializeObject(aquariums[b.ID - 1]);
                            await BroadcastMessageAsync(broadcastMessage);
                            break;
                        case 3://SEND DATA ==> receive data from aqua
                            Aquarium a = JsonConvert.DeserializeObject<Aquarium>(data);
                            

                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        aquariums[a.ID - 1] = a;
                                        if(md.Data != null)
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
                                    }
                                    catch (Exception ex) { }
                                });
                            


                            break;
                        case 4://CALIBRATE SENSOR  ==> irrelevant
                            break;

                        case 6://SEND DATA ==> receive masterdata 
                            md = JsonConvert.DeserializeObject<MasterData>(data);

                            md.Data[2].Temperature = 19.2;

                            Dispatcher.Invoke(() =>
                            {
                                foreach (var item in md.Data)
                                {
                                    switch (item.ConditionID)
                                    {
                                        case "1":
                                            item.ConditionID = "Eau Chaude";
                                            break;
                                        case "2":
                                            item.ConditionID = "Eau Froide";
                                            break;
                                        case "3":
                                            item.ConditionID = "Eau Ambiante";
                                            break;
                                    }
                                }
                                MasterDataDataGrid.ItemsSource = null;
                                MasterDataDataGrid.ItemsSource = md.Data;
                            });
                            /*Dispatcher.Invoke(() =>
                            {
                                aquariums[a.ID - 1] = a;
                                // Reset the ItemsSource of the DataGrid to trigger UI refresh
                                AquariumsDataGrid.ItemsSource = null;
                                AquariumsDataGrid.ItemsSource = aquariums;
                            });*/
                            break;
                        case 7://Request from Frontend ==> send all aquarium data to frontend
                            s = JsonConvert.SerializeObject(aquariums);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 8://Request from Frontend ==> send all flume data to frontend
                            s = JsonConvert.SerializeObject(flumes);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 9://Request from Frontend ==> send masterdata to frontend
                            s = JsonConvert.SerializeObject(md);
                            buffer = Encoding.UTF8.GetBytes(s);
                            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case 10://Request from Frontend ==> send aquaparams to frontend
                            //{"cmd":10,"AquaID":1 }
                            var requestMessage = JsonConvert.SerializeObject(new { cmd = 0, AquaID = t.ID });
                            await BroadcastMessageAsync(requestMessage);

                            // Wait for the response and forward the message to the frontend
                            String responseMessage = await WaitForResponseAsync(t.ID);
                            if (responseMessage != null)
                            {
                                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(responseMessage)), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            break;
                    }
                }
                catch (Exception e)
                {

                }
            }
            
        }

        private async Task<String> WaitForResponseAsync(int aquaID)
        {
            var tcs = new TaskCompletionSource<String>();
            _responseTasks[aquaID] = tcs;

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // Adjust timeout as needed
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            _responseTasks.TryRemove(aquaID, out _);

            if (completedTask == timeoutTask)
            {
                return null; // Timed out
            }

            return tcs.Task.Result;
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
                    header += "Flume["; header += i; header += "]_Temperature1;";
                    header += "Flume["; header += i; header += "]_pH1;";
                    header += "Flume["; header += i; header += "]_Temperature2;";
                    header += "Flume["; header += i; header += "]_pH2;";
                    header += "Flume["; header += i; header += "]_O2;";
                    header += "Flume["; header += i; header += "]_consigne_Temp;";
                    header += "Flume["; header += i; header += "]_sortiePID_Temp;";
                    header += "Flume["; header += i; header += "]_consigne_pH;";
                    header += "Flume["; header += i; header += "]_sortiePID_pH;";
                    header += "Flume["; header += i; header += "]_Vitesse;";
                    header += "Flume["; header += i; header += "]_Consigne_vitesse;";
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
            }catch(Exception ex) { }

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

            for (int i = 0; i < 8; i++)
            {
                data += flumes[i].debit; data += ";";
                data += flumes[i].temperature1; data += ";";
                data += flumes[i].pH1; data += ";";
                data += flumes[i].temperature2; data += ";";
                data += flumes[i].pH2; data += ";";
                data += flumes[i].oxy; data += ";";
                data += flumes[i].regulTemp.consigne; data += ";";
                data += flumes[i].regulTemp.sortiePID_pc; data += ";";
                data += flumes[i].regulpH.consigne; data += ";";
                data += flumes[i].regulpH.sortiePID_pc; data += ";";
                data += flumes[i].vitesse; data += ";";
                data += flumes[i].consigneVitesse; data += ";";

                writeDataPointAsync("Flume", (i + 1).ToString(), "debit", flumes[i].debit, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "temperature 1", flumes[i].temperature1, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "pH 1", flumes[i].pH1, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "temperature 2", flumes[i].temperature2, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "pH 2", flumes[i].pH2, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "O2", flumes[i].oxy, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "regulTemp.consigne", flumes[i].regulTemp.consigne, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "regulTemp.sortiePID", flumes[i].regulTemp.sortiePID_pc, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "regulpH.consigne", flumes[i].regulpH.consigne, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "regulpH.sortiePID", flumes[i].regulpH.sortiePID_pc, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "vitesse", flumes[i].vitesse, dt);
                writeDataPointAsync("Flume", (i + 1).ToString(), "consigne vitesse", flumes[i].consigneVitesse, dt);

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
