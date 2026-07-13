using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using InfluxDB.Client;
using InfluxDB.Client.Writes;
using InfluxDB.Client.Api.Domain;

namespace SuperviFlume_v2
{
    /// <summary>
    /// Serveur WebSocket basé sur HttpListener.
    /// Reçoit les trames JSON des automates (aquariums + MasterFlume)
    /// et émet des événements pour mettre à jour l'IHM.
    /// </summary>
    public class WebSocketServer
    {
        // ── État interne ─────────────────────────────────────────────────────────
        private HttpListener             _listener;
        private CancellationTokenSource  _acceptCts;
        private CancellationTokenSource  _saveCts;
        private CancellationTokenSource  _esp32Cts;
        private InfluxDBClient           _influxClient;
        private string                   _influxBucket;
        private string                   _influxOrg;

        // Clients WebSocket connectés (pour le broadcast)
        private readonly ConcurrentDictionary<WebSocket, byte> _clients = new ConcurrentDictionary<WebSocket, byte>();

        // Collection partagée des aquariums (20 slots, index = ID-1)
        public ObservableCollection<Aquarium> Aquariums { get; } = new ObservableCollection<Aquarium>();

        // Dernières données reçues du MasterFlume
        public MasterData LastMasterData { get; private set; } = new MasterData();

        // ── Événements vers l'IHM ────────────────────────────────────────────────
        /// <summary>Déclenché quand un aquarium reçoit de nouvelles données (cmd:3 SEND_DATA).</summary>
        public event Action<Aquarium>   AquariumReceived;

        /// <summary>Déclenché quand un aquarium envoie ses paramètres (cmd:2 SEND_PARAMS).</summary>
        public event Action<Aquarium>   AquariumParamsReceived;

        /// <summary>Déclenché quand le MasterFlume envoie de nouvelles données.</summary>
        public event Action<MasterData> MasterDataReceived;

        /// <summary>Message de log à afficher dans l'IHM.</summary>
        public event Action<string>     Log;

        // ── Constructeur ─────────────────────────────────────────────────────────
        public WebSocketServer()
        {
            for (int i = 0; i < 20; i++)
                Aquariums.Add(new Aquarium { ID = i + 1, regulTemp = new Regul(), regulpH = new Regul() });
        }

        // ── Démarrage / Arrêt ────────────────────────────────────────────────────
        public void Start(string url)
        {
            _acceptCts = new CancellationTokenSource();
            _saveCts   = new CancellationTokenSource();
            _esp32Cts  = new CancellationTokenSource();

            // ── InfluxDB ─────────────────────────────────────────────────────────
            string influxUrl   = ConfigurationManager.AppSettings["InfluxDBUrl"]    ?? "http://localhost:8086";
            string influxToken = ConfigurationManager.AppSettings["InfluxDBToken"]  ?? "";
            _influxBucket      = ConfigurationManager.AppSettings["InfluxDBBucket"] ?? "Flumes";
            _influxOrg         = ConfigurationManager.AppSettings["InfluxDBOrg"]    ?? "LOV";
            _influxClient      = InfluxDBClientFactory.Create(influxUrl, influxToken.ToCharArray());

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://+:81/");
            _listener.Start();

            _ = AcceptLoopAsync();
            _ = Esp32ClientLoopAsync(_esp32Cts.Token);

            int intervalMin;
            Int32.TryParse(ConfigurationManager.AppSettings["dataLogInterval"] ?? "5", out intervalMin);
            var interval = TimeSpan.FromMinutes(intervalMin);
            _ = RunPeriodicAsync(SaveData, interval, interval, _saveCts.Token);
        }

        public void Stop()
        {
            _acceptCts?.Cancel();
            _saveCts?.Cancel();
            _esp32Cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _influxClient?.Dispose(); } catch { }
        }

