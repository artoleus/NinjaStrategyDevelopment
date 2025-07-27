package main

import (
	"flag"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
)

func main() {
	// Command line flags
	var (
		port   = flag.String("port", "8080", "Server port")
		apiKey = flag.String("api-key", "", "API key for authentication (optional)")
		debug  = flag.Bool("debug", false, "Enable debug logging")
	)
	flag.Parse()

	// Configure logging
	if *debug {
		log.SetFlags(log.LstdFlags | log.Lshortfile)
	}

	// Create robust server
	server := NewRobustServer(*apiKey)
	
	// Start background cleanup routine
	server.startCleanupRoutine()

	// Setup middleware chain
	applyMiddleware := func(handler http.HandlerFunc) http.HandlerFunc {
		return server.panicRecoveryMiddleware(
			server.rateLimitMiddleware(
				server.authenticateRequest(handler),
			),
		)
	}

	// HTTP endpoints for NinjaTrader communication
	http.HandleFunc("/api/ninja-status", applyMiddleware(server.handleNinjaStatusUpdate))
	http.HandleFunc("/api/ninja-commands", applyMiddleware(server.handleNinjaGetCommands))
	http.HandleFunc("/api/ninja-confirmation", applyMiddleware(server.handleNinjaConfirmation))

	// WebSocket endpoint for mobile clients
	http.HandleFunc("/mobile", server.handleMobileConnection)

	// REST API endpoints
	http.HandleFunc("/api/status", applyMiddleware(server.handleStatusEndpoint))
	http.HandleFunc("/api/history", applyMiddleware(server.handleHistoryEndpoint))
	http.HandleFunc("/api/regime-current", applyMiddleware(server.handleRegimeEndpoint)) // Phase 1: Market Regime
	http.HandleFunc("/api/emergency-stop", applyMiddleware(server.handleEmergencyStopEndpoint))
	http.HandleFunc("/api/resume", applyMiddleware(server.handleResumeEndpoint))
	http.HandleFunc("/api/health", server.handleHealthCheck) // No auth required for health

	// Static file serving for web interface
	http.Handle("/", http.FileServer(http.Dir("./static/")))

	// Setup graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)

	go func() {
		<-sigChan
		log.Println("🛑 Received shutdown signal")
		server.Shutdown()
		os.Exit(0)
	}()

	// Start server
	addr := ":" + *port
	log.Printf("🚀 Enhanced Trading Strategy Remote Server starting on port %s", *port)
	log.Printf("🔒 Authentication: %s", func() string {
		if *apiKey != "" {
			return "Enabled (API Key required)"
		}
		return "Disabled"
	}())
	log.Printf("📊 NinjaTrader Status: POST http://localhost%s/api/ninja-status", *port)
	log.Printf("📥 NinjaTrader Commands: GET http://localhost%s/api/ninja-commands", *port)
	log.Printf("✅ NinjaTrader Confirmation: POST http://localhost%s/api/ninja-confirmation", *port)
	log.Printf("📱 Mobile WebSocket: ws://localhost%s/mobile", *port)
	log.Printf("🔥 Emergency Stop: POST http://localhost%s/api/emergency-stop", *port)
	log.Printf("▶️  Resume Strategy: POST http://localhost%s/api/resume", *port)
	log.Printf("📈 Current Status: GET http://localhost%s/api/status", *port)
	log.Printf("🧠 Market Regime: GET http://localhost%s/api/regime-current", *port)
	log.Printf("🏥 Health Check: GET http://localhost%s/api/health", *port)
	log.Printf("🌐 Web Interface: http://localhost%s/", *port)

	log.Fatal(http.ListenAndServe(addr, nil))
}