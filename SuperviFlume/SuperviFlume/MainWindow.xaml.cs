using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WebSocketServerExample
{
    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://192.168.1.134:81/");
            _listener.Start();

            await AcceptWebSocketClientsAsync(_cts.Token);
        }

        private async Task AcceptWebSocketClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);

                    await HandleWebSocketAsync(webSocketContext.WebSocket, token);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        private async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken token)
        {
            var buffer = new byte[1024];

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    AppendToTextBox($"Received: {message}");

                    var responseMessage = $"Echo: {message}";
                    var responseBuffer = Encoding.UTF8.GetBytes(responseMessage);

                    await webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, token);
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", token);
                }
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", token);
            webSocket.Dispose();
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
        }
    }
}
