#include "plugin_interface.h"
#include "plugin_registry.h"
#include "logger.h"
#include "websocket_server.h"
#include "input.h"
#include "trade.h"
#include <map>
#include <string>
#include <memory>
#include <vector>
#include <mutex>
#include <json.hpp>


#if defined(_WIN32) || defined(__CYGWIN__)
#ifdef EXCHANGE_RATE_EXPORTS
#define EXCHANGE_RATE_API __declspec(dllexport)
#else
#define EXCHANGE_RATE_API __declspec(dllimport)
#endif
#else
#define EXCHANGE_RATE_API __attribute__ ((visibility ("default")))
#endif

class Exchange_rate : public PluginInterface {
public:
    void execute(const std::map<std::string, std::string>& parameters) override {
        // Register WebSocket handler for client requests
        if (m_webSocketServer) {
            m_webSocketServer->registerPluginHandler(
                pluginName,
                [this](connection_hdl hdl, json message) { handleClient(hdl, message); }
            );
        }

        // Create Input instance for data access
        m_input = std::make_unique<Tools::Input>();

        // Preload fixed date range data into cache
        // Start date: 2025-01-01, End date: 2025-11-28
        std::string startDate = "20250101";
        std::string endDate   = "20251128";

        Tools::Logger::info("Loading exchange rate data from " + startDate + " to " + endDate);

        // Load one year of exchange rate data into cache
        try {
            nlohmann::json data = m_input->get_mysql_data(
                "sunjq",
                "hk_exchange_rate",
                { { "tradeDateKey >= %s", startDate },
                  { "tradeDateKey <= %s", endDate } }
            );

            {
                std::lock_guard<std::mutex> lock(m_cacheMutex);
                m_cachedExchangeRateData = data;
            }

            Tools::Logger::info("Cached " + std::to_string(data.size()) + " exchange rate records");

            // Log first few records as sample to show what data looks like
            int maxSample = 5;
            int idx = 0;
            if (data.is_array()) {
                for (const auto& rec : data) {
                    if (idx >= maxSample) break;
                    Tools::Logger::info("Cached sample record [" + std::to_string(idx) + "]: " + rec.dump());
                    ++idx;
                }
            }
        }
        catch (const std::exception& e) {
            Tools::Logger::error(std::string("Failed to load exchange rate data: ") + e.what());
            // Initialize empty cache on error
            {
                std::lock_guard<std::mutex> lock(m_cacheMutex);
                m_cachedExchangeRateData = nlohmann::json::array();
            }
        }
        catch (...) {
            Tools::Logger::error("Unknown exception while loading exchange rate data");
            {
                std::lock_guard<std::mutex> lock(m_cacheMutex);
                m_cachedExchangeRateData = nlohmann::json::array();
            }
        }

        // Keep plugin running
        while (true) {
            std::this_thread::sleep_for(std::chrono::seconds(1));
        }
    }

    void setWebSocketServer(WebSocketServer* server) override {
        m_webSocketServer = server;
    }

private:
    std::string pluginName = "Exchange_rate";
    WebSocketServer* m_webSocketServer = nullptr;
    std::unique_ptr<Tools::Input> m_input;   // 行情 & 数据访问实例
    nlohmann::json m_cachedExchangeRateData;  // Cached exchange rate data (one year)
    std::mutex m_cacheMutex;                  // Mutex for thread-safe cache access

