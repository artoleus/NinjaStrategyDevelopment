package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"runtime/debug"
	"strings"
	"sync"
	"time"

	"github.com/gorilla/websocket"
	"golang.org/x/time/rate"
)

// Configuration constants
const (
	MaxMessageSize     = 1024 * 1024 // 1MB max message size
	WriteTimeout       = 10 * time.Second
	ReadTimeout        = 60 * time.Second
	PingInterval       = 30 * time.Second
	MaxConnections     = 100
	CommandTimeout     = 5 * time.Second
	MaxCommandQueue    = 10
	RateLimitRequests  = 10  // requests per second
	RateLimitBurst     = 20  // burst capacity
)

// AccountStatus represents the data received from NinjaTrader strategy
type AccountStatus struct {
	AccountName        string    `json:"accountName"`
	CurrentPrice       float64   `json:"currentPrice"`
	AccountBalance     float64   `json:"accountBalance"`
	TodaysPnL          float64   `json:"todaysPnL"`
	HasActiveTrade     bool      `json:"hasActiveTrade"`
	ActivePnL          float64   `json:"activePnL"`
	StrategyState      string    `json:"strategyState"`
	Timestamp          time.Time `json:"timestamp"`
	Instrument         string    `json:"instrument"`
	MarketPositionSize int       `json:"marketPositionSize"`
	
	// Market Regime Detection Data (Phase 1)
	MarketRegime       string  `json:"marketRegime,omitempty"`
	RegimeDirection    string  `json:"regimeDirection,omitempty"`
	RegimeConfidence   float64 `json:"regimeConfidence,omitempty"`
	CCI20Value         float64 `json:"cci20Value,omitempty"`
	CCI12Value         float64 `json:"cci12Value,omitempty"`
	CCISmaValue        float64 `json:"cciSmaValue,omitempty"`
	RecentCrossover    bool    `json:"recentCrossover,omitempty"`
	CrossoverDirection string  `json:"crossoverDirection,omitempty"`
}

// Command represents commands sent to/from the strategy
type Command struct {
	Action    string    `json:"action"`
	Timestamp time.Time `json:"timestamp"`
	ID        string    `json:"id,omitempty"`
}

// Client represents a connected mobile client
type Client struct {
	conn         *websocket.Conn
	send         chan []byte
	rateLimiter  *rate.Limiter
	lastPing     time.Time
	isAlive      bool
	mu           sync.RWMutex
}

// RobustServer holds the server state with enhanced error handling
type RobustServer struct {
	// Core components
	upgrader        websocket.Upgrader
	mobileClients   map[*Client]bool
	currentStatus   *AccountStatus
	statusHistory   []AccountStatus
	commandQueue    chan Command
	pendingCommands map[string]Command
	
	// Thread safety
	mu              sync.RWMutex
	clientsMu       sync.RWMutex
	
	// Configuration
	maxHistorySize  int
	apiKey          string
	
	// Health monitoring
	lastNinjaUpdate time.Time
	serverStartTime time.Time
	totalConnections int64
	totalCommands   int64
	
	// Rate limiting
	globalLimiter   *rate.Limiter
	
	// Context for graceful shutdown
	ctx    context.Context
	cancel context.CancelFunc
}

func NewRobustServer(apiKey string) *RobustServer {
	ctx, cancel := context.WithCancel(context.Background())
	
	return &RobustServer{
		upgrader: websocket.Upgrader{
			CheckOrigin: func(r *http.Request) bool {
				// In production, implement proper origin checking
				return true
			},
			ReadBufferSize:  1024,
			WriteBufferSize: 1024,
		},
		mobileClients:   make(map[*Client]bool),
		commandQueue:    make(chan Command, MaxCommandQueue),
		pendingCommands: make(map[string]Command),
		maxHistorySize:  1000,
		apiKey:          apiKey,
		serverStartTime: time.Now(),
		globalLimiter:   rate.NewLimiter(rate.Limit(RateLimitRequests), RateLimitBurst),
		ctx:             ctx,
		cancel:          cancel,
	}
}

