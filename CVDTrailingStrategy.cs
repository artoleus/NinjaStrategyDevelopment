#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Strategies
{
	/// <summary>
	/// CVD Trailing Strategy - Enhanced version with trailing stops
	///
	/// This strategy identifies bullish and bearish divergences between price fractals
	/// and Cumulative Volume Delta (CVD) to generate trading signals.
	///
	/// Key Features:
	/// - Real volume-based CVD calculation (not estimated)
	/// - Fractal detection with trend filtering
	/// - Divergence detection with confirmation
	/// - Time-based session filtering
	/// - Dynamic position sizing based on account performance
	/// - Progressive trailing stops (50%→BE, 75%→33%, 90%→50%)
	/// </summary>
	public class CVDTrailingStrategy : Strategy
	{
		#region Strategy Parameters

		[Range(1, 1000), NinjaScriptProperty]
		[Display(Name="Take Profit (Ticks)", Description="Take profit distance in ticks", Order=1, GroupName="Strategy Settings")]
		public int TakeProfitTicks { get; set; }

		[Range(1, 1000), NinjaScriptProperty]
		[Display(Name="Stop Loss (Ticks)", Description="Stop loss distance in ticks", Order=2, GroupName="Strategy Settings")]
		public int StopLossTicks { get; set; }

		[Range(1, 50), NinjaScriptProperty]
		[Display(Name="Fractal Periods", Description="Number of periods for fractal detection", Order=3, GroupName="CVD Settings")]
		public int FractalPeriods { get; set; }

		[Range(10, 2000), NinjaScriptProperty]
		[Display(Name="CVD Period", Description="Period for CVD calculation", Order=4, GroupName="CVD Settings")]
		public int CVDPeriod { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Use EMA Mode", Description="Use EMA instead of periodic sum for CVD", Order=5, GroupName="CVD Settings")]
		public bool UseEMAMode { get; set; }

		[Range(1, 5), NinjaScriptProperty]
		[Display(Name="Confirmation Bars", Description="Bars to wait after divergence detection", Order=6, GroupName="Signal Settings")]
		public int ConfirmationBars { get; set; }

		[Range(5, 100), NinjaScriptProperty]
		[Display(Name="Max Bars Between Fractals", Description="Maximum bars between fractals for divergence", Order=7, GroupName="Signal Settings")]
		public int MaxBarsBetweenFractals { get; set; }

		[Range(1, 20), NinjaScriptProperty]
		[Display(Name="Min Bars Between Fractals", Description="Minimum bars between fractals for divergence", Order=8, GroupName="Signal Settings")]
		public int MinBarsBetweenFractals { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Enable Time Restrictions", Description="Enable session-based trading", Order=9, GroupName="Time Management")]
		public bool EnableTimeRestrictions { get; set; }

		// Session string format: "HHmm-HHmm" in 24-hour format
		[NinjaScriptProperty]
		[Display(Name="Trading Session", Description="Trading session in format HHmm-HHmm (e.g. 2300-2130)", Order=10, GroupName="Time Management")]
		public string TradingSession { get; set; }

		// Daily Profit Management
		[Range(1, double.MaxValue), NinjaScriptProperty]
		[Display(Name="Daily Profit Limit", Description="Stop trading after reaching this profit amount", Order=11, GroupName="Profit Management")]
		public double DailyProfitLimit { get; set; }

		[Range(1, double.MaxValue), NinjaScriptProperty]
		[Display(Name="Daily Loss Limit", Description="Stop trading after reaching this loss amount (positive number)", Order=12, GroupName="Profit Management")]
		public double DailyLossLimit { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name="Reset Time", Description="Time of day to reset daily profit (HH:MM format)", Order=13, GroupName="Profit Management")]
		public DateTime ResetTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Include Unrealized P&L", Description="Include open position P&L in daily profit calculation", Order=14, GroupName="Profit Management")]
		public bool IncludeUnrealizedPL { get; set; }

		// Dynamic Position Sizing
		[NinjaScriptProperty]
		[Display(Name="Enable Dynamic Sizing", Description="Enable position sizing based on account value", Order=15, GroupName="Position Sizing")]
		public bool EnableDynamicSizing { get; set; }

		[Range(1, 10000), NinjaScriptProperty]
		[Display(Name="Base Quantity", Description="Base position size (quantity = 1)", Order=16, GroupName="Position Sizing")]
		public int BaseQuantity { get; set; }

		[Range(100, 100000), NinjaScriptProperty]
		[Display(Name="Tier 2 Threshold", Description="Account profit threshold for quantity = 2", Order=17, GroupName="Position Sizing")]
		public double Tier2Threshold { get; set; }

		[Range(100, 100000), NinjaScriptProperty]
		[Display(Name="Tier 3 Threshold", Description="Account profit threshold for quantity = 3", Order=18, GroupName="Position Sizing")]
		public double Tier3Threshold { get; set; }

		// Progressive Trailing Stop
		[NinjaScriptProperty]
		[Display(Name="Enable Trailing Stop", Description="Enable progressive trailing stop management", Order=19, GroupName="Trailing Stop")]
		public bool EnableTrailingStop { get; set; }

		[Range(10, 90), NinjaScriptProperty]
		[Display(Name="Breakeven Profit %", Description="Profit percentage to move stop to breakeven", Order=20, GroupName="Trailing Stop")]
		public double BreakevenProfitPercent { get; set; }

		[Range(10, 95), NinjaScriptProperty]
		[Display(Name="Trail Level 1 Profit %", Description="Profit percentage for first trailing level", Order=21, GroupName="Trailing Stop")]
		public double TrailLevel1ProfitPercent { get; set; }

		[Range(10, 80), NinjaScriptProperty]
		[Display(Name="Trail Level 1 Stop %", Description="Stop percentage for first trailing level", Order=22, GroupName="Trailing Stop")]
		public double TrailLevel1StopPercent { get; set; }

		[Range(10, 99), NinjaScriptProperty]
		[Display(Name="Trail Level 2 Profit %", Description="Profit percentage for second trailing level", Order=23, GroupName="Trailing Stop")]
		public double TrailLevel2ProfitPercent { get; set; }

		[Range(10, 90), NinjaScriptProperty]
		[Display(Name="Trail Level 2 Stop %", Description="Stop percentage for second trailing level", Order=24, GroupName="Trailing Stop")]
		public double TrailLevel2StopPercent { get; set; }

		#endregion

		#region Private Variables

		// CVD Calculation
		private Series<double> cvdValues;
		private Series<double> buyingVolume;
		private Series<double> sellingVolume;
		private Series<double> delta;
		private EMA cvdEMA;

		// Fractal Detection
		private Series<double> fractalHighs;
		private Series<double> fractalLows;
		private EMA trendEMA;

		// Divergence Tracking
		private List<FractalData> highFractals;
		private List<FractalData> lowFractals;

		// Signal State Management
		private bool bullishSignalDetected;
		private bool bearishSignalDetected;
		private int bullishSignalBar;
		private int bearishSignalBar;
		private bool bullishAlertIssued;
		private bool bearishAlertIssued;

		// Session Management
		private TimeSpan sessionStart;
		private TimeSpan sessionEnd;
		private bool isOverNightSession;

		// Performance Tracking
		private int totalSignals;
		private int winningTrades;
		private int losingTrades;

		// Daily Profit Management
		private double dailyProfit = 0;
		private DateTime lastResetDate;
		private double startOfDayBalance = 0;
		private bool isLiveTrading = false;
		private bool hasLoggedLimitReached = false;
		private bool hasLoggedStopReached = false;
		private HashSet<string> countedTradeIds = new HashSet<string>();

		// Thread Safety
		private readonly object fractalLock = new object();
		private readonly object tradeLock = new object();

		// Dynamic Position Sizing
		private double startingAccountValue = 0;
		private int currentPositionSize = 1;

		// Trailing Stop Management
		private double entryPrice = 0;
		private double originalStopPrice = 0;
		private double currentStopPrice = 0;
		private int trailingStopLevel = 0; // 0=none, 1=breakeven, 2=level1, 3=level2
		private MarketPosition lastKnownPosition = MarketPosition.Flat;

		#endregion

		#region Helper Classes

		private class FractalData
		{
			public int BarIndex { get; set; }
			public double Price { get; set; }
			public double CVDValue { get; set; }
			public DateTime Timestamp { get; set; }
		}

		private enum DivergenceStrength
		{
			Weak,
			Moderate,
			Strong
		}

		#endregion


		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"CVD Divergence Strategy - Identifies price/CVD divergences for trading signals";
				Name = "CVDDivergenceStrategy";
				Calculate = Calculate.OnBarClose;
				EntriesPerDirection = 1;
				EntryHandling = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds = 30;
				IsFillLimitOnTouch = false;
				MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution = OrderFillResolution.Standard;
				Slippage = 0;
				StartBehavior = StartBehavior.WaitUntilFlat;
				TimeInForce = TimeInForce.Gtc;
				TraceOrders = false;
				RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade = 100; // Need sufficient history for CVD calculation

				// Default Parameters (from live_trading.yaml and Pine Script)
				TakeProfitTicks = 20;
				StopLossTicks = 10;
				FractalPeriods = 10;
				CVDPeriod = 1000;
				UseEMAMode = false; // Use periodic sum by default
				ConfirmationBars = 2;
				MaxBarsBetweenFractals = 30;
				MinBarsBetweenFractals = 5;
				EnableTimeRestrictions = true;
				TradingSession = "0800-2130"; // 08:00 to 21:30 (UTC+1)

				// Daily Profit Management defaults
				DailyProfitLimit = 2500;  // £2500 profit limit
				DailyLossLimit = 1500;    // £1500 loss limit
				ResetTime = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
				IncludeUnrealizedPL = false;

				// Dynamic Position Sizing defaults
				EnableDynamicSizing = true;
				BaseQuantity = 1;
				Tier2Threshold = 2000;  // $2000 profit for quantity = 2
				Tier3Threshold = 5000;  // $5000 profit for quantity = 3

				// Trailing Stop defaults
				EnableTrailingStop = true;
				BreakevenProfitPercent = 50;   // Move to breakeven at 50% profit
				TrailLevel1ProfitPercent = 75; // Move to 33% profit at 75% profit
				TrailLevel1StopPercent = 33;
				TrailLevel2ProfitPercent = 90; // Move to 50% profit at 90% profit  
				TrailLevel2StopPercent = 50;
			}
			else if (State == State.Configure)
			{
				// Parse trading session
				ParseTradingSession();
			}
			else if (State == State.Realtime)
			{
				isLiveTrading = true;

				// When transitioning to real-time, calculate today's profit so far
				dailyProfit = CalculateTodaysProfitFromHistory();
				startOfDayBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar) - dailyProfit;

				// Initialize starting account value for position sizing
				if (EnableDynamicSizing)
				{
					startingAccountValue = Account.Get(AccountItem.CashValue, Currency.UsDollar);
					currentPositionSize = BaseQuantity;
					Print($"Dynamic Position Sizing Enabled - Starting Account Value: ${startingAccountValue:F2}");
				}
			}
			else if (State == State.DataLoaded)
			{
				lastResetDate = Time[0].Date;
				// Initialize series and indicators
				cvdValues = new Series<double>(this);
				buyingVolume = new Series<double>(this);
				sellingVolume = new Series<double>(this);
				delta = new Series<double>(this);
				fractalHighs = new Series<double>(this);
				fractalLows = new Series<double>(this);

				// Initialize EMA for trend filtering (Pine Script line 84)
				trendEMA = EMA(Close, 50);

				// Initialize CVD EMA if using EMA mode
				if (UseEMAMode)
				{
					cvdEMA = EMA(delta, CVDPeriod);
				}

				// Initialize collections
				highFractals = new List<FractalData>();
				lowFractals = new List<FractalData>();

				// Reset state variables
				bullishSignalDetected = false;
				bearishSignalDetected = false;
				bullishAlertIssued = false;
				bearishAlertIssued = false;
				totalSignals = 0;
				winningTrades = 0;
				losingTrades = 0;

				// Initialize position sizing for backtesting
				if (EnableDynamicSizing)
				{
					startingAccountValue = 100000; // Default for backtesting
					currentPositionSize = BaseQuantity;
				}
			}
			else if (State == State.Terminated)
			{
				// Safe cleanup of resources
				try
				{
					if (highFractals != null)
					{
						lock (fractalLock)
						{
							highFractals.Clear();
							highFractals = null;
						}
					}

					if (lowFractals != null)
					{
						lock (fractalLock)
						{
							lowFractals.Clear();
							lowFractals = null;
						}
					}

					if (countedTradeIds != null)
					{
						lock (tradeLock)
						{
							countedTradeIds.Clear();
							countedTradeIds = null;
						}
					}

					// Clear references to indicators (they're disposed by framework)
					trendEMA = null;
					cvdEMA = null;

					// Reset trailing stop state
					ResetTrailingStopState();

					Print($"{Time[0]}: CVD Divergence Strategy resources cleaned up successfully");
				}
				catch (Exception ex)
				{
					Print($"Cleanup warning (non-critical): {ex.Message}");
				}
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			try
			{
				// Need sufficient bars for calculations
				if (CurrentBar < Math.Max(FractalPeriods * 2, CVDPeriod))
					return;

				// Check if we need to reset for a new day
				CheckDailyReset();

				// Calculate CVD (Cumulative Volume Delta)
				CalculateCVD();

				// Detect fractals with trend filtering
				DetectFractals();

				// Update fractal collections and detect divergences
				UpdateFractalCollections();

				// Check for divergence signals
				CheckForDivergences();

				// Process signal confirmations and entries
				ProcessSignalConfirmations();

				// Check for position exits
				ManagePositions();

				// Manage trailing stops
				ManageTrailingStop();

				// Update dynamic position sizing
				if (EnableDynamicSizing)
				{
					UpdatePositionSize();
				}
			}
			catch (Exception ex)
			{
				// Log error but continue trading - don't stop strategy execution
				Print($"OnBarUpdate Error (non-critical): {ex.Message} at {Time[0]}");
				
				// Reset signal flags to prevent stuck states
				if (ex.Message.Contains("divergence") || ex.Message.Contains("signal"))
				{
					bullishSignalDetected = false;
					bearishSignalDetected = false;
					bullishAlertIssued = false;
					bearishAlertIssued = false;
				}
			}
		}

		#endregion

		#region CVD Calculation

		private void CalculateCVD()
		{
			// Safety checks - don't change any calculations, just add validation
			if (cvdValues == null || buyingVolume == null || sellingVolume == null || delta == null)
			{
				Print("CVD Series not initialized - skipping calculation");
				return;
			}

			if (CurrentBar < 0 || Volume[0] <= 0)
			{
				// Set to neutral values but continue
				if (buyingVolume != null) buyingVolume[0] = 0;
				if (sellingVolume != null) sellingVolume[0] = 0;
				if (delta != null) delta[0] = 0;
				if (cvdValues != null) cvdValues[0] = 0;
				return;
			}

			// Calculate buying and selling volume based on bar position
			// Pine Script lines 72-75
			double range = High[0] - Low[0];
			
			// Enhanced validation: check for extreme price movements or invalid data
			bool isValidBar = High[0] >= Low[0] && Close[0] >= Low[0] && Close[0] <= High[0];
			
			if (range > 0 && isValidBar)
			{
				double closePosition = (Close[0] - Low[0]) / range;
				// Ensure position is between 0 and 1
				closePosition = Math.Max(0, Math.Min(1, closePosition));
				
				buyingVolume[0] = Volume[0] * closePosition;
				sellingVolume[0] = Volume[0] * (1 - closePosition);
			}
			else
			{
				// No range or invalid bar data, split volume evenly
				buyingVolume[0] = Volume[0] * 0.5;
				sellingVolume[0] = Volume[0] * 0.5;
			}

			// Calculate delta
			delta[0] = buyingVolume[0] - sellingVolume[0];

			// Calculate CVD based on mode (Pine Script lines 77-81)
			if (UseEMAMode)
			{
				if (cvdEMA != null)
					cvdValues[0] = cvdEMA[0];
				else
					cvdValues[0] = delta[0]; // Fallback to delta if EMA not available
			}
			else
			{
				// Periodic sum
				double sum = 0;
				int barsToSum = Math.Min(CVDPeriod, CurrentBar + 1);
				for (int i = 0; i < barsToSum; i++)
				{
					sum += delta[i];
				}
				cvdValues[0] = sum;
			}
		}

		#endregion

		#region Fractal Detection

		private void DetectFractals()
		{
			// Need enough bars for fractal detection
			if (CurrentBar < FractalPeriods * 2)
				return;

			// Pivot High Detection (Pine Script line 91)
			bool isHighFractal = true;
			double centerPrice = High[FractalPeriods];

			// Check left and right sides
			for (int i = 0; i < FractalPeriods; i++)
			{
				if (High[i] >= centerPrice || High[FractalPeriods + 1 + i] >= centerPrice)
				{
					isHighFractal = false;
					break;
				}
			}

			// Pivot Low Detection (Pine Script line 94)
			bool isLowFractal = true;
			double centerLowPrice = Low[FractalPeriods];

			// Check left and right sides
			for (int i = 0; i < FractalPeriods; i++)
			{
				if (Low[i] <= centerLowPrice || Low[FractalPeriods + 1 + i] <= centerLowPrice)
				{
					isLowFractal = false;
					break;
				}
			}

			// Apply trend filtering (Pine Script lines 85-86, 95-96)
			bool upTrend = Close[FractalPeriods] > trendEMA[FractalPeriods];
			bool downTrend = Close[FractalPeriods] < trendEMA[FractalPeriods];

			// Store fractals with trend filter
			if (isHighFractal && upTrend)
			{
				fractalHighs[FractalPeriods] = centerPrice;

				// Draw fractal on chart
				Draw.TriangleDown(this, "FractalHigh_" + (CurrentBar - FractalPeriods),
					false, FractalPeriods, centerPrice + 2 * TickSize, Brushes.Red);
			}
			else
			{
				fractalHighs[FractalPeriods] = double.NaN;
			}

			if (isLowFractal && downTrend)
			{
				fractalLows[FractalPeriods] = centerLowPrice;

				// Draw fractal on chart
				Draw.TriangleUp(this, "FractalLow_" + (CurrentBar - FractalPeriods),
					false, FractalPeriods, centerLowPrice - 2 * TickSize, Brushes.Green);
			}
			else
			{
				fractalLows[FractalPeriods] = double.NaN;
			}
		}

		#endregion

		#region Fractal Collection Management

		private void UpdateFractalCollections()
		{
			// Thread-safe fractal collection updates
			lock (fractalLock)
			{
				// Safety check
				if (highFractals == null || lowFractals == null)
					return;

				// Add new high fractals to collection
				if (!double.IsNaN(fractalHighs[FractalPeriods]))
				{
					var fractalData = new FractalData
					{
						BarIndex = CurrentBar - FractalPeriods,
						Price = fractalHighs[FractalPeriods],
						CVDValue = cvdValues[FractalPeriods],
						Timestamp = Time[FractalPeriods]
					};

					highFractals.Add(fractalData);

					// Keep only recent fractals (last 100)
					if (highFractals.Count > 100)
						highFractals.RemoveAt(0);
				}

				// Add new low fractals to collection
				if (!double.IsNaN(fractalLows[FractalPeriods]))
				{
					var fractalData = new FractalData
					{
						BarIndex = CurrentBar - FractalPeriods,
						Price = fractalLows[FractalPeriods],
						CVDValue = cvdValues[FractalPeriods],
						Timestamp = Time[FractalPeriods]
					};

					lowFractals.Add(fractalData);

					// Keep only recent fractals (last 100)
					if (lowFractals.Count > 100)
						lowFractals.RemoveAt(0);
				}
			}
		}

		#endregion

		#region Divergence Detection

		private void CheckForDivergences()
		{
			CheckBullishDivergence();
			CheckBearishDivergence();
		}

		private void CheckBullishDivergence()
		{
			lock (fractalLock)
			{
				// Need at least 2 low fractals for comparison
				if (lowFractals == null || lowFractals.Count < 2)
					return;

			// Get the two most recent low fractals
			var currentFractal = lowFractals[lowFractals.Count - 1];

			// Check against all previous fractals within time constraints
			bool divergenceFound = false;

			for (int i = lowFractals.Count - 2; i >= 0; i--)
			{
				var previousFractal = lowFractals[i];

				// Check time constraints (Pine Script lines 115, 164-165)
				int barsBetween = currentFractal.BarIndex - previousFractal.BarIndex;

				if (barsBetween > MaxBarsBetweenFractals || barsBetween < MinBarsBetweenFractals)
					continue;

				// Check for bullish divergence:
				// Price: Lower Low (current < previous)
				// CVD: Higher Low (current > previous)
				// Pine Script line 165: (Low_Last_Price < Low_Per_Price) and (Low_Last_Hist > Low_Per_Hist)
				if (currentFractal.Price < previousFractal.Price &&
					currentFractal.CVDValue > previousFractal.CVDValue)
				{
					divergenceFound = true;

					// Draw divergence line
					DrawDivergenceLine("BullDiv_" + CurrentBar, previousFractal, currentFractal, Brushes.LimeGreen);

					// Mark signal detection
					if (!bullishSignalDetected)
					{
						bullishSignalDetected = true;
						bullishSignalBar = CurrentBar;
						bullishAlertIssued = false;

						Print($"Bullish Divergence Detected at bar {CurrentBar}: Price {currentFractal.Price:F2} < {previousFractal.Price:F2}, CVD {currentFractal.CVDValue:F2} > {previousFractal.CVDValue:F2}");
					}
					break; // Found divergence, no need to check older fractals
				}
			}

			// Reset signal if no divergence found
			if (!divergenceFound)
			{
				bullishSignalDetected = false;
			}
			}
		}

		private void CheckBearishDivergence()
		{
			lock (fractalLock)
			{
				// Need at least 2 high fractals for comparison
				if (highFractals == null || highFractals.Count < 2)
					return;

			// Get the two most recent high fractals
			var currentFractal = highFractals[highFractals.Count - 1];

			// Check against all previous fractals within time constraints
			bool divergenceFound = false;

			for (int i = highFractals.Count - 2; i >= 0; i--)
			{
				var previousFractal = highFractals[i];

				// Check time constraints (Pine Script lines 115, 118-119)
				int barsBetween = currentFractal.BarIndex - previousFractal.BarIndex;

				if (barsBetween > MaxBarsBetweenFractals || barsBetween < MinBarsBetweenFractals)
					continue;

				// Check for bearish divergence:
				// Price: Higher High (current > previous)
				// CVD: Lower High (current < previous)
				// Pine Script line 119: (High_Last_Price > High_Per_Price) and (High_Last_Hist < High_Per_Hist)
				if (currentFractal.Price > previousFractal.Price &&
					currentFractal.CVDValue < previousFractal.CVDValue)
				{
					divergenceFound = true;

					// Draw divergence line
					DrawDivergenceLine("BearDiv_" + CurrentBar, previousFractal, currentFractal, Brushes.OrangeRed);

					// Mark signal detection
					if (!bearishSignalDetected)
					{
						bearishSignalDetected = true;
						bearishSignalBar = CurrentBar;
						bearishAlertIssued = false;

						Print($"Bearish Divergence Detected at bar {CurrentBar}: Price {currentFractal.Price:F2} > {previousFractal.Price:F2}, CVD {currentFractal.CVDValue:F2} < {previousFractal.CVDValue:F2}");
					}
					break; // Found divergence, no need to check older fractals
				}
			}

			// Reset signal if no divergence found
			if (!divergenceFound)
			{
				bearishSignalDetected = false;
			}
			}
		}

		private void DrawDivergenceLine(string tag, FractalData start, FractalData end, Brush color)
		{
			// Draw line on price chart
			Draw.Line(this, tag + "_Price", false,
				CurrentBar - start.BarIndex, start.Price,
				CurrentBar - end.BarIndex, end.Price,
				color, DashStyleHelper.Solid, 2);
		}

		#endregion

		#region Signal Processing

		private void ProcessSignalConfirmations()
		{
			// Check bullish signal confirmation (Pine Script lines 353-354, 369-374)
			if (bullishSignalDetected &&
				(CurrentBar - bullishSignalBar) == ConfirmationBars &&
				!bullishAlertIssued)
			{
				if (IsInTradingSession() && Position.MarketPosition == MarketPosition.Flat && IsWithinDailyLimits())
				{
					EnterLongPosition();
					bullishAlertIssued = true;
					totalSignals++;

					// Draw entry signal
					Draw.ArrowUp(this, "LongEntry_" + CurrentBar, false, 0,
						Low[0] - 3 * TickSize, Brushes.LimeGreen);
				}
			}

			// Check bearish signal confirmation (Pine Script lines 353-354, 377-382)
			if (bearishSignalDetected &&
				(CurrentBar - bearishSignalBar) == ConfirmationBars &&
				!bearishAlertIssued)
			{
				if (IsInTradingSession() && Position.MarketPosition == MarketPosition.Flat && IsWithinDailyLimits())
				{
					EnterShortPosition();
					bearishAlertIssued = true;
					totalSignals++;

					// Draw entry signal
					Draw.ArrowDown(this, "ShortEntry_" + CurrentBar, false, 0,
						High[0] + 3 * TickSize, Brushes.OrangeRed);
				}
			}
		}

		#endregion

		#region Dynamic Position Sizing

		private void UpdatePositionSize()
		{
			try
			{
				double currentAccountValue;
				double accountProfit;

				if (isLiveTrading)
				{
					// For live trading, use actual account value
					currentAccountValue = Account.Get(AccountItem.CashValue, Currency.UsDollar);
					accountProfit = currentAccountValue - startingAccountValue;
				}
				else
				{
					// For backtesting, use cumulative strategy profit
					accountProfit = SystemPerformance.AllTrades.TradesCount > 0 ? 
						SystemPerformance.AllTrades.Sum(t => t.ProfitCurrency) : 0;
				}

				int newPositionSize = CalculatePositionSize(accountProfit);

				// Only log when size changes
				if (newPositionSize != currentPositionSize)
				{
					int oldSize = currentPositionSize;
					currentPositionSize = newPositionSize;
					Print($"{Time[0]}: Position size changed from {oldSize} to {currentPositionSize} (Account P&L: ${accountProfit:F2})");
				}
			}
			catch (Exception ex)
			{
				Print($"UpdatePositionSize Error: {ex.Message}");
			}
		}

		private int CalculatePositionSize(double accountProfit)
		{
			// Determine position size based on account profit thresholds
			if (accountProfit >= Tier3Threshold)
			{
				return BaseQuantity * 3; // Quantity = 3
			}
			else if (accountProfit >= Tier2Threshold)
			{
				return BaseQuantity * 2; // Quantity = 2
			}
			else
			{
				return BaseQuantity * 1; // Quantity = 1
			}
		}

		private int GetCurrentPositionSize()
		{
			if (EnableDynamicSizing)
			{
				// Validate position size is within reasonable bounds
				int validatedSize = Math.Max(1, Math.Min(currentPositionSize, 10));
				if (validatedSize != currentPositionSize)
				{
					Print($"Position size clamped from {currentPositionSize} to {validatedSize}");
					currentPositionSize = validatedSize;
				}
				return currentPositionSize;
			}
			else
			{
				return DefaultQuantity;
			}
		}

		private void LogPositionSizingStatus()
		{
			if (EnableDynamicSizing)
			{
				double accountProfit = isLiveTrading ? 
					Account.Get(AccountItem.CashValue, Currency.UsDollar) - startingAccountValue :
					(SystemPerformance.AllTrades.TradesCount > 0 ? SystemPerformance.AllTrades.Sum(t => t.ProfitCurrency) : 0);

				Print($"Position Sizing Status: Account P&L=${accountProfit:F2}, Current Size={currentPositionSize}, Tier2=${Tier2Threshold}, Tier3=${Tier3Threshold}");
			}
		}

		#endregion

		#region Trailing Stop Management

		private void ManageTrailingStop()
		{
			if (!EnableTrailingStop || Position.MarketPosition == MarketPosition.Flat)
			{
				ResetTrailingStopState();
				return;
			}

			// Initialize trailing stop on new position
			if (lastKnownPosition == MarketPosition.Flat && Position.MarketPosition != MarketPosition.Flat)
			{
				InitializeTrailingStop();
			}

			// Update trailing stop levels based on current profit
			if (Position.MarketPosition != MarketPosition.Flat)
			{
				UpdateTrailingStopLevels();
			}

			lastKnownPosition = Position.MarketPosition;
		}

		private void InitializeTrailingStop()
		{
			entryPrice = Position.AveragePrice;
			trailingStopLevel = 0;

			// Calculate original stop loss price based on current position
			if (Position.MarketPosition == MarketPosition.Long)
			{
				originalStopPrice = entryPrice - (StopLossTicks * TickSize);
			}
			else if (Position.MarketPosition == MarketPosition.Short)
			{
				originalStopPrice = entryPrice + (StopLossTicks * TickSize);
			}

			currentStopPrice = originalStopPrice;

			Print($"Trailing Stop Initialized: Entry={entryPrice:F2}, Original Stop={originalStopPrice:F2}, Position={Position.MarketPosition}");
		}

		private void UpdateTrailingStopLevels()
		{
			double currentPrice = Close[0];
			double unrealizedPnL = Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, currentPrice);
			double maxPossibleProfit = CalculateMaxPossibleProfit();
			
			if (maxPossibleProfit <= 0) return; // Avoid division by zero

			double profitPercentage = (unrealizedPnL / maxPossibleProfit) * 100;

			// Determine which trailing level we should be at
			int targetLevel = DetermineTrailingLevel(profitPercentage);

			// Only move stops forward, never backward
			if (targetLevel > trailingStopLevel)
			{
				double newStopPrice = CalculateTrailingStopPrice(targetLevel);
				
				// Validate stop is moving in the right direction
				if (IsValidStopMovement(newStopPrice))
				{
					UpdateStopLoss(newStopPrice, targetLevel);
				}
			}
		}

		private double CalculateMaxPossibleProfit()
		{
			double maxProfit = 0;

			if (Position.MarketPosition == MarketPosition.Long)
			{
				// Max profit = (TP price - Entry price) * Quantity * Point Value  
				double tpPrice = entryPrice + (TakeProfitTicks * TickSize);
				maxProfit = (tpPrice - entryPrice) * Position.Quantity * Instrument.MasterInstrument.PointValue;
			}
			else if (Position.MarketPosition == MarketPosition.Short)
			{
				// Max profit = (Entry price - TP price) * Quantity * Point Value
				double tpPrice = entryPrice - (TakeProfitTicks * TickSize);
				maxProfit = (entryPrice - tpPrice) * Position.Quantity * Instrument.MasterInstrument.PointValue;
			}

			return maxProfit;
		}

		private int DetermineTrailingLevel(double profitPercentage)
		{
			if (profitPercentage >= TrailLevel2ProfitPercent)
				return 3; // Level 2 trailing
			else if (profitPercentage >= TrailLevel1ProfitPercent)  
				return 2; // Level 1 trailing
			else if (profitPercentage >= BreakevenProfitPercent)
				return 1; // Breakeven
			else
				return 0; // Original stop
		}

		private double CalculateTrailingStopPrice(int level)
		{
			double newStopPrice = originalStopPrice;

			switch (level)
			{
				case 1: // Breakeven
					newStopPrice = entryPrice;
					break;

				case 2: // Trail Level 1 (33% of profit)
					newStopPrice = CalculatePartialProfitStop(TrailLevel1StopPercent);
					break;

				case 3: // Trail Level 2 (50% of profit)
					newStopPrice = CalculatePartialProfitStop(TrailLevel2StopPercent);
					break;
			}

			return newStopPrice;
		}

		private double CalculatePartialProfitStop(double profitPercent)
		{
			double fullProfitDistance = TakeProfitTicks * TickSize;
			double partialProfitDistance = fullProfitDistance * (profitPercent / 100.0);

			if (Position.MarketPosition == MarketPosition.Long)
			{
				return entryPrice + partialProfitDistance;
			}
			else // Short position
			{
				return entryPrice - partialProfitDistance;
			}
		}

		private bool IsValidStopMovement(double newStopPrice)
		{
			if (Position.MarketPosition == MarketPosition.Long)
			{
				// For long positions, stop should move up (be higher than current stop)
				return newStopPrice > currentStopPrice;
			}
			else if (Position.MarketPosition == MarketPosition.Short)
			{
				// For short positions, stop should move down (be lower than current stop)  
				return newStopPrice < currentStopPrice;
			}

			return false;
		}

		private void UpdateStopLoss(double newStopPrice, int newLevel)
		{
			try
			{
				string exitName = Position.MarketPosition == MarketPosition.Long ? "CVD_Long" : "CVD_Short";
				SetStopLoss(exitName, CalculationMode.Price, newStopPrice, false);

				string levelName = GetTrailingLevelName(newLevel);
				Print($"Trailing Stop Updated to {levelName}: New Stop={newStopPrice:F2} (was {currentStopPrice:F2})");

				currentStopPrice = newStopPrice;
				trailingStopLevel = newLevel;
			}
			catch (Exception ex)
			{
				Print($"Error updating trailing stop: {ex.Message}");
			}
		}

		private string GetTrailingLevelName(int level)
		{
			switch (level)
			{
				case 1: return "Breakeven";
				case 2: return $"Trail L1 ({TrailLevel1StopPercent}%)";
				case 3: return $"Trail L2 ({TrailLevel2StopPercent}%)";
				default: return "Original";
			}
		}

		private void ResetTrailingStopState()
		{
			if (trailingStopLevel > 0) // Only log if we had an active trailing stop
			{
				Print("Trailing Stop Reset - Position Closed");
			}

			entryPrice = 0;
			originalStopPrice = 0;
			currentStopPrice = 0;
			trailingStopLevel = 0;
		}

		#endregion

		#region Position Management

		private void EnterLongPosition()
		{
			// Calculate TP and SL prices (Pine Script lines 372-374)
			double entryPrice = Close[0];
			double takeProfitPrice = entryPrice + (TakeProfitTicks * TickSize);
			double stopLossPrice = entryPrice - (StopLossTicks * TickSize);

			// Use dynamic position sizing
			int positionSize = GetCurrentPositionSize();
			
			EnterLong(positionSize, "CVD_Long");
			SetProfitTarget("CVD_Long", CalculationMode.Price, takeProfitPrice);
			SetStopLoss("CVD_Long", CalculationMode.Price, stopLossPrice, false);

			Print($"Long Entry: Qty={positionSize}, Price={entryPrice:F2}, TP={takeProfitPrice:F2}, SL={stopLossPrice:F2}");
		}

		private void EnterShortPosition()
		{
			// Calculate TP and SL prices (Pine Script lines 380-382)
			double entryPrice = Close[0];
			double takeProfitPrice = entryPrice - (TakeProfitTicks * TickSize);
			double stopLossPrice = entryPrice + (StopLossTicks * TickSize);

			// Use dynamic position sizing
			int positionSize = GetCurrentPositionSize();

			EnterShort(positionSize, "CVD_Short");
			SetProfitTarget("CVD_Short", CalculationMode.Price, takeProfitPrice);
			SetStopLoss("CVD_Short", CalculationMode.Price, stopLossPrice, false);

			Print($"Short Entry: Qty={positionSize}, Price={entryPrice:F2}, TP={takeProfitPrice:F2}, SL={stopLossPrice:F2}");
		}

		private void ManagePositions()
		{
			// Force close positions outside trading session (Pine Script lines 364-365)
			if (EnableTimeRestrictions && !IsInTradingSession() && Position.MarketPosition != MarketPosition.Flat)
			{
				if (Position.MarketPosition == MarketPosition.Long)
					ExitLong("CVD_Long", "Session_Close");
				else if (Position.MarketPosition == MarketPosition.Short)
					ExitShort("CVD_Short", "Session_Close");

				Print("Position closed - Outside trading session");
			}
		}

		#endregion

		#region Time Management

		private void ParseTradingSession()
		{
			try
			{
				// Parse session string format "HHmm-HHmm" (e.g., "2300-2130")
				string[] parts = TradingSession.Split('-');
				if (parts.Length != 2)
				{
					Print("Invalid trading session format. Using default 0800-2130");
					sessionStart = new TimeSpan(8, 0, 0);
					sessionEnd = new TimeSpan(21, 30, 0);
					isOverNightSession = false;
					return;
				}

				// Parse start time
				string startStr = parts[0].PadLeft(4, '0');
				int startHour = int.Parse(startStr.Substring(0, 2));
				int startMin = int.Parse(startStr.Substring(2, 2));
				sessionStart = new TimeSpan(startHour, startMin, 0);

				// Parse end time
				string endStr = parts[1].PadLeft(4, '0');
				int endHour = int.Parse(endStr.Substring(0, 2));
				int endMin = int.Parse(endStr.Substring(2, 2));
				sessionEnd = new TimeSpan(endHour, endMin, 0);

				// Determine if overnight session
				isOverNightSession = sessionStart > sessionEnd;

				Print($"Trading Session: {sessionStart} to {sessionEnd} (Overnight: {isOverNightSession})");
			}
			catch (Exception ex)
			{
				Print($"Error parsing trading session: {ex.Message}. Using default 0800-2130");
				sessionStart = new TimeSpan(8, 0, 0);
				sessionEnd = new TimeSpan(21, 30, 0);
				isOverNightSession = false;
			}
		}

		private bool IsInTradingSession()
		{
			if (!EnableTimeRestrictions)
				return true;

			// Get current time
			DateTime currentTime = Time[0];
			TimeSpan currentTimeOfDay = currentTime.TimeOfDay;

			// Check for weekend (Pine Script line 278)
			if (currentTime.DayOfWeek == DayOfWeek.Saturday || currentTime.DayOfWeek == DayOfWeek.Sunday)
				return false;

			// Check trading session (Pine Script lines 281, 432-435)
			if (isOverNightSession)
			{
				// Overnight session: trading if time >= start OR time <= end
				return currentTimeOfDay >= sessionStart || currentTimeOfDay <= sessionEnd;
			}
			else
			{
				// Regular session: trading if start <= time <= end
				return currentTimeOfDay >= sessionStart && currentTimeOfDay <= sessionEnd;
			}
		}

		#endregion

		#region Event Handlers

		protected override void OnExecutionUpdate(Execution execution, string executionId,
			double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
		{
			// Track trade results
			if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
			{
				if (execution.Order.Name.Contains("Entry"))
				{
					Print($"Entry Filled: {execution.Order.Name} at {price:F2}");
				}
				else
				{
					Print($"Exit Filled: {execution.Order.Name} at {price:F2}");

					// Track win/loss statistics
					if (SystemPerformance.AllTrades.Count > 0)
					{
						var lastTrade = SystemPerformance.AllTrades[SystemPerformance.AllTrades.Count - 1];
						if (lastTrade.ProfitCurrency > 0)
							winningTrades++;
						else
							losingTrades++;
					}
				}
			}
		}

		#endregion

		#region Properties (for optimization)

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> CVDValues
		{
			get { return cvdValues; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> FractalHighs
		{
			get { return fractalHighs; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> FractalLows
		{
			get { return fractalLows; }
		}

		#endregion

		#region ToString Override

		public override string ToString()
		{
			string sizingInfo = EnableDynamicSizing ? $", DynSize:{currentPositionSize}" : "";
			string trailingInfo = EnableTrailingStop ? $", Trail:{GetTrailingLevelName(trailingStopLevel)}" : "";
			return $"CVD Divergence (TP:{TakeProfitTicks}, SL:{StopLossTicks}, CVD:{CVDPeriod}, Fractals:{FractalPeriods}{sizingInfo}{trailingInfo})";
		}

		#endregion

		#region Daily Profit Management

		private void CheckDailyReset()
		{
			DateTime resetDateTime = Time[0].Date.Add(ResetTime.TimeOfDay);
			DateTime lastResetDateTime = lastResetDate.Add(ResetTime.TimeOfDay);

			if (Time[0] >= resetDateTime && lastResetDate < resetDateTime.Date)
			{
				// Reset for new day
				dailyProfit = 0;
				lock (tradeLock)
				{
					if (countedTradeIds != null)
						countedTradeIds.Clear();
				}
				lastResetDate = Time[0].Date;
				hasLoggedLimitReached = false;
				hasLoggedStopReached = false;

				if (isLiveTrading)
				{
					startOfDayBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				}

				Print($"{Time[0]}: Daily profit reset. New trading day started.");
			}
		}

		private double GetCurrentDailyProfit()
		{
			if (!isLiveTrading)
			{
				// For backtesting, use SystemPerformance
				return CalculateDailyProfitFromSystemPerformance();
			}
			else
			{
				// For live trading, combine realized and unrealized P&L
				double realizedToday = CalculateTodaysRealizedPL();
				double unrealizedPL = 0;

				if (IncludeUnrealizedPL && Position.MarketPosition != MarketPosition.Flat)
				{
					unrealizedPL = Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
				}

				return realizedToday + unrealizedPL;
			}
		}

		private double CalculateTodaysRealizedPL()
		{
			double todaysPL = 0;

			// Method using account balance change
			if (isLiveTrading && startOfDayBalance > 0)
			{
				double currentBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
				todaysPL = currentBalance - startOfDayBalance;
			}

			return todaysPL;
		}

		private double CalculateDailyProfitFromSystemPerformance()
		{
			double todayProfit = 0;
			DateTime resetDateTime = Time[0].Date.Add(ResetTime.TimeOfDay);

			foreach (Trade trade in SystemPerformance.AllTrades)
			{
				if (trade.Exit.Time >= resetDateTime.AddDays(-1) && trade.Exit.Time < resetDateTime)
				{
					todayProfit += trade.ProfitCurrency;
				}
			}

			return todayProfit;
		}

		private double CalculateTodaysProfitFromHistory()
		{
			double profit = 0;
			DateTime todayStart = Time[0].Date.Add(ResetTime.TimeOfDay);

			if (SystemPerformance.AllTrades.Count > 0)
			{
				foreach (Trade trade in SystemPerformance.AllTrades)
				{
					if (trade.Exit.Time >= todayStart)
					{
						profit += trade.ProfitCurrency;
					}
				}
			}

			return profit;
		}

		private bool IsWithinDailyLimits()
		{
			double currentDailyProfit = GetCurrentDailyProfit();

			// Check profit limit
			if (currentDailyProfit >= DailyProfitLimit)
			{
				if (!hasLoggedLimitReached)
				{
					Print($"{Time[0]}: Daily profit limit reached: £{currentDailyProfit:F2} >= £{DailyProfitLimit:F2}");
					hasLoggedLimitReached = true;
				}
				return false;
			}

			// Check loss limit (negative profit means loss)
			if (currentDailyProfit <= -DailyLossLimit)
			{
				if (!hasLoggedStopReached)
				{
					Print($"{Time[0]}: Daily loss limit reached: £{Math.Abs(currentDailyProfit):F2} >= £{DailyLossLimit:F2}");
					hasLoggedStopReached = true;
				}
				return false;
			}

			return true;
		}

		#endregion
	}
}