    void handleClient(connection_hdl hdl, json message) {
        // Receive client request
        Tools::Logger::info("Exchange_rate received message: " + message.dump());

        if (!m_webSocketServer) {
            Tools::Logger::error("Exchange_rate handleClient: WebSocket server not initialized");
            return;
        }

        try {
            // Extract startDate and endDate from client request
            // Frontend sends: { "pluginArg": {...}, "startDate": "20251101", "endDate": "20251125" }
            std::string startDate;
            std::string endDate;

            // Log all keys in message for debugging
            if (message.is_object()) {
                std::string keys = "Message keys: ";
                for (auto it = message.begin(); it != message.end(); ++it) {
                    keys += it.key() + " ";
                }
                Tools::Logger::info(keys);
            }

            // Try to get startDate and endDate from top level (expected location)
            if (message.contains("startDate")) {
                if (message["startDate"].is_string()) {
                    startDate = message["startDate"].get<std::string>();
                    Tools::Logger::info("Found startDate in top level: " + startDate);
                }
                else if (message["startDate"].is_number()) {
                    startDate = std::to_string(message["startDate"].get<int>());
                    Tools::Logger::info("Found startDate (number) in top level: " + startDate);
                }
            }
            
            if (message.contains("endDate")) {
                if (message["endDate"].is_string()) {
                    endDate = message["endDate"].get<std::string>();
                    Tools::Logger::info("Found endDate in top level: " + endDate);
                }
                else if (message["endDate"].is_number()) {
                    endDate = std::to_string(message["endDate"].get<int>());
                    Tools::Logger::info("Found endDate (number) in top level: " + endDate);
                }
            }

            // If not found in top level, try nested in "arg" object
            if (startDate.empty() && message.contains("arg") && message["arg"].is_object()) {
                if (message["arg"].contains("startDate")) {
                    if (message["arg"]["startDate"].is_string()) {
                        startDate = message["arg"]["startDate"].get<std::string>();
                        Tools::Logger::info("Found startDate in arg: " + startDate);
                    }
                    else if (message["arg"]["startDate"].is_number()) {
                        startDate = std::to_string(message["arg"]["startDate"].get<int>());
                        Tools::Logger::info("Found startDate (number) in arg: " + startDate);
                    }
                }
            }

            if (endDate.empty() && message.contains("arg") && message["arg"].is_object()) {
                if (message["arg"].contains("endDate")) {
                    if (message["arg"]["endDate"].is_string()) {
                        endDate = message["arg"]["endDate"].get<std::string>();
                        Tools::Logger::info("Found endDate in arg: " + endDate);
                    }
                    else if (message["arg"]["endDate"].is_number()) {
                        endDate = std::to_string(message["arg"]["endDate"].get<int>());
                        Tools::Logger::info("Found endDate (number) in arg: " + endDate);
                    }
                }
            }

            // Get instanceId from request for response
            std::string instanceId = "";
            if (message.contains("pluginArg") && message["pluginArg"].is_object()) {
                if (message["pluginArg"].contains("instanceId") && 
                    message["pluginArg"]["instanceId"].is_string()) {
                    instanceId = message["pluginArg"]["instanceId"].get<std::string>();
                }
            }

            // Validate parameters
            if (startDate.empty() || endDate.empty()) {
                Tools::Logger::error("Exchange_rate handleClient: startDate or endDate missing. Message: " + message.dump());
                // Send empty array as response with proper format
                nlohmann::json errorResponse;
                errorResponse["pluginArg"]["name"] = pluginName;
                if (!instanceId.empty()) {
                    errorResponse["pluginArg"]["instanceId"] = instanceId;
                }
                errorResponse["data"] = nlohmann::json::array();
                m_webSocketServer->sendClient(hdl, pluginName, errorResponse);
                return;
            }

            Tools::Logger::info("Filter exchange rate data, startDate: " + startDate + ", endDate: " + endDate);

            // Filter data from cache based on date range
            nlohmann::json filteredData = nlohmann::json::array();
            
            {
                std::lock_guard<std::mutex> lock(m_cacheMutex);
                
                if (m_cachedExchangeRateData.is_array()) {
                    for (const auto& record : m_cachedExchangeRateData) {
                        if (record.is_object() && record.contains("tradeDateKey")) {
                            // Get tradeDateKey value (could be int or string)
                            std::string tradeDateKeyStr;
                            if (record["tradeDateKey"].is_number()) {
                                tradeDateKeyStr = std::to_string(record["tradeDateKey"].get<int>());
                            }
                            else if (record["tradeDateKey"].is_string()) {
                                tradeDateKeyStr = record["tradeDateKey"].get<std::string>();
                            }
                            
                            // Compare dates (assuming format YYYYMMDD)
                            if (!tradeDateKeyStr.empty() && 
                                tradeDateKeyStr >= startDate && 
                                tradeDateKeyStr <= endDate) {
                                filteredData.push_back(record);
                            }
                        }
                    }
                }
            }

            Tools::Logger::info("Filtered " + std::to_string(filteredData.size()) + " records from cache");

            // Log first few filtered records as sample to show response content
            int maxSample = 5;
            int idx = 0;
            if (filteredData.is_array()) {
                for (const auto& rec : filteredData) {
                    if (idx >= maxSample) break;
                    Tools::Logger::info("Filtered sample record [" + std::to_string(idx) + "]: " + rec.dump());
                    ++idx;
                }
            }

            // Build response message with pluginArg for frontend routing
            // Frontend expects: { "pluginArg": { "name": "...", "instanceId": "..." }, "data": [...] }
            nlohmann::json response;
            
            // Build response with pluginArg and data
            response["pluginArg"]["name"] = pluginName;
            if (!instanceId.empty()) {
                response["pluginArg"]["instanceId"] = instanceId;
            }
            response["data"] = filteredData;

            // Log response before sending
            Tools::Logger::info("Sending response with " + std::to_string(filteredData.size()) + " records. Response: " + response.dump().substr(0, 500));

            // Send filtered json data to client
            if (m_webSocketServer) {
                m_webSocketServer->sendClient(hdl, pluginName, response);
                Tools::Logger::info("Response sent successfully");
            }
            else {
                Tools::Logger::error("Cannot send response: WebSocket server is null");
            }
        }
        catch (const std::exception& e) {
            Tools::Logger::error(std::string("Exchange_rate handleClient exception: ") + e.what());
            // Send empty array on error with proper format
            try {
                std::string instanceId = "";
                if (message.contains("pluginArg") && message["pluginArg"].is_object()) {
                    if (message["pluginArg"].contains("instanceId") && 
                        message["pluginArg"]["instanceId"].is_string()) {
                        instanceId = message["pluginArg"]["instanceId"].get<std::string>();
                    }
                }
                nlohmann::json errorResponse;
                errorResponse["pluginArg"]["name"] = pluginName;
                if (!instanceId.empty()) {
                    errorResponse["pluginArg"]["instanceId"] = instanceId;
                }
                errorResponse["data"] = nlohmann::json::array();
                if (m_webSocketServer) {
                    m_webSocketServer->sendClient(hdl, pluginName, errorResponse);
                }
            }
            catch (...) {
                // Ignore send error
            }
        }
        catch (...) {
            Tools::Logger::error("Exchange_rate handleClient unknown exception.");
            // Send empty array on error with proper format
            try {
                std::string instanceId = "";
                if (message.contains("pluginArg") && message["pluginArg"].is_object()) {
                    if (message["pluginArg"].contains("instanceId") && 
                        message["pluginArg"]["instanceId"].is_string()) {
                        instanceId = message["pluginArg"]["instanceId"].get<std::string>();
                    }
                }
                nlohmann::json errorResponse;
                errorResponse["pluginArg"]["name"] = pluginName;
                if (!instanceId.empty()) {
                    errorResponse["pluginArg"]["instanceId"] = instanceId;
                }
                errorResponse["data"] = nlohmann::json::array();
                if (m_webSocketServer) {
                    m_webSocketServer->sendClient(hdl, pluginName, errorResponse);
                }
            }
            catch (...) {
                // Ignore send error
            }
        }
    }
};

extern "C" EXCHANGE_RATE_API PluginInterface* create_plugin() {
    return new Exchange_rate();
}

extern "C" EXCHANGE_RATE_API void destroy_plugin(PluginInterface* p) {
    delete p;
}
