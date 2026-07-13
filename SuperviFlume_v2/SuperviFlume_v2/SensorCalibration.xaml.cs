using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SuperviFlume_v2
{
    public partial class SensorCalibration : Window
    {
        private readonly WebSocketServer     _server;
        private          CancellationTokenSource _cts;

        // ── Tâche périodique ─────────────────────────────────────────────────────
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

        private async Task InitializeAsync()
        {
            await RunPeriodicAsync(RefreshMeasure, TimeSpan.Zero, TimeSpan.FromSeconds(1), _cts.Token);
        }

        // ── Constructeur ─────────────────────────────────────────────────────────
        public SensorCalibration(WebSocketServer server)
        {
            InitializeComponent();
            _server = server;

            // Remplir la ComboBox : 12 aquariums + 8 flumes
            for (int i = 1; i <= 12; i++)
                cbDeviceNumber.Items.Add($"Aquarium {i}");
            for (int i = 1; i <= 8; i++)
                cbDeviceNumber.Items.Add($"Flume {i}");

            cbDeviceNumber.SelectedIndex = 0;

            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                    StartRefresh();
            };
        }

        private void StartRefresh()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            InitializeAsync();
        }

        // ── Résolution PLCID / sensorID (même mapping que SuperviFlume v1) ──────
        //   deviceID  : 1-20  (1-12 = aquariums, 13-20 = flumes)
        //   sensorType: "O2" ou "pH"
        private static (int PLCID, int sensorID) GetIds(int deviceID, string sensorType)
        {
            int PLCID    = 0;
            int sensorID = 0;

            if (sensorType == "O2")
            {
                if      (deviceID <  4) { PLCID = 1; sensorID = deviceID + 9; }
                else if (deviceID <  7) { PLCID = 2; sensorID = deviceID + 9 - 3; }
                else if (deviceID < 10) { PLCID = 3; sensorID = deviceID + 9 - 6; }
                else if (deviceID < 13) { PLCID = 4; sensorID = deviceID + 9 - 9; }
                else if (deviceID < 17) { PLCID = 6; sensorID = deviceID - 3; }
                else if (deviceID < 21) { PLCID = 7; sensorID = deviceID - 7; }
            }
            else // pH
            {
                if      (deviceID <  4) { PLCID = 1; sensorID = deviceID; }
                else if (deviceID <  7) { PLCID = 2; sensorID = deviceID - 3; }
                else if (deviceID < 10) { PLCID = 3; sensorID = deviceID - 6; }
                else if (deviceID < 13) { PLCID = 4; sensorID = deviceID - 9; }
                else if (deviceID < 17) { PLCID = 6; sensorID = deviceID - 12; }
                else if (deviceID < 21) { PLCID = 7; sensorID = deviceID - 16; }
            }

            return (PLCID, sensorID);
        }

        // ── Rafraîchissement des valeurs affichées ────────────────────────────────
        private void RefreshMeasure()
        {
            Dispatcher.Invoke(() =>
            {
                int idx = cbDeviceNumber.SelectedIndex;
                if (idx < 0 || idx >= _server.Aquariums.Count) return;

                var a = _server.Aquariums[idx];
                labelO2SensorValue.Content = $"O2 : {a.oxy:F2} %";
                labelpHSensorValue.Content = $"pH : {a.pH:F2}";
            });
        }

        // ── Boutons O2 ────────────────────────────────────────────────────────────
        private void btnSetOffset_Click(object sender, RoutedEventArgs e)
        {
            int deviceID = cbDeviceNumber.SelectedIndex + 1;
            var (PLCID, sensorID) = GetIds(deviceID, "O2");
            SendReq(PLCID, deviceID, sensorID, calibParam: 0, value: 0.0);
        }

        private void btnSetSlope_Click(object sender, RoutedEventArgs e)
        {
            int deviceID = cbDeviceNumber.SelectedIndex + 1;
            var (PLCID, sensorID) = GetIds(deviceID, "O2");
            SendReq(PLCID, deviceID, sensorID, calibParam: 1, value: 100.0);
        }

        // ── Bouton pH ─────────────────────────────────────────────────────────────
        private void btnCalibratepH_Click(object sender, RoutedEventArgs e)
        {
            int deviceID = cbDeviceNumber.SelectedIndex + 1;
            var (PLCID, sensorID) = GetIds(deviceID, "pH");

            double value = 7.0;
            var str = tbpHCalibValue.Text.Replace('.', ',');
            double.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out value);

            SendReq(PLCID, deviceID, sensorID, calibParam: 1, value: value);
        }

        // ── Envoi de la trame de calibration vers les automates ──────────────────
        //   Format : {"cmd":4,"PLCID":X,"AquaID":Y,"sensorID":Z,"calibParam":P,"value":V}
        private void SendReq(int PLCID, int deviceID, int sensorID, int calibParam, double value)
        {
            var culture = CultureInfo.InvariantCulture;
            string msg = $"{{\"cmd\":4,\"PLCID\":{PLCID},\"AquaID\":{deviceID},\"sensorID\":{sensorID},\"calibParam\":{calibParam},\"value\":{value.ToString("F2", culture)}}}";
            _ = _server.BroadcastMessageAsync(msg);
        }

        // ── Évènements UI ─────────────────────────────────────────────────────────
        private void cbDeviceNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshMeasure();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts?.Cancel();
            this.Hide();
            e.Cancel = true;
        }
    }
}
