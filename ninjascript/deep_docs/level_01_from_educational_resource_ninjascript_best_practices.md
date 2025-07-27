# Indicators
Build innovative chart indicators

Build, test and deploy automated trading strategies

Develop drawing tools, chart styles, and custom rendering

Create completely new experiences

Trade and Market Data APIs available
# NinjaScript Best Practices
There are some best practices to be aware of when developing **NinjaScript** classes. The following tables present a non-exhaustive list of considerations to keep in mind when designing and implementing your code.
## Note
  * NinjaTrader is multi-threaded and event driven. Always assume that any of the methods you implement in NinjaScript could be called from another thread.
## [State management practices](https://developer.ninjatrader.com/docs/desktop/ninjascript_best_practices#state-management-practices)
### Managing Resources
  * The **OnStateChange()** method is called anytime there has been a change of **State** and can be used to help you setup, manage, and destroy several types of resources. Where these values are setup is highly dependent on the kind of resource you are using. The section below will cover how to manage various resources throughout different states.
### Setting Default UI Property Grid values
  * Reserve **State.SetDefaults** for defaulting any public properties you wish to have exposed on the UI property grid. You should also use this **State** for setting default desired **NinjaScript** property behavior which can be overridden from the property grid (e.g. **Calculate** , **IsOverlay** , etc.). For Plots and Lines you wish to configure, **AddPlot()** , **AddLine()** should also have their default values set during this **State**.
Why

---

*Clean NinjaScript Documentation for RAG/AI Development*