        // ── Boucle d'acceptation des connexions ──────────────────────────────────
        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_acceptCts.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                        _ = Task.Run(() => HandleWebSocketAsync(context));
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
            }
            catch (Exception) { /* arrêt normal */ }
        }

        // ── Gestion d'une connexion WebSocket ────────────────────────────────────
        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            var ws        = wsContext.WebSocket;
            var buffer    = new byte[4096];

            _clients.TryAdd(ws, 0);
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Log?.Invoke($"Reçu : {message}");
                        await ProcessMessageAsync(message, ws);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                Log?.Invoke($"Erreur WebSocket : {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(ws, out _);
            }
        }

        // ── Envoi d'un message à tous les clients connectés ──────────────────────
        public async Task BroadcastMessageAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            foreach (var ws in _clients.Keys)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                }
            }
        }

        // ── Dispatch des trames JSON ─────────────────────────────────────────────
        // Protocole :
        //   cmd 0  REQ_PARAMS  → l'automate demande les consignes de son aquarium
        //   cmd 1  REQ_DATA    → ignoré
        //   cmd 2  SEND_PARAMS → l'automate envoie ses paramètres mis à jour
        //   cmd 3  SEND_DATA   → l'automate envoie ses mesures
        //   cmd 4  CALIBRATE   → ignoré
        //   cmd 6  MASTER_DATA → le MasterFlume envoie les données circuit général
        //   cmd 7  FRONTEND    → une IHM web demande toutes les données aquariums
        private async Task ProcessMessageAsync(string data, WebSocket ws)
        {
            try
            {
                var frame = JsonConvert.DeserializeObject<TrameJson>(data);

                switch (frame.cmd)
                {
                    case 0: // REQ_PARAMS — envoyer les consignes à l'automate
                        if (frame.ID >= 1 && frame.ID <= Aquariums.Count)
                        {
                            string payload = JsonConvert.SerializeObject(Aquariums[frame.ID - 1]);
                            await SendTextAsync(ws, payload);
                        }
                        break;

                    case 2: // SEND_PARAMS — paramètres PID / consignes
                        if (frame.ID == 0)
                        {
                            // AquaID = 0 → réponse master params
                            MergeMasterData(data);
                            MasterDataReceived?.Invoke(LastMasterData);
                        }
                        else if (frame.ID >= 1 && frame.ID <= Aquariums.Count)
                        {
                            // PopulateObject met à jour uniquement les champs présents dans le JSON —
                            // les mesures (temp/pH/debit/oxy) ne sont pas envoyées dans SEND_PARAMS
                            // et doivent donc être préservées plutôt qu'écrasées à 0.
                            var target = Aquariums[frame.ID - 1];
                            JsonConvert.PopulateObject(data, target);
                            target.lastUpdated = DateTime.Now;
                            AquariumParamsReceived?.Invoke(target);
                        }
                        break;

                    case 3: // SEND_DATA — données de mesure d'un aquarium
                        if (frame.ID >= 1 && frame.ID <= Aquariums.Count)
                        {
                            // PopulateObject met à jour uniquement les champs présents
                            // dans le JSON — regulTemp et regulpH sont préservés
                            var target = Aquariums[frame.ID - 1];
                            JsonConvert.PopulateObject(data, target);
                            target.lastUpdated = DateTime.Now;
                            AquariumReceived?.Invoke(target);
                        }
                        break;

                    case 6: // MASTER_DATA — circuit général (froid / chaud / ambiant)
                        MergeMasterData(data);
                        MasterDataReceived?.Invoke(LastMasterData);
                        await BroadcastMessageAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{{\"cmd\":6,\"tempAmbiante\":{0},\"tempChaud\":{1},\"tempFroid\":{2},\"pHAmbiant\":{3}}}",
                            LastMasterData.Data[2].Temperature, LastMasterData.Data[0].Temperature,
                            LastMasterData.Data[1].Temperature, LastMasterData.Data[2].PH));
                        break;

                    case 7: // REQ_TEMPS — IHM demande les températures de tous les bacs
                        var sb = new System.Text.StringBuilder("{\"cmd\":8");
                        var ic = System.Globalization.CultureInfo.InvariantCulture;
                        for (int i = 0; i < Aquariums.Count; i++)
                            sb.Append(string.Format(ic, ",\"tank{0}\":{1}", i + 1, Aquariums[i].temperature));
                        sb.Append('}');
                        await SendTextAsync(ws, sb.ToString());
                        break;


                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Erreur traitement trame : {ex.Message}");
            }
        }

        // ── Fusion MasterData (préserve les champs absents du JSON) ─────────────
        private void MergeMasterData(string json)
        {
            if (LastMasterData == null) LastMasterData = new MasterData();
            var existing = JObject.FromObject(LastMasterData);
            existing.Merge(JObject.Parse(json), new JsonMergeSettings
            {
                MergeArrayHandling  = MergeArrayHandling.Merge,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });
            LastMasterData = existing.ToObject<MasterData>();
        }

        // ── Envoi d'un message texte ─────────────────────────────────────────────
        private static async Task SendTextAsync(WebSocket ws, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        // ── Sauvegarde périodique CSV + InfluxDB ─────────────────────────────────
        private void SaveData()
        {
            try
            {
                var    dt       = DateTime.Now.ToUniversalTime();
                string basePath = ConfigurationManager.AppSettings["dataFileBasePath"] ?? "data\\flumes";
                string filePath = (basePath + "_" + dt.ToString("yyyy-MM-dd") + ".csv").Replace('\\', '/');
                WriteToFile(filePath, dt);
                Log?.Invoke($"Données sauvegardées : {filePath}");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Erreur sauvegarde : {ex.Message}");
            }
        }

        private async Task WriteDataPointAsync(string tag, string field, double value, DateTime dt)
        {
            var point = PointData
                .Measurement("Flumes")
                .Tag("Aquarium", tag)
                .Field(field, value)
                .Timestamp(dt.ToUniversalTime(), WritePrecision.S);
            try
            {
                var writeApi = _influxClient.GetWriteApiAsync();
                await writeApi.WritePointAsync(point, _influxBucket, _influxOrg);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Erreur InfluxDB : {ex.Message}");
            }
        }

        private void WriteToFile(string filePath, DateTime dt)
        {

            string tag = "Eau chaude";
            // ── En-têtes CSV (20 appareils : 12 aquariums + 8 flumes) ─────────────
            if (!System.IO.File.Exists(filePath))
            {
                var header = new System.Text.StringBuilder("Time;");
                // MasterData : Eau chaude (7 champs)
                header.Append("Master_EauChaude_pression;Master_EauChaude_temperature;Master_EauChaude_debit;");
                header.Append("Master_EauChaude_consigne_Temp;Master_EauChaude_sortiePID_Temp;");
                header.Append("Master_EauChaude_consigne_Pression;Master_EauChaude_sortiePID_Pression;");
                // MasterData : Eau froide (7 champs)
                header.Append("Master_EauFroide_pression;Master_EauFroide_temperature;Master_EauFroide_debit;");
                header.Append("Master_EauFroide_consigne_Temp;Master_EauFroide_sortiePID_Temp;");
                header.Append("Master_EauFroide_consigne_Pression;Master_EauFroide_sortiePID_Pression;");
                // MasterData : Eau ambiante (6 champs)
                header.Append("Master_EauAmbiante_pression;Master_EauAmbiante_temperature;Master_EauAmbiante_pH;Master_EauAmbiante_debit;");
                header.Append("Master_EauAmbiante_consigne_Pression;Master_EauAmbiante_sortiePID_Pression;");
                for (int i = 1; i <= 12; i++)
                {
                    header.Append($"Aqua[{i}]_debit;Aqua[{i}]_Temperature;Aqua[{i}]_pH;Aqua[{i}]_O2;");
                    header.Append($"Aqua[{i}]_consigne_Temp;Aqua[{i}]_sortiePID_Temp;");
                    header.Append($"Aqua[{i}]_consigne_pH;Aqua[{i}]_sortiePID_pH;");
                }
                for (int i = 1; i <= 8; i++)
                {
                    header.Append($"Flume[{i}]_debit;Flume[{i}]_Temperature;Flume[{i}]_pH;Flume[{i}]_O2;Flume[{i}]_vitesse;");
                    header.Append($"Flume[{i}]_consigne_Temp;Flume[{i}]_sortiePID_Temp;");
                    header.Append($"Flume[{i}]_consigne_pH;Flume[{i}]_sortiePID_pH;");
                }
                header.Append("\n");
                System.IO.File.WriteAllText(filePath, header.ToString());
            }

            // ── InfluxDB : MasterData ─────────────────────────────────────────────
            if (LastMasterData?.Data != null && LastMasterData.Data.Count >= 3)
            {
                for (int i = 0; i < 2; i++)
                {
                    tag = "Eau chaude";
                    if (i == 1) { tag = "Eau Froide"; }
                    var d = LastMasterData.Data[i];
                    WriteDataPointAsync(tag, "pression",               d.Pression,           dt);
                    WriteDataPointAsync(tag, "temperature",            d.Temperature,        dt);
                    WriteDataPointAsync(tag, "debit",                  d.Debit,              dt);
                    WriteDataPointAsync(tag, "regulTemp.consigne",     d.RTemp.consigne,     dt);
                    WriteDataPointAsync(tag, "regulTemp.sortiePID",    d.RTemp.sortiePID_pc, dt);
                    WriteDataPointAsync(tag, "regulPression.consigne", d.RPression.consigne,     dt);
                    WriteDataPointAsync(tag, "regulPression.sortiePID",d.RPression.sortiePID_pc, dt);
                }

                tag = "Eau ambiante";
                var amb = LastMasterData.Data[2];
                WriteDataPointAsync(tag, "pression",                amb.Pression,              dt);
                WriteDataPointAsync(tag, "temperature",             amb.Temperature,           dt);
                WriteDataPointAsync(tag, "pH",                      amb.PH,                    dt);
                WriteDataPointAsync(tag, "debit",                   amb.Debit,                 dt);
                WriteDataPointAsync(tag, "regulPression.consigne",  amb.RPression.consigne,    dt);
                WriteDataPointAsync(tag, "regulPression.sortiePID", amb.RPression.sortiePID_pc,dt);
            }

            // ── CSV + InfluxDB : aquariums 1-12 ──────────────────────────────────
            var line = new System.Text.StringBuilder(dt.ToString("yyyy-MM-dd HH:mm:ss") + ";");

            // ── CSV : MasterData (Eau chaude + Eau froide : 7 champs chacune, Eau ambiante : 6 champs) ──
            if (LastMasterData?.Data != null && LastMasterData.Data.Count >= 3)
            {
                for (int i = 0; i < 2; i++)
                {
                    var d = LastMasterData.Data[i];
                    line.Append($"{d.Pression};{d.Temperature};{d.Debit};");
                    line.Append($"{d.RTemp?.consigne};{d.RTemp?.sortiePID_pc};");
                    line.Append($"{d.RPression?.consigne};{d.RPression?.sortiePID_pc};");
                }
                var amb = LastMasterData.Data[2];
                line.Append($"{amb.Pression};{amb.Temperature};{amb.PH};{amb.Debit};");
                line.Append($"{amb.RPression?.consigne};{amb.RPression?.sortiePID_pc};");
            }
            else
            {
                line.Append(";;;;;;;");  // Eau chaude : 7 champs vides
                line.Append(";;;;;;;");  // Eau froide : 7 champs vides
                line.Append(";;;;;;");   // Eau ambiante : 6 champs vides
            }

            for (int i = 0; i < 12; i++)
            {
                var a = Aquariums[i];
                tag = (a.ID).ToString();
                line.Append($"{a.debit};{a.temperature};{a.pH};{a.oxy};");
                line.Append($"{a.regulTemp.consigne};{a.regulTemp.sortiePID_pc};");
                line.Append($"{a.regulpH.consigne};{a.regulpH.sortiePID_pc};");

                WriteDataPointAsync(tag, "debit",              a.debit,                  dt);
                WriteDataPointAsync(tag, "temperature",        a.temperature,            dt);
                WriteDataPointAsync(tag, "pH",                 a.pH,                     dt);
                WriteDataPointAsync(tag, "O2",                 a.oxy,                    dt);
                WriteDataPointAsync(tag, "regulTemp.consigne", a.regulTemp.consigne,     dt);
                WriteDataPointAsync(tag, "regulTemp.sortiePID",a.regulTemp.sortiePID_pc, dt);
                WriteDataPointAsync(tag, "regulpH.consigne",   a.regulpH.consigne,       dt);
                WriteDataPointAsync(tag, "regulpH.sortiePID",  a.regulpH.sortiePID_pc,   dt);
            }

            // ── CSV + InfluxDB : flumes 13-20 (index 12-19) ──────────────────────
            for (int i = 12; i < 20; i++)
            {
                var a      = Aquariums[i];

                tag = (a.ID).ToString();
                int flumeN = i - 11; // 1-8
                line.Append($"{a.debit};{a.temperature};{a.pH};{a.oxy};{a.debitCircul};");
                line.Append($"{a.regulTemp.consigne};{a.regulTemp.sortiePID_pc};");
                line.Append($"{a.regulpH.consigne};{a.regulpH.sortiePID_pc};");

                WriteDataPointAsync(tag, "debit",              a.debit,                  dt);
                WriteDataPointAsync(tag, "temperature",        a.temperature,            dt);
                WriteDataPointAsync(tag, "pH",                 a.pH,                     dt);
                WriteDataPointAsync(tag, "O2",                 a.oxy,                    dt);
                WriteDataPointAsync(tag, "vitesse",            a.debitCircul,            dt);
                WriteDataPointAsync(tag, "regulTemp.consigne", a.regulTemp.consigne,     dt);
                WriteDataPointAsync(tag, "regulTemp.sortiePID",a.regulTemp.sortiePID_pc, dt);
                WriteDataPointAsync(tag, "regulpH.consigne",   a.regulpH.consigne,       dt);
                WriteDataPointAsync(tag, "regulpH.sortiePID",  a.regulpH.sortiePID_pc,   dt);
            }

            line.Append("\n");
            System.IO.File.AppendAllText(filePath, line.ToString());
        }

        // ── Client WebSocket vers l'ESP32 en mode AP (192.168.4.1) ──────────────
        // Envoie les 20 températures toutes les 5 s ; reconnecte automatiquement.
        private async Task Esp32ClientLoopAsync(CancellationToken ct)
        {
            const string uri = "ws://192.168.4.1:80/ws";
            while (!ct.IsCancellationRequested)
            {
                using (var ws = new System.Net.WebSockets.ClientWebSocket())
                {
                    try
                    {
                        await ws.ConnectAsync(new Uri(uri), ct);
                        Log?.Invoke("ESP32 WiFi : connecté");

                        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                        {
                            var sb = new System.Text.StringBuilder("{\"cmd\":8");
                            var ic = System.Globalization.CultureInfo.InvariantCulture;
                            for (int i = 0; i < Aquariums.Count; i++)
                                sb.Append(string.Format(ic, ",\"tank{0}\":{1:F2}", i + 1, Aquariums[i].temperature));
                            sb.Append('}');

                            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                            
                            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

                            await Task.Delay(5000, ct);
                        }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"ESP32 WiFi : {ex.Message}, reconnexion dans 5 s");
                    }
                }

                try { await Task.Delay(5000, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        // ── Utilitaire : tâche périodique ────────────────────────────────────────
        private static async Task RunPeriodicAsync(Action onTick, TimeSpan dueTime, TimeSpan interval, CancellationToken token)
        {
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);

            while (!token.IsCancellationRequested)
            {
                onTick?.Invoke();
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
        }
    }
}