// Middleware for authentication
func (s *RobustServer) authenticateRequest(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if s.apiKey != "" {
			providedKey := r.Header.Get("X-API-Key")
			if providedKey != s.apiKey {
				http.Error(w, "Unauthorized", http.StatusUnauthorized)
				return
			}
		}
		next(w, r)
	}
}

// Middleware for rate limiting
func (s *RobustServer) rateLimitMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if !s.globalLimiter.Allow() {
			http.Error(w, "Rate limit exceeded", http.StatusTooManyRequests)
			return
		}
		next(w, r)
	}
}

// Middleware for panic recovery
func (s *RobustServer) panicRecoveryMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		defer func() {
			if err := recover(); err != nil {
				log.Printf("PANIC: %v\nStack trace:\n%s", err, debug.Stack())
				http.Error(w, "Internal server error", http.StatusInternalServerError)
			}
		}()
		next(w, r)
	}
}

// Enhanced HTTP endpoint for NinjaTrader status updates
func (s *RobustServer) handleNinjaStatusUpdate(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	// Read and validate request body
	decoder := json.NewDecoder(r.Body)
	decoder.DisallowUnknownFields()
	
	var status AccountStatus
	if err := decoder.Decode(&status); err != nil {
		log.Printf("Invalid status JSON: %v", err)
		http.Error(w, "Invalid JSON", http.StatusBadRequest)
		return
	}

	// Validate required fields
	if status.AccountName == "" || status.Instrument == "" {
		http.Error(w, "Missing required fields", http.StatusBadRequest)
		return
	}

	// Update server state
	s.mu.Lock()
	s.currentStatus = &status
	s.lastNinjaUpdate = time.Now()
	
	// Add to history with size limit
	s.statusHistory = append(s.statusHistory, status)
	if len(s.statusHistory) > s.maxHistorySize {
		s.statusHistory = s.statusHistory[1:]
	}
	s.mu.Unlock()

	// Broadcast to mobile clients
	s.broadcastToMobileClients(status)

	// Log important events
	if status.HasActiveTrade {
		log.Printf("📊 Active Trade - %s: Position Size: %d, P&L: £%.2f", 
			status.Instrument, status.MarketPositionSize, status.ActivePnL)
	}

	if status.StrategyState == "EMERGENCY_STOPPED" {
		log.Printf("🚨 STRATEGY EMERGENCY STOPPED - Account: %s", status.AccountName)
	}
	
	// Log regime changes (Phase 1)
	if status.RecentCrossover && status.MarketRegime != "" {
		log.Printf("📈 Market Regime: %s - %s (%.1f%% confidence) - CCI Crossover: %s",
			status.MarketRegime, status.RegimeDirection, status.RegimeConfidence*100, status.CrossoverDirection)
	}

	w.WriteHeader(http.StatusOK)
	w.Write([]byte(`{"status":"received"}`))
}

// HTTP endpoint for NinjaTrader to get commands
func (s *RobustServer) handleNinjaGetCommands(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	w.Header().Set("Content-Type", "application/json")

	// Check for pending commands with timeout
	select {
	case cmd := <-s.commandQueue:
		json.NewEncoder(w).Encode(cmd)
		log.Printf("📤 Command sent to NinjaTrader: %s", cmd.Action)
	case <-time.After(1 * time.Second):
		// No commands available
		w.WriteHeader(http.StatusNoContent)
	}
}

// HTTP endpoint for NinjaTrader confirmations
func (s *RobustServer) handleNinjaConfirmation(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var confirmation map[string]interface{}
	if err := json.NewDecoder(r.Body).Decode(&confirmation); err != nil {
		http.Error(w, "Invalid JSON", http.StatusBadRequest)
		return
	}

	log.Printf("✅ Confirmation received from NinjaTrader: %v", confirmation)
	
	// Broadcast confirmation to mobile clients
	confirmationBytes, _ := json.Marshal(confirmation)
	s.broadcastRawToMobileClients(confirmationBytes)

	w.WriteHeader(http.StatusOK)
	w.Write([]byte(`{"status":"confirmed"}`))
}

