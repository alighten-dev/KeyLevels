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
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// A simple class to represent a key level.
public class KeyLevel
{
    public string Name { get; set; }
    public double Value { get; set; }
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class KeyLevels : Indicator
    {
        private CurrentDayOHL currentDayOHL1;
        private PriorDayOHLC priorDayOHLC1;
        private Pivots pivots1;
		private int firstBarOfSession;
		private SimpleFont myFont;
		
		       
        private bool yPOCCalculated;         // flag to ensure we compute yesterday's POC only once
		
		// Variables for the Opening Range (first 15 minutes starting at 9:30)
		double _yPOC = 0;
        private double _ORHigh = 0;
        private double _ORLow = double.MaxValue;
        private double _ORCenter = 0;
        private bool ORCompleted = false;


        // Collection that will hold all key levels.
        private List<KeyLevel> keyLevelsList = new List<KeyLevel>();

        // Option to draw the horizontal lines for each key level.
        [NinjaScriptProperty]
        [Display(Name = "Draw Levels", Description = "Draw horizontal lines for each key level", Order = 1, GroupName = "Parameters")]
        public bool DrawLevels { get; set; } = true;
		
		[XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "KeyLevel Color", Order = 2, GroupName = "Parameters")]
        public Brush KeyLevelColor { get; set; } = Brushes.Goldenrod;
        [Browsable(false)]
        public string KeyLevelColorSerialize
        {
            get { return Serialize.BrushToString(KeyLevelColor); }
            set { KeyLevelColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public List<KeyLevel> KeyLevelsCollection
        {
            get { return keyLevelsList; }
        }


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "KeyLevels";
                Description = "Combines previous day OHLC and POC with the current day's OHL, Pivot Point, R1, S1, POC, and Opening Range High/Low/Center into a single collection of key levels. - By Alighten";
                Calculate = Calculate.OnEachTick; 
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
                BarsRequiredToPlot = 1;
            }
            else if (State == State.DataLoaded)
            {
                currentDayOHL1 = CurrentDayOHL();
                priorDayOHLC1 = PriorDayOHLC();
                pivots1 = Pivots(PivotRange.Daily, HLCCalculationMode.CalcFromIntradayData, 0, 0, 0, 20) ;
				
				myFont = new SimpleFont("Arial", 12) { Size = 12, Bold = false };
				
				yPOCCalculated = false;
            }
        }
		
		
        protected override void OnBarUpdate()
        {
            // Ensure we have at least one bar.
            if (CurrentBars[0] < 6)
                return;
			
			if (Bars.IsFirstBarOfSession)
		    {
		        firstBarOfSession = CurrentBar;
				_yPOC = 0;
				yPOCCalculated = false;
				_ORHigh = High[0];
    			_ORLow = Low[0];
                _ORCenter = 0;
                ORCompleted = false;
		    }
		
		    // Compute yesterday's POC on the first bar of the new session if not computed yet.
			
		    if (Bars.IsFirstBarOfSession && !yPOCCalculated)
            {
                DateTime yesterday = Time[0].Date.AddDays(-1);
                double maxVolume = 0;
                double poc = 0;
                // Loop through historical bars; you might limit the loop to a certain number if needed.
                for (int i = 0; i < CurrentBar; i++)
                {
                    if (Time[i].Date == yesterday)
                    {
                        if (Volume[i] > maxVolume)
                        {
                            maxVolume = Volume[i];
                            // OPTIONS A: Use average of High and Low as an approximation for the bar's price level.
                            //poc = (High[i] + Low[i]) / 2;
							// OPTION B: using a simple average that includes Close.
				            //poc = (High[i] + Low[i] + Close[i]) / 3;
				            // OPTION C: a weighted average giving more importance to Close.
				            poc = (High[i] + Low[i] + 2 * Close[i]) / 4;
                        }
                    }
                }
                _yPOC = poc;
                yPOCCalculated = true;
            }
			
			double _tPOC = 0;
            double maxVolumeToday = 0;
            for (int i = 0; i < CurrentBar; i++)
            {
                // Only consider bars from today.
                if (Time[i].Date == Time[0].Date)
                {
                    if (Volume[i] > maxVolumeToday)
                    {
                        maxVolumeToday = Volume[i];
                        // Option C: weighted average giving more weight to the Close.
                        _tPOC = (High[i] + Low[i] + 2 * Close[i]) / 4;
                    }
                }
            }
			
			SessionIterator sessionIterator = new SessionIterator(Bars);
			DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
			DateTime sessionOpenDT  = tradingDay.Date.Add(new TimeSpan(9, 30, 0));
			DateTime sessionOREndDT = tradingDay.Date.Add(new TimeSpan(9, 45, 0));
			
			if (Time[0] >= sessionOpenDT && Time[0] < sessionOREndDT)
			{
			    _ORHigh = Math.Max(_ORHigh, High[0]);
			    _ORLow  = Math.Min(_ORLow, Low[0]);
			}
			else if (Time[0] >= sessionOREndDT && !ORCompleted)
			{
			    _ORCenter = (_ORHigh + _ORLow) / 2;
			    ORCompleted = true;
			}


            // Retrieve the key levels from the sub-indicators.
            double _yOpen  = priorDayOHLC1.PriorOpen[0];
            double _yHigh  = priorDayOHLC1.PriorHigh[0];
            double _yLow   = priorDayOHLC1.PriorLow[0];
            double _yClose = priorDayOHLC1.PriorClose[0];

            double _tOpen  = currentDayOHL1.CurrentOpen[0];
            double _tHigh  = currentDayOHL1.CurrentHigh[0];
            double _tLow   = currentDayOHL1.CurrentLow[0];

            double _tPP = pivots1.Pp[0];
            double _tR1 = pivots1.R1[0];
            double _tS1 = pivots1.S1[0];

            // Update the key levels collection.
            keyLevelsList.Clear();
            keyLevelsList.Add(new KeyLevel { Name = "yOpen", Value = _yOpen });
            keyLevelsList.Add(new KeyLevel { Name = "yHigh", Value = _yHigh });
            keyLevelsList.Add(new KeyLevel { Name = "yLow",  Value = _yLow });
            keyLevelsList.Add(new KeyLevel { Name = "yClose", Value = _yClose });
			keyLevelsList.Add(new KeyLevel { Name = "yPOC", Value = _yPOC });

            keyLevelsList.Add(new KeyLevel { Name = "tOpen", Value = _tOpen });
            keyLevelsList.Add(new KeyLevel { Name = "tHigh", Value = _tHigh });
            keyLevelsList.Add(new KeyLevel { Name = "tLow",  Value = _tLow });
			keyLevelsList.Add(new KeyLevel { Name = "tPOC", Value = _tPOC });

            keyLevelsList.Add(new KeyLevel { Name = "tPP", Value = _tPP });
            keyLevelsList.Add(new KeyLevel { Name = "tR1", Value = _tR1 });
            keyLevelsList.Add(new KeyLevel { Name = "tS1", Value = _tS1 });
			
			if (ORCompleted)
            {
                keyLevelsList.Add(new KeyLevel { Name = "ORHigh", Value = _ORHigh });
                keyLevelsList.Add(new KeyLevel { Name = "ORLow", Value = _ORLow });
                keyLevelsList.Add(new KeyLevel { Name = "ORCenter", Value = _ORCenter });
            }

            // Optionally draw each key level as a horizontal line.
            if (DrawLevels)
            {				
				
				int startBarsAgo = CurrentBar - firstBarOfSession;

				Draw.Line(this, "yOpen", false, startBarsAgo, _yOpen, 0, _yOpen, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "yHigh", false, startBarsAgo, _yHigh, 0, _yHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "yLow", false, startBarsAgo, _yLow, 0, _yLow, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "yClose", false, startBarsAgo, _yClose, 0, _yClose, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "yPOC", false, startBarsAgo, _yPOC, 0, _yPOC, KeyLevelColor, DashStyleHelper.Dot, 2);

				Draw.Line(this, "tOpen", false, startBarsAgo, _tOpen, 0, _tOpen, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "tHigh", false, startBarsAgo, _tHigh, 0, _tHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "tLow", false, startBarsAgo, _tLow, 0, _tLow, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "tPOC", false, startBarsAgo, _tPOC, 0, _tPOC, KeyLevelColor, DashStyleHelper.Dot, 2);
              
				Draw.Line(this, "tPP", false, startBarsAgo, _tPP, 0, _tPP, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "tR1", false, startBarsAgo, _tR1, 0, _tR1, KeyLevelColor, DashStyleHelper.Dot, 2);
				Draw.Line(this, "tS1", false, startBarsAgo, _tS1, 0, _tS1, KeyLevelColor, DashStyleHelper.Dot, 2);
				
				if (ORCompleted)
                {
                    Draw.Line(this, "ORHigh", false, startBarsAgo, _ORHigh, 0, _ORHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "ORLow", false, startBarsAgo, _ORLow, 0, _ORLow, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "ORCenter", false, startBarsAgo, _ORCenter, 0, _ORCenter, KeyLevelColor, DashStyleHelper.Dot, 2);
                }
				
				int labelBarsAgo = 3;
                int offset = 10;
				
				Draw.Text(this, "label_yOpen", false, "yOpen", labelBarsAgo, _yOpen, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_yHigh", false, "yHigh", labelBarsAgo, _yHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_yLow", false, "yLow", labelBarsAgo, _yLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_yClose", false, "yClose", labelBarsAgo, _yClose, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_yPOC", false, "yPOC", labelBarsAgo, _yPOC, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);

				Draw.Text(this, "label_tOpen", false, "tOpen", labelBarsAgo, _tOpen, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_tHigh", false, "tHigh", labelBarsAgo, _tHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_tLow", false, "tLow", labelBarsAgo, _tLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_tPOC", false, "tPOC", labelBarsAgo, _tPOC, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				
				Draw.Text(this, "label_tPP", false, "tPP", labelBarsAgo, _tPP, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_tR1", false, "tR1", labelBarsAgo, _tR1, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				Draw.Text(this, "label_tS1", false, "tS1", labelBarsAgo, _tS1, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
				
				if (ORCompleted)
                {
                    Draw.Text(this, "label_ORHigh", false, "ORHigh", labelBarsAgo, _ORHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_ORLow", false, "ORLow", labelBarsAgo, _ORLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_ORCenter", false, "ORCenter", labelBarsAgo, _ORCenter, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }

            }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private KeyLevels[] cacheKeyLevels;
		public KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor)
		{
			return KeyLevels(Input, drawLevels, keyLevelColor);
		}

		public KeyLevels KeyLevels(ISeries<double> input, bool drawLevels, Brush keyLevelColor)
		{
			if (cacheKeyLevels != null)
				for (int idx = 0; idx < cacheKeyLevels.Length; idx++)
					if (cacheKeyLevels[idx] != null && cacheKeyLevels[idx].DrawLevels == drawLevels && cacheKeyLevels[idx].KeyLevelColor == keyLevelColor && cacheKeyLevels[idx].EqualsInput(input))
						return cacheKeyLevels[idx];
			return CacheIndicator<KeyLevels>(new KeyLevels(){ DrawLevels = drawLevels, KeyLevelColor = keyLevelColor }, input, ref cacheKeyLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor);
		}
	}
}

#endregion
