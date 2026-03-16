using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SuperviFlume_v2
{
    public partial class LogWindow : Window
    {
        private const int MaxLines = 500;
        private readonly ObservableCollection<string> _lines = new ObservableCollection<string>();

        public LogWindow()
        {
            InitializeComponent();
            LstLog.ItemsSource = _lines;
        }

        public void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _lines.Add($"[{DateTime.Now:HH:mm:ss.fff}]  {message}");

                while (_lines.Count > MaxLines)
                    _lines.RemoveAt(0);

                TxtCount.Text = $"{_lines.Count} ligne(s)";

                if (BtnPause.IsChecked != true)
                    LstLog.ScrollIntoView(_lines[_lines.Count - 1]);
            });
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _lines.Clear();
            TxtCount.Text = "0 ligne(s)";
        }

        // Masquer plutôt que fermer, pour pouvoir rouvrir la fenêtre
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