// Enhanced WebSocket handler for mobile clients
func (s *RobustServer) handleMobileConnection(w http.ResponseWriter, r *http.Request) {
	conn, err := s.upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("Failed to upgrade mobile connection: %v", err)
		return
	}

	// Create client with rate limiting
	client := &Client{
		conn:        conn,
		send:        make(chan []byte, 256),
		rateLimiter: rate.NewLimiter(rate.Limit(5), 10), // 5 req/sec, burst 10
		lastPing:    time.Now(),
		isAlive:     true,
	}

	s.clientsMu.Lock()
	s.mobileClients[client] = true
	clientCount := len(s.mobileClients)
	s.totalConnections++
	s.clientsMu.Unlock()

	log.Printf("📱 Mobile client connected (total: %d)", clientCount)

	// Send current status immediately
	s.mu.RLock()
	if s.currentStatus != nil {
		statusBytes, _ := json.Marshal(s.currentStatus)
		select {
		case client.send <- statusBytes:
		default:
			// Channel full, skip
		}
	}
	s.mu.RUnlock()

	// Start client goroutines
	go s.clientWriter(client)
	go s.clientReader(client)
}

// Client writer goroutine with timeout handling
func (s *RobustServer) clientWriter(client *Client) {
	defer func() {
		if err := recover(); err != nil {
			log.Printf("PANIC in clientWriter: %v", err)
		}
		s.cleanupClient(client)
	}()

	ticker := time.NewTicker(PingInterval)
	defer ticker.Stop()

	for {
		select {
		case message, ok := <-client.send:
			client.conn.SetWriteDeadline(time.Now().Add(WriteTimeout))
			if !ok {
				client.conn.WriteMessage(websocket.CloseMessage, []byte{})
				return
			}

			if err := client.conn.WriteMessage(websocket.TextMessage, message); err != nil {
				log.Printf("Error writing to mobile client: %v", err)
				return
			}

		case <-ticker.C:
			client.conn.SetWriteDeadline(time.Now().Add(WriteTimeout))
			if err := client.conn.WriteMessage(websocket.PingMessage, nil); err != nil {
				return
			}

		case <-s.ctx.Done():
			return
		}
	}
}

// Client reader goroutine with command processing
func (s *RobustServer) clientReader(client *Client) {
	defer func() {
		if err := recover(); err != nil {
			log.Printf("PANIC in clientReader: %v", err)
		}
		s.cleanupClient(client)
	}()

	client.conn.SetReadLimit(MaxMessageSize)
	client.conn.SetReadDeadline(time.Now().Add(ReadTimeout))
	client.conn.SetPongHandler(func(string) error {
		client.mu.Lock()
		client.lastPing = time.Now()
		client.mu.Unlock()
		client.conn.SetReadDeadline(time.Now().Add(ReadTimeout))
		return nil
	})

	for {
		_, message, err := client.conn.ReadMessage()
		if err != nil {
			if websocket.IsUnexpectedCloseError(err, websocket.CloseGoingAway, websocket.CloseAbnormalClosure) {
				log.Printf("Websocket error: %v", err)
			}
			return
		}

		// Rate limit client commands
		if !client.rateLimiter.Allow() {
			log.Printf("Rate limiting mobile client command")
			continue
		}

		// Process command
		var cmd Command
		if err := json.Unmarshal(message, &cmd); err != nil {
			log.Printf("Invalid command JSON from mobile client: %v", err)
			continue
		}

		// Validate command
		if !s.isValidCommand(cmd) {
			log.Printf("Invalid command from mobile client: %v", cmd)
			continue
		}

		s.processClientCommand(cmd)
	}
}

// Validate command structure and content
func (s *RobustServer) isValidCommand(cmd Command) bool {
	// Check required fields
	if cmd.Action == "" {
		return false
	}

	// Validate action values
	validActions := map[string]bool{
		"EMERGENCY_STOP": true,
		"RESUME":         true,
		"GET_STATUS":     true,
	}

	return validActions[strings.ToUpper(cmd.Action)]
}

