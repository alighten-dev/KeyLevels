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
        #region Public Properties for Key Levels
        [Browsable(false)]
        [XmlIgnore]
        public double YOpen { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double YHigh { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double YLow { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double YClose { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double YPOC { get { return _yPOC; } }  // Already computed in _yPOC

        [Browsable(false)]
        [XmlIgnore]
        public double TOpen { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double THigh { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TLow { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TPOC { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TPP { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TR1 { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TS1 { get; private set; }

        [Browsable(false)]
        [XmlIgnore]
        public double ORHigh { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double ORLow { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double ORCenter { get; private set; }

        // ---- New Initial Balance Public Properties ----
        [Browsable(false)]
        [XmlIgnore]
        public double IBHigh { get { return IBCompleted ? _IBHigh : 0; } }
        [Browsable(false)]
        [XmlIgnore]
        public double IBLow { get { return IBCompleted ? _IBLow : 0; } }
        [Browsable(false)]
        [XmlIgnore]
        public double IBCenter { get { return IBCompleted ? _IBCenter : 0; } }
        #endregion

        #region Define Variables
        private CurrentDayOHL currentDayOHL1;
        private PriorDayOHLC priorDayOHLC1;
        private Pivots pivots1;
        private int firstBarOfSession;
        private SimpleFont myFont;

        private bool yPOCCalculated;         // flag to ensure we compute yesterday's POC only once
		
        // Variables for the Opening Range
        private double _yPOC = 0;
        private double _ORHigh = 0;
        private double _ORLow = double.MaxValue;
        private double _ORCenter = 0;
        private bool ORCompleted = false;

        // ---- Variables for the Initial Balance (IB) ----
        private double _IBHigh = 0;
        private double _IBLow = double.MaxValue;
        private double _IBCenter = 0;
        private bool IBCompleted = false;

        // Collection that will hold all key levels.
        private List<KeyLevel> keyLevelsList = new List<KeyLevel>();
        #endregion

        #region Indicator Parameters

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

        /////////// New Checkbox Parameters for Groups ///////////
        [NinjaScriptProperty]
        [Display(Name = "Show Yesterday's POC", Order = 10, GroupName = "Display Groups")]
        public bool ShowYPOC { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's POC", Order = 11, GroupName = "Display Groups")]
        public bool ShowTPOC { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Open Range", Order = 12, GroupName = "Display Groups")]
        public bool ShowOpenRange { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Yesterday's OHLC", Order = 13, GroupName = "Display Groups")]
        public bool ShowYesterdayOHLC { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's OHL", Order = 14, GroupName = "Display Groups")]
        public bool ShowTodayOHL { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's Pivot Points", Order = 15, GroupName = "Display Groups")]
        public bool ShowTodayPivotPoints { get; set; } = true;

        // --------- New Parameter for Initial Balance Display ---------
        [NinjaScriptProperty]
        [Display(Name = "Show Initial Balance", Order = 16, GroupName = "Display Groups")]
        public bool ShowInitialBalance { get; set; } = true;

        /////////// Configurable Open Range Times ///////////
		[NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range Start Time", Order = 20, GroupName = "Open Range Settings", Description = "Configure the start time for the Open Range period")]
        public DateTime OpenRangeStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range End Time", Order = 21, GroupName = "Open Range Settings", Description = "Configure the end time for the Open Range period")]
        public DateTime OpenRangeEnd { get; set; } =  DateTime.Parse("10:00", System.Globalization.CultureInfo.InvariantCulture);

        // --------- New Configurable Initial Balance Times ---------
        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance Start Time", Order = 22, GroupName = "Initial Balance Settings", Description = "Configure the start time for the Initial Balance period")]
        public DateTime IBStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance End Time", Order = 23, GroupName = "Initial Balance Settings", Description = "Configure the end time for the Initial Balance period")]
        public DateTime IBEnd { get; set; } = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);

        #endregion

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
                Description = "Combines previous day OHLC and POC with the current day's OHL, Pivot Point, R1, S1, POC, Opening Range and Initial Balance levels into a single collection of key levels. - By Alighten";
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
                pivots1 = Pivots(PivotRange.Daily, HLCCalculationMode.CalcFromIntradayData, 0, 0, 0, 20);
				
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

                // ---- Reset Initial Balance variables at session start ----
                _IBHigh = High[0];
                _IBLow = Low[0];
                _IBCenter = 0;
                IBCompleted = false;
            }
		
            // Compute yesterday's POC on the first bar of the new session if not computed yet.
            if (Bars.IsFirstBarOfSession && !yPOCCalculated && ShowYPOC)
            {
                DateTime yesterday = Time[0].Date.AddDays(-1);
                double maxVolume = 0;
                double poc = 0;
                for (int i = 0; i < CurrentBar; i++)
                {
                    if (Time[i].Date == yesterday)
                    {
                        if (Volume[i] > maxVolume)
                        {
                            maxVolume = Volume[i];
                            // Option C: weighted average giving more importance to Close.
                            poc = (High[i] + Low[i] + 2 * Close[i]) / 4;
                        }
                    }
                }
                _yPOC = poc;
                yPOCCalculated = true;
            }
			
            double tPOC = 0;
            double maxVolumeToday = 0;
			if (ShowTPOC)
			{
	            for (int i = 0; i < CurrentBar; i++)
	            {
	                if (Time[i].Date == Time[0].Date)
	                {
	                    if (Volume[i] > maxVolumeToday)
	                    {
	                        maxVolumeToday = Volume[i];
	                        tPOC = (High[i] + Low[i] + 2 * Close[i]) / 4;
	                    }
	                }
	            }
			}
			
            SessionIterator sessionIterator = new SessionIterator(Bars);
            DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
            // Use configurable open range times:
            DateTime sessionOpenDT  = tradingDay.Date.Add(OpenRangeStart.TimeOfDay);
            DateTime sessionOREndDT = tradingDay.Date.Add(OpenRangeEnd.TimeOfDay);
			
            if (Time[0] >= sessionOpenDT && Time[0] < sessionOREndDT)
            {
                _ORHigh = Math.Max(_ORHigh, High[0]);
                _ORLow = Math.Min(_ORLow, Low[0]);
            }
            else if (Time[0] >= sessionOREndDT && !ORCompleted)
            {
                _ORCenter = (_ORHigh + _ORLow) / 2;
                ORCompleted = true;
            }

            // ---- Compute Initial Balance (IB) using its own configurable times ----
            DateTime sessionIBStartDT = tradingDay.Date.Add(IBStart.TimeOfDay);
            DateTime sessionIBEndDT = tradingDay.Date.Add(IBEnd.TimeOfDay);
            if (Time[0] >= sessionIBStartDT && Time[0] < sessionIBEndDT)
            {
                _IBHigh = Math.Max(_IBHigh, High[0]);
                _IBLow = Math.Min(_IBLow, Low[0]);
            }
            else if (Time[0] >= sessionIBEndDT && !IBCompleted)
            {
                _IBCenter = (_IBHigh + _IBLow) / 2;
                IBCompleted = true;
            }

            // Update public properties based on the sub-indicator values.
            YOpen = priorDayOHLC1.PriorOpen[0];
            YHigh = priorDayOHLC1.PriorHigh[0];
            YLow = priorDayOHLC1.PriorLow[0];
            YClose = priorDayOHLC1.PriorClose[0];

            TOpen = currentDayOHL1.CurrentOpen[0];
            THigh = currentDayOHL1.CurrentHigh[0];
            TLow = currentDayOHL1.CurrentLow[0];
            TPOC = tPOC;

            TPP = pivots1.Pp[0];
            TR1 = pivots1.R1[0];
            TS1 = pivots1.S1[0];

            if (ORCompleted)
            {
                ORHigh = _ORHigh;
                ORLow = _ORLow;
                ORCenter = _ORCenter;
            }
            else
            {
                ORHigh = 0;
                ORLow = 0;
                ORCenter = 0;
            }

            // Update the key levels collection based on enabled groups.
            keyLevelsList.Clear();
            if (ShowYesterdayOHLC)
            {
                keyLevelsList.Add(new KeyLevel { Name = "yOpen", Value = YOpen });
                keyLevelsList.Add(new KeyLevel { Name = "yHigh", Value = YHigh });
                keyLevelsList.Add(new KeyLevel { Name = "yLow",  Value = YLow });
                keyLevelsList.Add(new KeyLevel { Name = "yClose", Value = YClose });
            }
            if (ShowYPOC)
                keyLevelsList.Add(new KeyLevel { Name = "yPOC", Value = _yPOC });
            if (ShowTodayOHL)
            {
                keyLevelsList.Add(new KeyLevel { Name = "tOpen", Value = TOpen });
                keyLevelsList.Add(new KeyLevel { Name = "tHigh", Value = THigh });
                keyLevelsList.Add(new KeyLevel { Name = "tLow",  Value = TLow });
            }
            if (ShowTPOC)
                keyLevelsList.Add(new KeyLevel { Name = "tPOC", Value = TPOC });
            if (ShowTodayPivotPoints)
            {
                keyLevelsList.Add(new KeyLevel { Name = "tPP", Value = TPP });
                keyLevelsList.Add(new KeyLevel { Name = "tR1", Value = TR1 });
                keyLevelsList.Add(new KeyLevel { Name = "tS1", Value = TS1 });
            }
            if (ShowOpenRange && ORCompleted)
            {
                keyLevelsList.Add(new KeyLevel { Name = "ORHigh", Value = ORHigh });
                keyLevelsList.Add(new KeyLevel { Name = "ORLow", Value = ORLow });
                keyLevelsList.Add(new KeyLevel { Name = "ORCenter", Value = ORCenter });
            }
            // ---- Add Initial Balance levels if enabled and completed ----
            if (ShowInitialBalance && IBCompleted)
            {
                keyLevelsList.Add(new KeyLevel { Name = "IBHigh", Value = IBHigh });
                keyLevelsList.Add(new KeyLevel { Name = "IBLow", Value = IBLow });
                keyLevelsList.Add(new KeyLevel { Name = "IBCenter", Value = IBCenter });
            }

            // Optionally draw each key level as a horizontal line.
            if (DrawLevels)
            {				
                int startBarsAgo = CurrentBar - firstBarOfSession;
                int labelBarsAgo = 3;
                int offset = 10;

                // Yesterday's OHLC lines/text
                if (ShowYesterdayOHLC)
                {
                    Draw.Line(this, "yOpen", false, startBarsAgo, YOpen, 0, YOpen, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "yHigh", false, startBarsAgo, YHigh, 0, YHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "yLow", false, startBarsAgo, YLow, 0, YLow, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "yClose", false, startBarsAgo, YClose, 0, YClose, KeyLevelColor, DashStyleHelper.Dot, 2);

                    Draw.Text(this, "label_yOpen", false, "yOpen", labelBarsAgo, YOpen, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_yHigh", false, "yHigh", labelBarsAgo, YHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_yLow", false, "yLow", labelBarsAgo, YLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_yClose", false, "yClose", labelBarsAgo, YClose, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                if (ShowYPOC)
                {
                    Draw.Line(this, "yPOC", false, startBarsAgo, _yPOC, 0, _yPOC, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Text(this, "label_yPOC", false, "yPOC", labelBarsAgo, _yPOC, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                // Today's OHL lines/text
                if (ShowTodayOHL)
                {
                    Draw.Line(this, "tOpen", false, startBarsAgo, TOpen, 0, TOpen, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "tHigh", false, startBarsAgo, THigh, 0, THigh, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "tLow", false, startBarsAgo, TLow, 0, TLow, KeyLevelColor, DashStyleHelper.Dot, 2);

                    Draw.Text(this, "label_tOpen", false, "tOpen", labelBarsAgo, TOpen, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_tHigh", false, "tHigh", labelBarsAgo, THigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_tLow", false, "tLow", labelBarsAgo, TLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                if (ShowTPOC)
                {
                    Draw.Line(this, "tPOC", false, startBarsAgo, TPOC, 0, TPOC, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Text(this, "label_tPOC", false, "tPOC", labelBarsAgo, TPOC, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                // Today's Pivot Points lines/text
                if (ShowTodayPivotPoints)
                {
                    Draw.Line(this, "tPP", false, startBarsAgo, TPP, 0, TPP, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "tR1", false, startBarsAgo, TR1, 0, TR1, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "tS1", false, startBarsAgo, TS1, 0, TS1, KeyLevelColor, DashStyleHelper.Dot, 2);

                    Draw.Text(this, "label_tPP", false, "tPP", labelBarsAgo, TPP, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_tR1", false, "tR1", labelBarsAgo, TR1, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_tS1", false, "tS1", labelBarsAgo, TS1, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                // Open Range lines/text
                if (ShowOpenRange && ORCompleted)
                {
                    Draw.Line(this, "ORHigh", false, startBarsAgo, ORHigh, 0, ORHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "ORLow", false, startBarsAgo, ORLow, 0, ORLow, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "ORCenter", false, startBarsAgo, ORCenter, 0, ORCenter, KeyLevelColor, DashStyleHelper.Dot, 2);

                    Draw.Text(this, "label_ORHigh", false, "ORHigh", labelBarsAgo, ORHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_ORLow", false, "ORLow", labelBarsAgo, ORLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_ORCenter", false, "ORCenter", labelBarsAgo, ORCenter, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                }
                // ---- Initial Balance lines/text ----
                if (ShowInitialBalance && IBCompleted)
                {
                    Draw.Line(this, "IBHigh", false, startBarsAgo, IBHigh, 0, IBHigh, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "IBLow", false, startBarsAgo, IBLow, 0, IBLow, KeyLevelColor, DashStyleHelper.Dot, 2);
                    Draw.Line(this, "IBCenter", false, startBarsAgo, IBCenter, 0, IBCenter, KeyLevelColor, DashStyleHelper.Dot, 2);

                    Draw.Text(this, "label_IBHigh", false, "IBHigh", labelBarsAgo, IBHigh, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_IBLow", false, "IBLow", labelBarsAgo, IBLow, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "label_IBCenter", false, "IBCenter", labelBarsAgo, IBCenter, offset, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
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
		public KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return KeyLevels(Input, drawLevels, keyLevelColor, showYPOC, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public KeyLevels KeyLevels(ISeries<double> input, bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			if (cacheKeyLevels != null)
				for (int idx = 0; idx < cacheKeyLevels.Length; idx++)
					if (cacheKeyLevels[idx] != null && cacheKeyLevels[idx].DrawLevels == drawLevels && cacheKeyLevels[idx].KeyLevelColor == keyLevelColor && cacheKeyLevels[idx].ShowYPOC == showYPOC && cacheKeyLevels[idx].ShowTPOC == showTPOC && cacheKeyLevels[idx].ShowOpenRange == showOpenRange && cacheKeyLevels[idx].ShowYesterdayOHLC == showYesterdayOHLC && cacheKeyLevels[idx].ShowTodayOHL == showTodayOHL && cacheKeyLevels[idx].ShowTodayPivotPoints == showTodayPivotPoints && cacheKeyLevels[idx].ShowInitialBalance == showInitialBalance && cacheKeyLevels[idx].OpenRangeStart == openRangeStart && cacheKeyLevels[idx].OpenRangeEnd == openRangeEnd && cacheKeyLevels[idx].IBStart == iBStart && cacheKeyLevels[idx].IBEnd == iBEnd && cacheKeyLevels[idx].EqualsInput(input))
						return cacheKeyLevels[idx];
			return CacheIndicator<KeyLevels>(new KeyLevels(){ DrawLevels = drawLevels, KeyLevelColor = keyLevelColor, ShowYPOC = showYPOC, ShowTPOC = showTPOC, ShowOpenRange = showOpenRange, ShowYesterdayOHLC = showYesterdayOHLC, ShowTodayOHL = showTodayOHL, ShowTodayPivotPoints = showTodayPivotPoints, ShowInitialBalance = showInitialBalance, OpenRangeStart = openRangeStart, OpenRangeEnd = openRangeEnd, IBStart = iBStart, IBEnd = iBEnd }, input, ref cacheKeyLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor, showYPOC, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor, showYPOC, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor, showYPOC, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor, bool showYPOC, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor, showYPOC, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}
	}
}

#endregion
