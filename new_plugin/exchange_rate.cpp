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
            // Expected format: { "startDate": "20251101", "endDate": "20251125" }
            std::string startDate;
            std::string endDate;

            if (message.contains("startDate")) {
                startDate = message["startDate"].get<std::string>();
            }
            if (message.contains("endDate")) {
                endDate = message["endDate"].get<std::string>();
            }

            // Validate parameters
            if (startDate.empty() || endDate.empty()) {
                Tools::Logger::error("Exchange_rate handleClient: startDate or endDate missing.");
                // Send empty array as response
                nlohmann::json emptyData = nlohmann::json::array();
                m_webSocketServer->sendClient(hdl, pluginName, emptyData);
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

            // Send filtered json data to client
            if (m_webSocketServer) {
                m_webSocketServer->sendClient(hdl, pluginName, filteredData);
            }
        }
        catch (const std::exception& e) {
            Tools::Logger::error(std::string("Exchange_rate handleClient exception: ") + e.what());
            // Send empty array on error
            try {
                nlohmann::json emptyData = nlohmann::json::array();
                if (m_webSocketServer) {
                    m_webSocketServer->sendClient(hdl, pluginName, emptyData);
                }
            }
            catch (...) {
                // Ignore send error
            }
        }
        catch (...) {
            Tools::Logger::error("Exchange_rate handleClient unknown exception.");
            // Send empty array on error
            try {
                nlohmann::json emptyData = nlohmann::json::array();
                if (m_webSocketServer) {
                    m_webSocketServer->sendClient(hdl, pluginName, emptyData);
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
