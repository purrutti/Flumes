using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SuperviFlume_v2
{
    public partial class GeneralInletParamsWindow : Window
    {
        private readonly WebSocketServer _server;
        private int _condID = 0;

        private CancellationTokenSource _loadingCts;

        public GeneralInletParamsWindow(WebSocketServer server)
        {
            InitializeComponent();
            _server = server;

            _server.MasterDataReceived += OnMasterDataReceived;

            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue && _condID > 0)
                    RequestParamsFromMaster();
            };
        }

        // ── Réception MasterData ─────────────────────────────────────────────
        private void OnMasterDataReceived(MasterData md)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!IsVisible) return;
                _loadingCts?.Cancel();
                SetFormEnabled(true);
            });
        }

        // ── Requête cmd:0 vers le master PLC ────────────────────────────────
        private void RequestParamsFromMaster()
        {
            _loadingCts?.Cancel();
            _loadingCts = new CancellationTokenSource();
            var token = _loadingCts.Token;

            SetFormEnabled(false);

            _ = _server.BroadcastMessageAsync("{\"cmd\":0,\"AquaID\":0,\"PLCID\":5}");

            _ = Task.Delay(5000, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Dispatcher.InvokeAsync(() => SetFormEnabled(true));
            });
        }

        // ── Changement de condition ──────────────────────────────────────────
        private void cbCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCondition.SelectedItem is ComboBoxItem item)
            {
                _condID = int.Parse(item.Tag.ToString());
                grpTemp.Visibility = _condID == 3 ? Visibility.Collapsed : Visibility.Visible;
                RequestParamsFromMaster();
                LoadCurrentValues();
            }
        }

        // ── Chargement depuis LastMasterData ─────────────────────────────────
        private void LoadCurrentValues()
        {
            var md = _server.LastMasterData;
            if (md?.Data == null || _condID == 0) return;

            var item = md.Data.Find(d => d.ConditionID == _condID);
            if (item == null) return;

            var ci = CultureInfo.InvariantCulture;

            if (_condID != 3 && item.RTemp != null)
            {
                tbTempCons.Text          = item.RTemp.consigne.ToString("F2", ci);
                tbTempKp.Text            = item.RTemp.Kp.ToString("F5", ci);
                tbTempKi.Text            = item.RTemp.Ki.ToString("F5", ci);
                tbTempKd.Text            = item.RTemp.Kd.ToString("F5", ci);
                chkTempForcage.IsChecked = item.RTemp.autorisationForcage;
                tbTempConsForcage.Text   = item.RTemp.consigneForcage.ToString(ci);
            }

            if (item.RPression != null)
            {
                tbPresCons.Text          = item.RPression.consigne.ToString("F2", ci);
                tbPresKp.Text            = item.RPression.Kp.ToString("F5", ci);
                tbPresKi.Text            = item.RPression.Ki.ToString("F5", ci);
                tbPresKd.Text            = item.RPression.Kd.ToString("F5", ci);
                chkPresForcage.IsChecked = item.RPression.autorisationForcage;
                tbPresConsForcage.Text   = item.RPression.consigneForcage.ToString(ci);
            }
        }

        // ── Enable / disable formulaire ──────────────────────────────────────
        private void SetFormEnabled(bool enabled)
        {
            bool isTemp = _condID != 3;

            cbCondition.IsEnabled        = enabled;
            grpTemp.IsEnabled            = enabled && isTemp;
            tbPresCons.IsEnabled         = enabled;
            tbPresKp.IsEnabled           = enabled;
            tbPresKi.IsEnabled           = enabled;
            tbPresKd.IsEnabled           = enabled;
            chkPresForcage.IsEnabled     = enabled;
            tbPresConsForcage.IsEnabled  = enabled;
            btnSubmit.IsEnabled          = enabled;

            lblLoading.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Submit ────────────────────────────────────────────────────────────
        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_condID == 0)
            {
                MessageBox.Show("Please select a condition first.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ci = CultureInfo.InvariantCulture;

            string rPresJson = BuildRegulJson(
                Parse(tbPresCons.Text), Parse(tbPresKp.Text),
                Parse(tbPresKi.Text),   Parse(tbPresKd.Text),
                chkPresForcage.IsChecked == true,
                (int)Parse(tbPresConsForcage.Text), ci);

            string dataJson;
            if (_condID == 3)
            {
                dataJson = $"{{\"CondID\":{_condID},\"rPression\":{rPresJson}}}";
            }
            else
            {
                string rTempJson = BuildRegulJson(
                    Parse(tbTempCons.Text), Parse(tbTempKp.Text),
                    Parse(tbTempKi.Text),   Parse(tbTempKd.Text),
                    chkTempForcage.IsChecked == true,
                    (int)Parse(tbTempConsForcage.Text), ci);

                dataJson = $"{{\"CondID\":{_condID},\"rTemp\":{rTempJson},\"rPression\":{rPresJson}}}";
            }

            string msg = "{\"cmd\":2,\"PLCID\":5,\"AquaID\":0,\"data\":["
                       + dataJson
                       + "]}";

            _ = _server.BroadcastMessageAsync(msg);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string BuildRegulJson(double cons, double kp, double ki, double kd,
                                             bool forcage, int consForcage, CultureInfo ci)
        {
            return "{"
                + $"\"cons\":{cons.ToString("F2", ci)},"
                + $"\"Kp\":{kp.ToString("F5", ci)},"
                + $"\"Ki\":{ki.ToString("F5", ci)},"
                + $"\"Kd\":{kd.ToString("F5", ci)},"
                + $"\"autorisationForcage\":{(forcage ? "true" : "false")},"
                + $"\"consigneForcage\":{consForcage}"
                + "}";
        }

        private static double Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            double.TryParse(text.Replace(',', '.'), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v);
            return v;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _loadingCts?.Cancel();
            this.Hide();
            e.Cancel = true;
        }
    }
}
