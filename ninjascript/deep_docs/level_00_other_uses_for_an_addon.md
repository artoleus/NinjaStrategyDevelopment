# Indicators
Build innovative chart indicators

Build, test and deploy automated trading strategies

Develop drawing tools, chart styles, and custom rendering

Create completely new experiences

Trade and Market Data APIs available
# Other Uses for an AddOn
## [Modifying Existing NinjaTrader Windows](https://developer.ninjatrader.com/docs/desktop/other_uses_for_an_addon#modifying-existing-ninjatrader-windows)
To modify an existing type of NinjaTrader window (for example, to add a button to all charts), you will first need to obtain a reference to each individual window of that type that is open. This can be done by overriding the **OnWindowCreated()** method, then declaring an object of the Type of the window you are looking for, and finally assigning the object a reference to the Window passed into the method:
```
// OnWindowCreated() will be called any time a new NTWindow is created. It will be called in the thread of that window
protected override void OnWindowCreated(Window window)
{
  // Declare a Chart object and instantiate it to the Window passed into the method
  Gui.Chart.Chart myChart = window as Gui.Chart.Chart;
  // Use this check to return if the calling Window is not of the Type you are looking for
  if (myChart == null)
    return;
}
```
If you are unsure of the Type name for a particular type of window, you can open an instance of that window then run the code below, which will print the Type to the Output Window:
```
protected override void OnWindowCreated(Window window)
{
  // Print the Type of any open windows, for future reference
  Print(window.ToString());
}
```
```
// Declare a Chart, ChartTrader, and UI elements to add to Chart Trader
Gui.Chart.Chart myChart;
Gui.Chart.ChartTrader chartTrader;
Button sampleButton;
Grid myGrid;
Grid mainGrid;
protected override void OnWindowCreated(Window window)
{
  // Instantiate myChart by assigning a reference to the calling Window
  myChart = window as Gui.Chart.Chart;
  if (myChart == null)
  {
    return;
  }
  //find chart trader from myChart's Chart Control by its Automation ID: "ChartWindowChartTrader"
  chartTrader = Window.GetWindow(myChart.ActiveChartControl.Parent).FindFirst("ChartWindowChartTraderControl") as Gui.Chart.ChartTrader;
  if (chartTrader == null)
  {
    return;
  }
  // Instantiate sampleButton
  sampleButton = new Button
  {
    Content = "Sample Button",
    Style = System.Windows.Application.Current.TryFindResource("Button") as Style
  };
  // Attach a custom event handler to the .Click event
  sampleButton.Click += SampleButton_Click;
  // Set a custom AutomationId for the button, so that it can be referenced elsewhere the same way we found Chart Trader
  System.Windows.Automation.AutomationProperties.SetAutomationId(sampleButton, "SampleButton");
  //this is the main chart trader grid where the default buttons and controls reside
  mainGrid = chartTrader.FindName("grdMain") as Grid;
  // Return if Chart Trader is null
  if (mainGrid == null)
  {
    return;
  }
  // by default, there will be 7 rows in Chart Trader, we need to add a new row for the new button
  if (mainGrid.RowDefinitions.Count <= 7)
    mainGrid.RowDefinitions.Add(new RowDefinition());
  //define a new grid, and add our button to that grid
  myGrid = new Grid();
  myGrid.Children.Add(sampleButton);
  //set my grid to the new row 
  Grid.SetRow(myGrid, 8);
  //finally, add our grid to the main grid
  mainGrid.Children.Add(myGrid);
}
private void SampleButton_Click(object sender, RoutedEventArgs e)
{
  Print("Sample Button Clicked");
}
```
Since we are dynamically adding elements to open windows, it is important to clean up any unused resources and detach any event handlers when the affected windows are destroyed. You can use the same approach as shown above to obtain a reference to each affected window within the **OnWindowDestroyed()** method:
```
protected override void OnWindowDestroyed(Window window)
{
  // Return if there is no button, or if the destroyed window is not a chart
  if(sampleButton == null || !(window is Gui.Chart.Chart))
  {
    return;
  }
  // Detach the event handler from the .Click event, remove the grid, and nullify the button
  sampleButton.Click -= SampleButton_Click;
  mainGrid.Children.Remove(myGrid);
  sampleButton = null;
}
```
Below is another example of adding elements into chart windows. In this example, we add a new panel to the top of all chart windows, then take all existing chart content and move it into a row beneath the panel we've just added:
```
protected override void OnWindowCreated(Window window)
{
  // Obtain a reference to any chart that triggered OnWindowCreated
  Chart Window = window as Chart;
  // Instantiate a grid to hold a reference to the content of the chart window
  Grid mainWindowGrid = Window.Content as Grid;
  // Add existing row definition for existing row if it is not present
  if (mainWindowGrid.RowDefinitions.Count == 0)
  {
    mainWindowGrid.RowDefinitions.Add(new RowDefinition());
  }
  // Instantiate a RowDefinition and set its height
  RowDefinition row = new RowDefinition();
  row.Height = new GridLength(PanelLength);
  // Insert the new row into the chart's main window grid
  mainWindowGrid.RowDefinitions.Insert(0, row);
  //Move Existing Elements down one row, since our new content will take the top row
  foreach (UIElement element in mainWindowGrid.Children)
  {
    element.SetValue(Grid.RowProperty, (int)element.GetValue(Grid.RowProperty) + 1);
  }
  //Create the Top Panel grid and add it to our newly defined row
  Grid Panel = new Grid();
  Panel.SetValue(Grid.RowProperty, 0);
  mainWindowGrid.Children.Add(Panel);
  //Create a sample text block and add it to the Top/Bottom Panel Grid.
  TextBlock TextBlock = new TextBlock();
  TextBlock.Text = PanelDirection.ToString() + " Panel (" + PanelLocation.ToString() + ") Sample Text Block";
  TextBlock.Foreground = Brushes.Red;
  TextBlock.SetValue(Grid.RowProperty, 0);
  Panel.Children.Add(TextBlock);
}
```
## [Accessing Account Data](https://developer.ninjatrader.com/docs/desktop/other_uses_for_an_addon#accessing-account-data)
From time to time, you may need to access certain global data, such as account values, order states, position info, etc. In these cases, you can subscribe to an appropriate event using a custom event handler method. Below is a list of a few such events which can be captured:
Method| Description

---

*Clean NinjaScript Documentation for RAG/AI Development*
