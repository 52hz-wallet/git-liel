using WinFormsApp_ant.Tools;
using System.Text.Json;
using System.Reflection;
using ScottPlot.WinForms;
using System.Globalization;

namespace WinFormsApp_ant.UIPlugins
{
    [pluginArg(name = "Exchange_rate", index = 2, type = "其他", text = "港股历史汇率走势", single = true)]
    public partial class exchange_rate : BasePluginForm
    {
        public exchange_rate()
        {
            InitializeComponent();
            var pluginArg = GetType().GetCustomAttribute<pluginArgAttribute>();
            
            // Debug: Log plugin registration
            System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Registering message handler for pluginName: '{pluginArg.name}'");
            WebSocketClient.RegisterPluginMessageHandler_withPluginName(pluginArg.name, HandleMessage);
            System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Message handler registered successfully");
            
            // Initialize DataGridView columns
            InitializeDataGridView();
            
            // Set default dates: one week ago to today
            DateTime today = DateTime.Today;
            DateTime oneWeekAgo = today.AddDays(-7);
            
            if (startDatePicker != null)
                startDatePicker.Value = oneWeekAgo;
            if (endDatePicker != null)
                endDatePicker.Value = today;
        }

        private void InitializeDataGridView()
        {
            if (dataGridView == null)
                return;

            dataGridView.Columns.Clear();
            dataGridView.Columns.Add("TradeDate", "交易日期");
            dataGridView.Columns.Add("ReferenceRate", "参考汇率中间价");
            dataGridView.Columns.Add("EstimatedRate", "估值汇率");
            dataGridView.Columns.Add("Difference", "差值");

            // Set column widths
            dataGridView.Columns["TradeDate"].Width = 120;
            dataGridView.Columns["ReferenceRate"].Width = 150;
            dataGridView.Columns["EstimatedRate"].Width = 150;
            dataGridView.Columns["Difference"].Width = 150;
        }

        private async void exchange_rate_Load(object? sender, EventArgs e)
        {
            // Load data on window open
            // Delay slightly to ensure UI is fully initialized
            await Task.Delay(100);
            
            try
            {
                await LoadDataAsync();
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Silently handle task cancellation (timeout, etc.) - this is normal
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Task canceled in exchange_rate_Load (this is normal)");
            }
            catch (System.ObjectDisposedException)
            {
                // Silently handle object disposal (during shutdown) - this is normal
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Object disposed in exchange_rate_Load (this is normal)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error in exchange_rate_Load: {ex.Message}");
                // Don't show error on load, just log it
            }
        }

