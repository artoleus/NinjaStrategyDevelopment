# Indicators
Build innovative chart indicators

Build, test and deploy automated trading strategies

Develop drawing tools, chart styles, and custom rendering

Create completely new experiences

Trade and Market Data APIs available
# DefaultChartStyle
## [Definition](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#definition)
Allows to set a default ChartStyle for usage with a NinjaTrader bars type
## [Property Value](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#property-value)
A **ChartStyleType** enum value representing the [ChartStyle](https://developer.ninjatrader.com/docs/desktop/chartstyletype) to be set as default. System defaults include:
  * **ChartStyleType.Box**
  * **ChartStyleType.CandleStick**
  * **ChartStyleType.LineOnClose**
  * **ChartStyleType.OHLC**
  * **ChartStyleType.PointAndFigure**
  * **ChartStyleType.KagiLine**
  * **ChartStyleType.OpenClose**
  * **ChartStyleType.Mountain**
## [Syntax](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#syntax)
`DefaultChartStyle`
## [Examples](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#examples)
```
protected override void OnStateChange()
{
  if (State == State.SetDefaults)
  {
    Name           = "SampleBarsType";
    BarsPeriod        = new BarsPeriod { BarsPeriodType = (BarsPeriodType) 15, BarsPeriodTypeName = "SampleBarsType(15)", Value = 1 };
    BuiltFrom        = BarsPeriodType.Minute;
    DaysToLoad        = 5;
    DefaultChartStyle    = Gui.Chart.ChartStyleType.CandleStick;
    IsIntraday        = true;
  }
}
```
#### ON THIS PAGE
  * [Definition](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#definition)
  * [Property Value](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#property-value)
  * [Syntax](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#syntax)
  * [Examples](https://developer.ninjatrader.com/docs/desktop/defaultchartstyle#examples)

  * [Strategies](https://developer.ninjatrader.com/products/strategies)
  * [Chart Components](https://developer.ninjatrader.com/products/chart-experiences)
  * [Add-On Development](https://developer.ninjatrader.com/products/user-experiences)
  * [APIs](https://developer.ninjatrader.com/products/api)

  * [Support Forum](https://discourse.ninjatrader.com/)

[](https://www.linkedin.com/company/ninjatrader-group-llc)

---

*Clean NinjaScript Documentation for RAG/AI Development*
