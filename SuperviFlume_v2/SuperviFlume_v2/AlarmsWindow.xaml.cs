using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SuperviFlume_v2
{
    public partial class AlarmsWindow : Window
    {
        private readonly AlarmManager _manager;

        public AlarmsWindow(AlarmManager manager)
        {
            InitializeComponent();
            _manager = manager;
            dgAlarms.ItemsSource = _manager.ActiveAlarms;
            LoadSettings();
        }

        // ── Chargement des paramètres dans le formulaire ───────────────────────
        private void LoadSettings()
        {
            var s  = _manager.Settings;
            var ci = CultureInfo.InvariantCulture;

            chkTemp.IsChecked      = s.TemperatureEnabled;
            tbTempDelta.Text       = s.TemperatureDelta.ToString("F2", ci);

            chkPH.IsChecked        = s.PHEnabled;
            tbPHDelta.Text         = s.PHDelta.ToString("F2", ci);

            chkO2.IsChecked        = s.O2Enabled;
            tbO2Min.Text           = s.O2Min.ToString("F2", ci);

            chkFlowrate.IsChecked  = s.FlowrateEnabled;
            tbFlowrateMin.Text     = s.FlowrateMin.ToString("F2", ci);

            chkSpeed.IsChecked     = s.SpeedEnabled;
            tbSpeedMin.Text        = s.SpeedMin.ToString("F2", ci);
        }

        // ── Sauvegarde ────────────────────────────────────────────────────────
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = _manager.Settings;

            s.TemperatureEnabled = chkTemp.IsChecked == true;
            s.TemperatureDelta   = Parse(tbTempDelta.Text);

            s.PHEnabled          = chkPH.IsChecked == true;
            s.PHDelta            = Parse(tbPHDelta.Text);

            s.O2Enabled          = chkO2.IsChecked == true;
            s.O2Min              = Parse(tbO2Min.Text);

            s.FlowrateEnabled    = chkFlowrate.IsChecked == true;
            s.FlowrateMin        = Parse(tbFlowrateMin.Text);

            s.SpeedEnabled       = chkSpeed.IsChecked == true;
            s.SpeedMin           = Parse(tbSpeedMin.Text);

            _manager.SaveSettings();
        }

        // ── Acquittement d'une alarme ─────────────────────────────────────────
        private void btnAck_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Alarme alarm)
                alarm.Acknowledge();
        }

        // ── Helper ───────────────────────────────────────────────────────────
        private static double Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            double.TryParse(text.Replace(',', '.'), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v);
            return v;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
    }
}