        private async Task LoadDataAsync()
        {
            if (startDatePicker == null || endDatePicker == null)
                return;

            try
            {
                string startDate = startDatePicker.Value.ToString("yyyyMMdd");
                string endDate = endDatePicker.Value.ToString("yyyyMMdd");

                var dict_data = new Dictionary<string, object>
                {
                    { "startDate", startDate },
                    { "endDate", endDate }
                };

                var pluginArg = GetType().GetCustomAttribute<pluginArgAttribute>();
                
                // Debug: Log request being sent
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Preparing to send request:");
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate]   pluginName={pluginArg.name}");
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate]   instanceId={this.instanceId}");
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate]   startDate={startDate}");
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate]   endDate={endDate}");
                
                // Check if WebSocket is connected
                if (!WebSocketClient.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[ExchangeRate] WebSocket is not connected, cannot send request");
                    MessageBox.Show("WebSocket 未连接，无法发送请求。请检查后端服务是否运行。", "连接错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] WebSocket is connected, sending request...");
                await WebSocketClient.SendServer(pluginArg.name, this.instanceId, dict_data);
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Request sent, waiting for response...");
            }
            catch (System.Threading.Tasks.TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Task canceled while sending request: {ex.Message}");
                // Task was canceled, this is usually not a critical error
            }
            catch (System.ObjectDisposedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Object disposed while sending request: {ex.Message}");
                // Object was disposed, might be during shutdown
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error in LoadDataAsync: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void HandleMessage(JsonDocument? doc)
        {
            if (doc == null)
            {
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] HandleMessage: doc is null");
                return;
            }

            try
            {
                // IMPORTANT: Extract all data immediately before JsonDocument is disposed
                // Backend returns JSON with pluginArg and data fields
                JsonElement root = doc.RootElement;
                
                // Debug: Log received message (extract string immediately)
                string fullMessage = root.GetRawText();
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Received message: {fullMessage.Substring(0, Math.Min(500, fullMessage.Length))}");

                // Find and extract the data array as JSON string to avoid disposal issues
                string? dataJsonString = null;
                int arrayLength = 0;

                // Check if root is an array (direct array response - unlikely but possible)
                if (root.ValueKind == JsonValueKind.Array)
                {
                    System.Diagnostics.Debug.WriteLine("[ExchangeRate] Root is array, using directly");
                    dataJsonString = root.GetRawText();
                    arrayLength = root.GetArrayLength();
                }
                // If wrapped in structure, try to find array in "data" field (expected format)
                else if (root.TryGetProperty("data", out JsonElement dataElement) && 
                         dataElement.ValueKind == JsonValueKind.Array)
                {
                    arrayLength = dataElement.GetArrayLength();
                    System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Found data array with {arrayLength} items");
                    dataJsonString = dataElement.GetRawText();
                }
                // If root is object but no "data" field, check if it's the array itself
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    // Backend might send the array directly as the message body
                    // Try to find any array property
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Found array in property: {prop.Name}");
                            dataJsonString = prop.Value.GetRawText();
                            arrayLength = prop.Value.GetArrayLength();
                            break;
                        }
                    }
                }

                // Now we have the JSON string, parse it in a new document that we control
                if (!string.IsNullOrEmpty(dataJsonString))
                {
                    System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Processing {arrayLength} records");
                    
                    // Parse the data array string into a new document
                    using var dataDoc = JsonDocument.Parse(dataJsonString);
                    JsonElement dataArray = dataDoc.RootElement;
                    
                    // Update UI on UI thread - pass the JsonElement which is valid within this scope
                    if (this.InvokeRequired)
                    {
                        // Clone the data to avoid disposal issues
                        string clonedDataJson = dataJsonString;
                        this.Invoke(new Action(() => {
                            try
                            {
                                using var clonedDoc = JsonDocument.Parse(clonedDataJson);
                                UpdateChartAndTable(clonedDoc.RootElement);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error in Invoke: {ex.Message}");
                            }
                        }));
                    }
                    else
                    {
                        UpdateChartAndTable(dataArray);
                    }
                }
                else
                {
                    // Log for debugging
                    string preview = fullMessage.Length > 500 ? fullMessage.Substring(0, 500) + "..." : fullMessage;
                    System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Data format not recognized. Full message: {preview}");
                    
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => {
                            MessageBox.Show($"Received data format is not recognized.\n\nMessage preview:\n{preview}", 
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                    else
                    {
                        MessageBox.Show($"Received data format is not recognized.\n\nMessage preview:\n{preview}", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Exception in HandleMessage: {ex.Message}\n{ex.StackTrace}");
                
                if (this.InvokeRequired)
                {
                    string errorMsg = ex.Message;
                    string errorStack = ex.StackTrace ?? "";
                    this.Invoke(new Action(() => {
                        MessageBox.Show($"Error processing message: {errorMsg}\n\nStack: {errorStack}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                else
                {
                    MessageBox.Show($"Error processing message: {ex.Message}\n\nStack: {ex.StackTrace}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateChartAndTable(JsonElement dataArray)
        {
            if (chartPlot == null || dataGridView == null)
            {
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Chart or DataGridView is null");
                return;
            }

            // Parse data
            List<ExchangeRateRecord> records = new List<ExchangeRateRecord>();
            
            // Log first record to see available fields
            bool firstRecordLogged = false;
            
            foreach (JsonElement record in dataArray.EnumerateArray())
            {
                try
                {
                    // Log available fields from first record for debugging
                    if (!firstRecordLogged && record.ValueKind == JsonValueKind.Object)
                    {
                        var fieldNames = new List<string>();
                        foreach (var prop in record.EnumerateObject())
                        {
                            fieldNames.Add($"{prop.Name} ({prop.Value.ValueKind})");
                        }
                        System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Available fields in first record: {string.Join(", ", fieldNames)}");
                        firstRecordLogged = true;
                    }
                    
                    ExchangeRateRecord rateRecord = new ExchangeRateRecord();
                    
                    // Extract tradeDateKey
                    if (record.TryGetProperty("tradeDateKey", out JsonElement dateElement))
                    {
                        if (dateElement.ValueKind == JsonValueKind.Number)
                        {
                            int dateInt = dateElement.GetInt32();
                            rateRecord.TradeDate = ParseDateFromInt(dateInt);
                        }
                        else if (dateElement.ValueKind == JsonValueKind.String)
                        {
                            string dateStr = dateElement.GetString() ?? "";
                            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, 
                                DateTimeStyles.None, out DateTime parsedDate))
                            {
                                rateRecord.TradeDate = parsedDate;
                            }
                        }
                    }

                    // Extract rate fields using actual database field names
                    // 参考汇率中间价: midRefExchangeRate
                    rateRecord.ReferenceRate = GetDoubleValue(record, "midRefExchangeRate") ?? 0.0;
                    
                    // 估值汇率: valExchangeRate
                    rateRecord.EstimatedRate = GetDoubleValue(record, "valExchangeRate") ?? 0.0;
                    
                    // 买入结算汇率: buySetExchangeRate
                    rateRecord.BuyRate = GetDoubleValue(record, "buySetExchangeRate") ?? 0.0;
                    
                    // 卖出结算汇率: sellSetExchangeRate
                    rateRecord.SellRate = GetDoubleValue(record, "sellSetExchangeRate") ?? 0.0;
                    
                    // Debug: Log extracted values for first record
                    if (records.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ExchangeRate] First record values:");
                        System.Diagnostics.Debug.WriteLine($"  TradeDate: {rateRecord.TradeDate:yyyy-MM-dd}");
                        System.Diagnostics.Debug.WriteLine($"  ReferenceRate (midRefExchangeRate): {rateRecord.ReferenceRate}");
                        System.Diagnostics.Debug.WriteLine($"  EstimatedRate (valExchangeRate): {rateRecord.EstimatedRate}");
                        System.Diagnostics.Debug.WriteLine($"  BuyRate (buySetExchangeRate): {rateRecord.BuyRate}");
                        System.Diagnostics.Debug.WriteLine($"  SellRate (sellSetExchangeRate): {rateRecord.SellRate}");
                    }

                    if (rateRecord.TradeDate != default(DateTime))
                    {
                        records.Add(rateRecord);
                    }
                }
                catch (Exception ex)
                {
                    // Skip invalid records but log the error
                    System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error parsing record: {ex.Message}");
                    continue;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Parsed {records.Count} valid records from {dataArray.GetArrayLength()} total records");

            // Sort by date
            records = records.OrderBy(r => r.TradeDate).ToList();

            // Update chart
            UpdateChart(records);

            // Update table
            UpdateTable(records);
        }

        private double? GetDoubleValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetDouble();
                else if (prop.ValueKind == JsonValueKind.String)
                {
                    if (double.TryParse(prop.GetString(), out double value))
                        return value;
                }
            }
            return null;
        }

        private DateTime ParseDateFromInt(int dateInt)
        {
            string dateStr = dateInt.ToString();
            if (dateStr.Length == 8 && DateTime.TryParseExact(dateStr, "yyyyMMdd", null, 
                DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
            return default(DateTime);
        }

        private void UpdateChart(List<ExchangeRateRecord> records)
        {
            if (chartPlot == null)
            {
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] ChartPlot is null");
                return;
            }
            
            if (records.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] No records to display in chart");
                chartPlot.Plot.Clear();
                chartPlot.Plot.Title("港股历史汇率走势 - 无数据");
                chartPlot.Refresh();
                return;
            }

            try
            {
                chartPlot.Plot.Clear();

                // Prepare data arrays
                double[] dates = records.Select(r => r.TradeDate.ToOADate()).ToArray();
                double[] referenceRates = records.Select(r => r.ReferenceRate).ToArray();
                double[] estimatedRates = records.Select(r => r.EstimatedRate).ToArray();
                double[] buyRates = records.Select(r => r.BuyRate).ToArray();
                double[] sellRates = records.Select(r => r.SellRate).ToArray();

                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Updating chart with {records.Count} records");
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Date range: {records.First().TradeDate:yyyy-MM-dd} to {records.Last().TradeDate:yyyy-MM-dd}");

                // Add series to chart with database field names as labels
                var refSeries = chartPlot.Plot.Add.Scatter(dates, referenceRates);
                refSeries.Label = "midRefExchangeRate";
                refSeries.Color = ScottPlot.Color.FromHex("#1f77b4");

                var estSeries = chartPlot.Plot.Add.Scatter(dates, estimatedRates);
                estSeries.Label = "valExchangeRate";
                estSeries.Color = ScottPlot.Color.FromHex("#ff7f0e");

                var buySeries = chartPlot.Plot.Add.Scatter(dates, buyRates);
                buySeries.Label = "buySetExchangeRate";
                buySeries.Color = ScottPlot.Color.FromHex("#2ca02c");

                var sellSeries = chartPlot.Plot.Add.Scatter(dates, sellRates);
                sellSeries.Label = "sellSetExchangeRate";
                sellSeries.Color = ScottPlot.Color.FromHex("#d62728");

                // Configure chart
                chartPlot.Plot.Axes.DateTimeTicksBottom();
                chartPlot.Plot.ShowLegend();
                chartPlot.Plot.Title("港股历史汇率走势");
                chartPlot.Refresh();
                
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Chart updated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error updating chart: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"更新图表时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTable(List<ExchangeRateRecord> records)
        {
            if (dataGridView == null)
            {
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] DataGridView is null");
                return;
            }

            try
            {
                // Clear existing data
                dataGridView.Rows.Clear();

                foreach (var record in records)
                {
                    double difference = record.ReferenceRate - record.EstimatedRate;
                    
                    dataGridView.Rows.Add(
                        record.TradeDate.ToString("yyyy-MM-dd"),
                        record.ReferenceRate.ToString("F6"),
                        record.EstimatedRate.ToString("F6"),
                        difference.ToString("F6")
                    );
                }
                
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Table updated with {records.Count} rows");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error updating table: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"更新表格时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DatePicker_ValueChanged(object? sender, EventArgs e)
        {
            // Revalidate dates
            if (startDatePicker != null && endDatePicker != null)
            {
                if (startDatePicker.Value > endDatePicker.Value)
                {
                    MessageBox.Show("开始日期不能晚于结束日期", "日期错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Reload data when date changes
            try
            {
                await LoadDataAsync();
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Silently handle - this is normal for network operations
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Task canceled in DatePicker_ValueChanged (this is normal)");
            }
            catch (System.ObjectDisposedException)
            {
                // Silently handle - this is normal during shutdown
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Object disposed in DatePicker_ValueChanged (this is normal)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error in DatePicker_ValueChanged: {ex.Message}");
            }
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            try
            {
                await LoadDataAsync();
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Silently handle - this is normal for network operations
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Task canceled in RefreshButton_Click (this is normal)");
            }
            catch (System.ObjectDisposedException)
            {
                // Silently handle - this is normal during shutdown
                System.Diagnostics.Debug.WriteLine("[ExchangeRate] Object disposed in RefreshButton_Click (this is normal)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeRate] Error in RefreshButton_Click: {ex.Message}");
                MessageBox.Show($"刷新数据时出错: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class ExchangeRateRecord
        {
            public DateTime TradeDate { get; set; }
            public double ReferenceRate { get; set; }
            public double EstimatedRate { get; set; }
            public double BuyRate { get; set; }
            public double SellRate { get; set; }
        }
    }
}