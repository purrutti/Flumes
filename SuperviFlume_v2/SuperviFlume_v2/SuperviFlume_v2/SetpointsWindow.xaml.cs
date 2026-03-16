using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace SuperviFlume_v2
{
    public partial class SetpointsWindow : Window
    {
        private readonly WebSocketServer _server;

        // ── Préférences locales par appareil (index 0-19) — persistantes ────────
        private bool[]   _useOffsetTemp = new bool[20];
        private bool[]   _useOffsetPH   = new bool[20];
        private double[] _offsetTemp     = new double[20];
        private double[] _offsetPH       = new double[20];

        private static readonly string PrefsFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "setpoints_prefs.json");

        private class OffsetPrefs
        {
            public bool[]   UseOffsetTemp { get; set; } = new bool[20];
            public bool[]   UseOffsetPH   { get; set; } = new bool[20];
            public double[] OffsetTemp    { get; set; } = new double[20];
            public double[] OffsetPH      { get; set; } = new double[20];
        }

        private void LoadPrefs()
        {
            try
            {
                if (!File.Exists(PrefsFile)) return;
                var p = JsonConvert.DeserializeObject<OffsetPrefs>(File.ReadAllText(PrefsFile));
                if (p == null) return;
                _useOffsetTemp = p.UseOffsetTemp ?? new bool[20];
                _useOffsetPH   = p.UseOffsetPH   ?? new bool[20];
                _offsetTemp    = p.OffsetTemp    ?? new double[20];
                _offsetPH      = p.OffsetPH      ?? new double[20];
            }
            catch { /* fichier corrompu → on repart à zéro */ }
        }

        private void SavePrefs()
        {
            try
            {
                var p = new OffsetPrefs
                {
                    UseOffsetTemp = _useOffsetTemp,
                    UseOffsetPH   = _useOffsetPH,
                    OffsetTemp    = _offsetTemp,
                    OffsetPH      = _offsetPH,
                };
                File.WriteAllText(PrefsFile, JsonConvert.SerializeObject(p, Formatting.Indented));
            }
            catch { }
        }

        // ── Timeout "Loading" ────────────────────────────────────────────────────
        private CancellationTokenSource _loadingCts;

        // ── Constructeur ─────────────────────────────────────────────────────────
        public SetpointsWindow(WebSocketServer server)
        {
            InitializeComponent();
            _server = server;

            for (int i = 1; i <= 12; i++)
                cbDevice.Items.Add($"Aquarium {i}");
            for (int i = 1; i <= 8; i++)
                cbDevice.Items.Add($"Flume {i}");

            LoadPrefs();

            cbDevice.SelectedIndex = 0;

            _server.AquariumParamsReceived += OnAquariumReceived;

            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    LoadCurrentValues();
                    RequestParamsFromPLC();
                }
            };
        }

        // ── Réception cmd:2 du PLC ───────────────────────────────────────────────
        private void OnAquariumReceived(Aquarium a)
        {
            Dispatcher.InvokeAsync(() =>
            {
                int idx = a.ID - 1;
                if (idx < 0 || idx >= 20) return;

                // Synchroniser les offsets depuis données PLC fraîches
                /*if (a.regulTemp != null) _offsetTemp[idx] = a.regulTemp.offset;
                if (a.regulpH   != null) _offsetPH[idx]   = a.regulpH.offset;
                */

                if (!IsVisible) return;
                if (a.ID != cbDevice.SelectedIndex + 1) return;

                // Annuler le timeout et réactiver le formulaire
                _loadingCts?.Cancel();
                UpdateFormFromAquarium(a);
                SetFormEnabled(true);
            });
        }

        // ── Changement d'appareil ────────────────────────────────────────────────
        private void cbDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadCurrentValues();
            RequestParamsFromPLC();
        }

        // Broadcast cmd:0 + passe en mode "Loading" avec timeout de 5 s
        private void RequestParamsFromPLC()
        {
            _loadingCts?.Cancel();
            _loadingCts = new CancellationTokenSource();
            var token = _loadingCts.Token;

            SetFormEnabled(false);

            int deviceID = cbDevice.SelectedIndex + 1;
            int PLCID    = GetPLCID(deviceID);
            _ = _server.BroadcastMessageAsync(
                $"{{\"cmd\":0,\"AquaID\":{deviceID},\"PLCID\":{PLCID}}}");

            // Réactiver automatiquement si pas de réponse après 5 s
            _ = Task.Delay(5000, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Dispatcher.InvokeAsync(() => SetFormEnabled(true));
            });
        }

        // ── Changement de régulateur ─────────────────────────────────────────────
        private void rbRegul_Checked(object sender, RoutedEventArgs e) => LoadCurrentValues();

        // ── Chargement depuis valeurs stockées ────────────────────────────────────
        private void LoadCurrentValues()
        {
            if (cbDevice == null) return;
            int idx = cbDevice.SelectedIndex;
            if (idx < 0 || idx >= _server.Aquariums.Count) return;
            UpdateFormFromAquarium(_server.Aquariums[idx]);
        }

        // ── Remplissage du formulaire ─────────────────────────────────────────────
        private void UpdateFormFromAquarium(Aquarium a)
        {
            bool isTemp = rbTemp?.IsChecked == true;
            var  regul  = isTemp ? a.regulTemp : a.regulpH;
            int  idx    = a.ID - 1;

            double ambient = GetAmbient(isTemp);
            lblAmbient.Content = isTemp
                ? $"Ambient value : {ambient:F2} °C"
                : $"Ambient value : {ambient:F2}";

            cbState.SelectedIndex = Math.Max(0, Math.Min(2, a.state));

            bool   uo        = isTemp ? _useOffsetTemp[idx] : _useOffsetPH[idx];
            double offsetVal = isTemp ? _offsetTemp[idx]     : _offsetPH[idx];

            chkUseOffset.IsChecked = uo;
            tbOffset.IsEnabled     = uo;
            tbSetpoint.IsEnabled   = !uo;

            if (regul == null) return;
            var ci = CultureInfo.InvariantCulture;

            tbKp.Text              = regul.Kp.ToString("F5", ci);
            tbKi.Text              = regul.Ki.ToString("F5", ci);
            tbKd.Text              = regul.Kd.ToString("F5", ci);
            chkAutorisationForcage.IsChecked = regul.autorisationForcage;
            tbConsigneForcage.Text = regul.consigneForcage.ToString("F2", ci);
            tbSetpoint.Text        = regul.consigne.ToString("F2", ci);
            tbOffset.Text          = offsetVal.ToString("F2", ci);
        }

        // ── Enable / disable tous les champs modifiables ──────────────────────────
        private void SetFormEnabled(bool enabled)
        {
            bool uo = chkUseOffset.IsChecked == true;

            rbTemp.IsEnabled                 = enabled;
            rbPH.IsEnabled                   = enabled;
            cbState.IsEnabled                = enabled;
            chkUseOffset.IsEnabled           = enabled;
            tbOffset.IsEnabled               = enabled && uo;
            tbSetpoint.IsEnabled             = enabled && !uo;
            tbKp.IsEnabled                   = enabled;
            tbKi.IsEnabled                   = enabled;
            tbKd.IsEnabled                   = enabled;
            chkAutorisationForcage.IsEnabled = enabled;
            tbConsigneForcage.IsEnabled      = enabled;
            btnSubmit.IsEnabled              = enabled;

            lblLoading.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Valeur ambiante (circuit sortie = Data[2]) ────────────────────────────
        private double GetAmbient(bool isTemp)
        {
            var md = _server.LastMasterData;
            if (md?.Data == null || md.Data.Count < 3) return 0.0;
            return isTemp ? md.Data[2].Temperature : md.Data[2].PH;
        }

        // ── Use Offset ────────────────────────────────────────────────────────────
        private void chkUseOffset_Changed(object sender, RoutedEventArgs e)
        {
            if (tbOffset == null) return;
            bool uo = chkUseOffset.IsChecked == true;
            tbOffset.IsEnabled   = uo;
            tbSetpoint.IsEnabled = !uo;
            RecalculateSetpoint();
        }

        private void tbOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (chkUseOffset?.IsChecked == true)
                RecalculateSetpoint();
        }

        private void RecalculateSetpoint()
        {
            if (chkUseOffset?.IsChecked != true || tbSetpoint == null) return;
            bool isTemp = rbTemp?.IsChecked == true;
            tbSetpoint.Text = (GetAmbient(isTemp) + Parse(tbOffset.Text))
                              .ToString("F2", CultureInfo.InvariantCulture);
        }

        // ── Submit ────────────────────────────────────────────────────────────────
        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            int  deviceID = cbDevice.SelectedIndex + 1;
            bool isTemp   = rbTemp.IsChecked == true;
            int  PLCID    = GetPLCID(deviceID);
            var  ci       = CultureInfo.InvariantCulture;
            var  a        = _server.Aquariums[deviceID - 1];
            int  idx      = deviceID - 1;

            // Sauvegarder useOffset et offset au moment du Submit
            bool uo = chkUseOffset.IsChecked == true;
            if (isTemp) { _useOffsetTemp[idx] = uo; _offsetTemp[idx] = Parse(tbOffset.Text); }
            else        { _useOffsetPH[idx]   = uo; _offsetPH[idx]   = Parse(tbOffset.Text); }
            SavePrefs();

            double cons = uo
                ? GetAmbient(isTemp) + Parse(tbOffset.Text)
                : Parse(tbSetpoint.Text);

            string rTempJson = isTemp
                ? BuildRegulJson(cons, ci)
                : BuildRegulFromStored(a.regulTemp, ci);

            string rpHJson = !isTemp
                ? BuildRegulJson(cons, ci)
                : BuildRegulFromStored(a.regulpH, ci);

            string boolStr(bool? b) => b == true ? "true" : "false";

            string msg = "{"
                + $"\"cmd\":2,"
                + $"\"PLCID\":{PLCID},"
                + $"\"AquaID\":{deviceID},"
                + $"\"useOffset\":{boolStr(chkUseOffset.IsChecked)},"
                + $"\"state\":{cbState.SelectedIndex},"
                + $"\"rTemp\":{rTempJson},"
                + $"\"rpH\":{rpHJson}"
                + "}";

            _ = _server.BroadcastMessageAsync(msg);

            RequestParamsFromPLC();
        }

        private string BuildRegulJson(double cons, CultureInfo ci)
        {
            int    idx    = cbDevice.SelectedIndex;
            bool   isTemp = rbTemp?.IsChecked == true;
            double offset = isTemp ? _offsetTemp[idx] : _offsetPH[idx];

            return "{"
                + $"\"cons\":{cons.ToString("F2", ci)},"
                + $"\"Kp\":{Parse(tbKp.Text).ToString("F5", ci)},"
                + $"\"Ki\":{Parse(tbKi.Text).ToString("F5", ci)},"
                + $"\"Kd\":{Parse(tbKd.Text).ToString("F5", ci)},"
                + $"\"autorisationForcage\":{(chkAutorisationForcage.IsChecked == true ? "true" : "false")},"
                + $"\"consigneForcage\":{Parse(tbConsigneForcage.Text).ToString("F2", ci)},"
                + $"\"offset\":{offset.ToString("F2", ci)}"
                + "}";
        }

        private static string BuildRegulFromStored(Regul r, CultureInfo ci)
        {
            if (r == null)
                return "{\"cons\":0.0,\"Kp\":0.0,\"Ki\":0.0,\"Kd\":0.0,\"autorisationForcage\":false,\"consigneForcage\":0.0,\"offset\":0.0}";
            return "{"
                + $"\"cons\":{r.consigne.ToString("F2", ci)},"
                + $"\"Kp\":{r.Kp.ToString("F5", ci)},"
                + $"\"Ki\":{r.Ki.ToString("F5", ci)},"
                + $"\"Kd\":{r.Kd.ToString("F5", ci)},"
                + $"\"autorisationForcage\":{(r.autorisationForcage ? "true" : "false")},"
                + $"\"consigneForcage\":{r.consigneForcage.ToString("F2", ci)},"
                + $"\"offset\":{r.offset.ToString("F2", ci)}"
                + "}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static double Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            double.TryParse(text.Replace(',', '.'), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v);
            return v;
        }

        private static int GetPLCID(int deviceID)
        {
            if      (deviceID <  4) return 1;
            else if (deviceID <  7) return 2;
            else if (deviceID < 10) return 3;
            else if (deviceID < 13) return 4;
            else if (deviceID < 17) return 6;
            else                    return 7;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _loadingCts?.Cancel();
            this.Hide();
            e.Cancel = true;
        }
    }
}
