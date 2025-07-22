// WinUI 3 app using Windows App SDK
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NLSampleApp
{
    public class UIMessage
    {
        public string CommandType { get; set; }
        public string Data { get; set; }
    }

    public class Game
    {
        public string gameID { get; set; }
        public string label { get; set; }
        public string value { get; set; }
    }

    public class Games
    {
        public string type { get; set; }
        public ObservableCollection<Game> games { get; set; }
    }
    public class GameAnalysisRequestClient
    {
        public string Query { get; set; }
        public string Source { get; set; }

    }

    public partial class MainWindow : Window
    {
        private ClientWebSocket updateSocket = new ClientWebSocket();
        private ObservableCollection<string> messages = new ObservableCollection<string>();
        private ObservableCollection<Game> gameList = new ObservableCollection<Game>();

        public MainWindow()
        {
            this.InitializeComponent();
            GameComboBox.ItemsSource = gameList;
            GameComboBox.SelectionChanged += GameComboBox_SelectionChanged;
            StartWebSockets();
        }

        private bool StartWebSockets()
        {
            try
            {
                updateSocket.ConnectAsync(new Uri("ws://localhost:5002/ws/update"), CancellationToken.None).GetAwaiter().GetResult();
             
                SendMessage(updateSocket, new UIMessage { CommandType = "hello", Data = "" }).GetAwaiter().GetResult();

                _ = ReceiveMessages(updateSocket);
            }
            catch (Exception ex)
            {
                var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                dispatcherQueue.TryEnqueue(() =>
                {
                    OutputBox.Text = $"StartWebSockets failed: {ex.Message}";
                });
            }
            return true;
        }

        private async Task SendMessage(ClientWebSocket socket, UIMessage message)
        {
            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveMessages(ClientWebSocket socket)
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        OutputBox.Text = $"Socket error: {ex.Message}";
                    });
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        string messageJson = messageBuilder.ToString();
                        messageBuilder.Clear();

                        try
                        {
                            var message = JsonSerializer.Deserialize<UIMessage>(messageJson);
                            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();


                            if (message.CommandType.ToLower() == "updategamelist")
                            {
                                var games = JsonSerializer.Deserialize<Games>(message.Data);
                                dispatcherQueue.TryEnqueue(() => {
                                    gameList.Clear();
                                    foreach (var g in games.games)
                                        gameList.Add(g);
                                });
                            }
                            else if (message.CommandType.ToLower() == "gameaianalysis")
                            {
                                try
                                {
                                    var jsonDoc = JsonDocument.Parse(message.Data);
                                    if (jsonDoc.RootElement.TryGetProperty("Analysis", out var analysisProp))
                                    {
                                        var analysisText = analysisProp.GetString();
                                        dispatcherQueue.TryEnqueue(() => {
                                            OutputBox.Text = analysisText;
                                        });
                                    }
                                    else
                                    {
                                        dispatcherQueue.TryEnqueue(() => {
                                            OutputBox.Text = "[Missing 'Analysis' field in response]";
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    dispatcherQueue.TryEnqueue(() => {
                                        OutputBox.Text = $"Failed to parse analysis: {ex.Message}";
                                    });
                                }
                            }
                            else if (message.CommandType.ToLower() == "helloresponse")
                            {
                                try
                                {
                                    await SendMessage(updateSocket, new UIMessage { CommandType = "getgames", Data = "" });
                                                                                                            
                                }
                                catch (Exception ex)
                                {
                                    dispatcherQueue.TryEnqueue(() =>
                                    {
                                        OutputBox.Text = $"Failed to send getgames: {ex.Message}";
                                    });
                                }
                            }
                            else if (message.CommandType.ToLower() == "matchinfo")
                            {
                            }
                            else if (message.CommandType.ToLower() == "supportedgames")
                            {
                            }
                            else if (message.CommandType.ToLower() == "updatemetrics")
                            {
                            }
                            else if (message.CommandType.ToLower() == "updatematchstats")
                            {
                            }
                            else if (message.CommandType.ToLower() == "setcurrentgame")
                            {
                            }
                            else if (message.CommandType.ToLower() == "gameanalysisstrings")
                            {
                            }
                            else
                            {
                                dispatcherQueue.TryEnqueue(() =>
                                {
                                    OutputBox.Text = $"Unhandled message: {message.CommandType}";
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                OutputBox.Text = $"JSON parse error: {ex.Message}\nRaw: {messageJson}";
                            });
                        }
                    }
                }
            }
        }

        private async void GameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameComboBox.SelectedItem is Game selectedGame)
            {
                var setGameMsg = new UIMessage
                {
                    CommandType = "setcurrentgame",
                    Data = selectedGame.value
                };
                await SendMessage(updateSocket, setGameMsg);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartWebSockets();
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            dispatcherQueue.TryEnqueue(() => {
                OutputBox.Text = "Analyzing...";
            });
            var query = InputBox.Text;
            var request = new { Query = query, Source = "WinUI" };
            var json = JsonSerializer.Serialize(new UIMessage
            {
                CommandType = "getgameanalysis",
                Data = JsonSerializer.Serialize(request)
            });
            var buffer = Encoding.UTF8.GetBytes(json);
            await updateSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
