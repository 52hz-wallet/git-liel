
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WinFormsApp_ant.Tools
{
    public static class WebSocketClient
    {
        public static ClientWebSocket Client { get; private set; } = new ClientWebSocket();

        public static bool IsConnected => Client?.State == WebSocketState.Open; // ��������״̬����

        private static readonly Dictionary<string, Action<JsonDocument?>> handlers_pluginName = new();
        private static readonly Dictionary<string, Action<JsonDocument?>> handlers_pluginInstanceId = new();
        private static System.Timers.Timer pingTimer;

        //private static CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        public static async Task StartConnectAsync()
        {

            try
            {
                await ConnectAsync();
                _ = Task.Run(ReceiveLoopAsync);
                InitializePingTimer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static async Task ConnectAsync()
        {

            try
            {
                var AntBaseUrl = GetWebSocketUrl();
                var url = new Uri(AntBaseUrl + "/ws?token=token123");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await Client.ConnectAsync(url, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static string GetWebSocketUrl(string configPath = "Config/tools_config.json")
        {
            try
            {
                var jsonString = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.TryGetProperty("WebSocketClient", out JsonElement webSocketClient) &&
                    webSocketClient.TryGetProperty("AntBaseUrl", out JsonElement antBaseUrl))
                {
                    return antBaseUrl.GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"��ȡ�����ļ�ʧ��: {ex.Message}");
            }

            return null; // �򷵻�Ĭ��ֵ
        }
        private static void InitializePingTimer()
        {
            pingTimer = new System.Timers.Timer(30000); // 30����
            pingTimer.Elapsed += async (sender, e) =>
            {
                if (Client?.State == WebSocketState.Open)
                {
                    try
                    {
                        var pingMessage = new Dictionary<string, object>
                            {
                                { "arg", new Dictionary<string, object> { { "topic", "ping" } } },
                                { "data", "" }
                            };
                        await SendServer("ping", "ping", pingMessage);
                        Console.WriteLine("�ѷ��� Ping ��Ϣ");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"���� Ping ��Ϣʧ��: {ex.Message}");
                    }
                }
            };
            pingTimer.AutoReset = true; // �����Զ��ظ�
            pingTimer.Start();
        }

        private static async Task ReceiveLoopAsync()
        {
            var buffer = new byte[524288];
            while (true)
            {
                if (IsConnected)
                {
                    try
                    {
                        var result = await Client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close) break;

                        // Optimized buffer handling
                        var messageString = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));

                        // Debug: Log received message
                        Console.WriteLine($"[WebSocketClient] Received message: {messageString.Substring(0, Math.Min(500, messageString.Length))}");

                        // Parse JSON with optimizations
                        using var document = JsonDocument.Parse(messageString);
                        var root = document.RootElement;

                        // Optimized property access with combined checks
                        if (root.TryGetProperty("pluginArg", out var pluginArg))
                        {
                            // Try to get handler by name first
                            if (pluginArg.TryGetProperty("name", out var nameProp) &&
                                nameProp.ValueKind == JsonValueKind.String)
                            {
                                var name = nameProp.GetString();
                                Console.WriteLine($"[WebSocketClient] Looking for handler by pluginName: '{name}'");
                                Console.WriteLine($"[WebSocketClient] Registered handlers: {string.Join(", ", handlers_pluginName.Keys)}");
                                
                                if (name is not null && handlers_pluginName.TryGetValue(name, out var handler))
                                {
                                    Console.WriteLine($"[WebSocketClient] Found handler for '{name}', calling it");
                                    // Clone the document to avoid disposal issues
                                    string clonedJson = messageString;
                                    using var clonedDoc = JsonDocument.Parse(clonedJson);
                                    handler(clonedDoc);
                                }
                                else
                                {
                                    Console.WriteLine($"[WebSocketClient] No handler found for pluginName: '{name}'");
                                }
                            }

                            // Then try by instanceId
                            if (pluginArg.TryGetProperty("instanceId", out var instanceIdProp) &&
                                instanceIdProp.ValueKind == JsonValueKind.String)
                            {
                                var instanceId = instanceIdProp.GetString();
                                if (instanceId is not null && handlers_pluginInstanceId.TryGetValue(instanceId, out var handler))
                                {
                                    Console.WriteLine($"[WebSocketClient] Found handler by instanceId: '{instanceId}', calling it");
                                    // Clone the document to avoid disposal issues
                                    string clonedJson = messageString;
                                    using var clonedDoc = JsonDocument.Parse(clonedJson);
                                    handler(clonedDoc);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[WebSocketClient] Received message does not contain 'pluginArg'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                else
                {
                    // ��������
                    await ReconnectAsync();
                    // ����ʧ�ܣ��ȴ�10����������
                    Console.WriteLine("�ȴ�10����ٴγ�������...");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }

            }
        }


        private static async Task ReconnectAsync()
        {

            Console.WriteLine("��ʼ��������...");

            try
            {
                // ������������
                if (Client != null && (Client.State == WebSocketState.Open ||
                    Client.State == WebSocketState.CloseReceived ||
                    Client.State == WebSocketState.CloseSent))
                {
                    await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                }
                else if (Client != null)
                {
                    // ������Ч״̬��ֱ���ͷ���Դ
                    Client.Dispose();
                    Client = null;
                }

                // ���´���ClientWebSocketʵ��
                Client = new ClientWebSocket();

                await ConnectAsync();
            }
            catch (Exception ex)
            {

            }
        }

        public static void RegisterPluginMessageHandler_withPluginName(string pluginName, Action<JsonDocument?> handleMessage)
        {
            handlers_pluginName[pluginName] = handleMessage;
        }

        public static void RegisterPluginMessageHandler_withPluginInstanceId(string pluginInstanceId, Action<JsonDocument?> handleMessage)
        {
            handlers_pluginInstanceId[pluginInstanceId] = handleMessage;
        }

        public static async Task SendServer(string pluginName, string pluginInstanceId, Dictionary<string, object> messageDict)
        {
            try
            {
                // Check if WebSocket is connected
                if (Client == null || Client.State != WebSocketState.Open)
                {
                    Console.WriteLine($"WebSocket is not connected. State: {Client?.State}");
                    return;
                }

                var jsonObject = new Dictionary<string, object>(messageDict);

                if (!jsonObject.ContainsKey("pluginArg"))
                {
                    jsonObject["pluginArg"] = new Dictionary<string, object>();
                }

                if (jsonObject["pluginArg"] is Dictionary<string, object> pluginArgDict)
                {
                    pluginArgDict["name"] = pluginName;
                    pluginArgDict["instanceId"] = pluginInstanceId;
                }
                else
                {
                    // If pluginArg is not a dictionary, replace it with a new one
                    var newPluginArg = new Dictionary<string, object> { ["name"] = pluginName, ["instanceId"] = pluginInstanceId };
                    jsonObject["pluginArg"] = newPluginArg;
                }

                string updatedMessage = JsonSerializer.Serialize(jsonObject);
                
                // Debug: Log the message being sent
                Console.WriteLine($"[WebSocketClient] Sending message to plugin '{pluginName}': {updatedMessage.Substring(0, Math.Min(500, updatedMessage.Length))}");
                
                byte[] bytes = Encoding.UTF8.GetBytes(updatedMessage);
                
                // Use a cancellation token with timeout to avoid hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
                
                Console.WriteLine($"[WebSocketClient] Message sent successfully to plugin '{pluginName}'");
            }
            catch (System.Threading.Tasks.TaskCanceledException ex)
            {
                Console.WriteLine($"SendServer: Task was canceled - {ex.Message}");
                // Task was canceled (likely timeout), this is not critical
            }
            catch (System.ObjectDisposedException ex)
            {
                Console.WriteLine($"SendServer: Object was disposed - {ex.Message}");
                // Object was disposed, might be during shutdown
            }
            catch (System.Net.WebSockets.WebSocketException ex)
            {
                Console.WriteLine($"SendServer: WebSocket error - {ex.Message}, State: {Client?.State}");
                // WebSocket error, connection might be broken
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendServer: Unexpected error - {ex.Message}, Type: {ex.GetType().Name}");
            }
        }

    }
}