// Process command from mobile client
func (s *RobustServer) processClientCommand(cmd Command) {
	cmd.Timestamp = time.Now()
	cmd.ID = fmt.Sprintf("cmd_%d", time.Now().UnixNano())
	
	log.Printf("📥 Processing mobile command: %s", cmd.Action)
	s.totalCommands++

	// Add to command queue with timeout
	select {
	case s.commandQueue <- cmd:
		log.Printf("📤 Command queued for NinjaTrader: %s", cmd.Action)
	case <-time.After(CommandTimeout):
		log.Printf("⚠️ Command queue timeout for: %s", cmd.Action)
	}
}

// Clean up disconnected client
func (s *RobustServer) cleanupClient(client *Client) {
	s.clientsMu.Lock()
	if s.mobileClients[client] {
		delete(s.mobileClients, client)
		clientCount := len(s.mobileClients)
		s.clientsMu.Unlock()
		
		close(client.send)
		client.conn.Close()
		
		log.Printf("📱 Mobile client disconnected (remaining: %d)", clientCount)
	} else {
		s.clientsMu.Unlock()
	}
}

// Broadcast status to all mobile clients
func (s *RobustServer) broadcastToMobileClients(status AccountStatus) {
	statusBytes, err := json.Marshal(status)
	if err != nil {
		log.Printf("Error marshaling status: %v", err)
		return
	}
	
	s.broadcastRawToMobileClients(statusBytes)
}

// Broadcast raw message to all mobile clients
func (s *RobustServer) broadcastRawToMobileClients(message []byte) {
	s.clientsMu.RLock()
	clients := make([]*Client, 0, len(s.mobileClients))
	for client := range s.mobileClients {
		clients = append(clients, client)
	}
	s.clientsMu.RUnlock()

	for _, client := range clients {
		select {
		case client.send <- message:
			// Message sent successfully
		default:
			// Channel full, client is slow - disconnect it
			log.Printf("Disconnecting slow mobile client")
			s.cleanupClient(client)
		}
	}
}

// Enhanced status endpoint with more details
func (s *RobustServer) handleStatusEndpoint(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")

	s.mu.RLock()
	status := s.currentStatus
	lastUpdate := s.lastNinjaUpdate
	s.mu.RUnlock()

	response := map[string]interface{}{
		"last_update": lastUpdate,
		"server_uptime": time.Since(s.serverStartTime).String(),
		"status": status,
	}

	if status == nil {
		response["error"] = "No strategy data available"
		w.WriteHeader(http.StatusServiceUnavailable)
	}

	json.NewEncoder(w).Encode(response)
}

// Enhanced health check endpoint
func (s *RobustServer) handleHealthCheck(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")

	s.mu.RLock()
	s.clientsMu.RLock()
	
	ninjaConnected := time.Since(s.lastNinjaUpdate) < 30*time.Second
	mobileClientCount := len(s.mobileClients)
	hasCurrentStatus := s.currentStatus != nil
	
	health := map[string]interface{}{
		"status":              "ok",
		"timestamp":           time.Now(),
		"ninja_connected":     ninjaConnected,
		"mobile_clients":      mobileClientCount,
		"has_current_status":  hasCurrentStatus,
		"server_uptime":       time.Since(s.serverStartTime).String(),
		"total_connections":   s.totalConnections,
		"total_commands":      s.totalCommands,
		"last_ninja_update":   s.lastNinjaUpdate,
		"command_queue_size":  len(s.commandQueue),
	}

	if hasCurrentStatus {
		health["strategy_state"] = s.currentStatus.StrategyState
	}
	
	s.clientsMu.RUnlock()
	s.mu.RUnlock()

	json.NewEncoder(w).Encode(health)
}

// Emergency stop endpoint
func (s *RobustServer) handleEmergencyStopEndpoint(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	cmd := Command{
		Action:    "EMERGENCY_STOP",
		Timestamp: time.Now(),
		ID:        fmt.Sprintf("emergency_%d", time.Now().UnixNano()),
	}

	select {
	case s.commandQueue <- cmd:
		log.Printf("🚨 EMERGENCY STOP command queued")
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]interface{}{
			"success": true,
			"message": "Emergency stop command sent",
			"timestamp": cmd.Timestamp,
		})
	case <-time.After(CommandTimeout):
		log.Printf("⚠️ Emergency stop command timeout")
		http.Error(w, "Command timeout", http.StatusRequestTimeout)
	}
}

// Resume endpoint
func (s *RobustServer) handleResumeEndpoint(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	cmd := Command{
		Action:    "RESUME",
		Timestamp: time.Now(),
		ID:        fmt.Sprintf("resume_%d", time.Now().UnixNano()),
	}

	select {
	case s.commandQueue <- cmd:
		log.Printf("▶️ RESUME command queued")
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]interface{}{
			"success": true,
			"message": "Resume command sent",
			"timestamp": cmd.Timestamp,
		})
	case <-time.After(CommandTimeout):
		log.Printf("⚠️ Resume command timeout")
		http.Error(w, "Command timeout", http.StatusRequestTimeout)
	}
}

// History endpoint with pagination
func (s *RobustServer) handleHistoryEndpoint(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")

	s.mu.RLock()
	history := make([]AccountStatus, len(s.statusHistory))
	copy(history, s.statusHistory)
	s.mu.RUnlock()

	// Basic pagination support
	limit := 100 // Default limit
	if len(history) > limit {
		history = history[len(history)-limit:]
	}

	json.NewEncoder(w).Encode(map[string]interface{}{
		"history": history,
		"count":   len(history),
		"total":   len(s.statusHistory),
	})
}

// Market Regime endpoint (Phase 1)
func (s *RobustServer) handleRegimeEndpoint(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")

	s.mu.RLock()
	status := s.currentStatus
	s.mu.RUnlock()

	if status == nil {
		http.Error(w, "No regime data available", http.StatusServiceUnavailable)
		return
	}

	regimeData := map[string]interface{}{
		"marketRegime":       status.MarketRegime,
		"regimeDirection":    status.RegimeDirection,
		"regimeConfidence":   status.RegimeConfidence,
		"cci20Value":         status.CCI20Value,
		"cci12Value":         status.CCI12Value,
		"cciSmaValue":        status.CCISmaValue,
		"recentCrossover":    status.RecentCrossover,
		"crossoverDirection": status.CrossoverDirection,
		"timestamp":          status.Timestamp,
		"instrument":         status.Instrument,
	}

	json.NewEncoder(w).Encode(regimeData)
}

// Start background cleanup goroutine
func (s *RobustServer) startCleanupRoutine() {
	go func() {
		ticker := time.NewTicker(1 * time.Minute)
		defer ticker.Stop()

		for {
			select {
			case <-ticker.C:
				s.cleanupDeadClients()
			case <-s.ctx.Done():
				return
			}
		}
	}()
}

// Clean up dead/stale clients
func (s *RobustServer) cleanupDeadClients() {
	s.clientsMu.RLock()
	var deadClients []*Client
	
	for client := range s.mobileClients {
		client.mu.RLock()
		if time.Since(client.lastPing) > 2*PingInterval {
			deadClients = append(deadClients, client)
		}
		client.mu.RUnlock()
	}
	s.clientsMu.RUnlock()

	for _, client := range deadClients {
		log.Printf("Cleaning up dead mobile client")
		s.cleanupClient(client)
	}
}

// Graceful shutdown
func (s *RobustServer) Shutdown() {
	log.Println("🛑 Shutting down server...")
	s.cancel()
	
	// Close all client connections
	s.clientsMu.Lock()
	for client := range s.mobileClients {
		client.conn.Close()
	}
	s.clientsMu.Unlock()
	
	log.Println("✅ Server shutdown complete")
}