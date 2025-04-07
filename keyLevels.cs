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
using NinjaTrader.Gui.NinjaScript;
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
        // These values are still available for internal logic if needed.
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
        public double YPOC { get; private set; }  
        [Browsable(false)]
        [XmlIgnore]
        public double YVAH { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double YVAL { get; private set; }

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

        // Variables for yPOC/yVAH/yVAL
        private SessionIterator sessionIterator;  
        private double ValueAreaPercent = 0.70;
		
        // Variables for the Opening Range
        private double _ORHigh = 0;
        private double _ORLow = double.MaxValue;
        private double _ORCenter = 0;
        private bool ORCompleted = false;

        // ---- Variables for the Initial Balance (IB) ----
        private double _IBHigh = 0;
        private double _IBLow = double.MaxValue;
        private double _IBCenter = 0;
        private bool IBCompleted = false;

        // Optional key levels collection (if needed for internal use)
        private List<KeyLevel> keyLevelsList = new List<KeyLevel>();
		
		// Occupied price levels for label spacing
		List<double> occupiedPriceLevels = new List<double>();

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
        
        // New property for Open Range level color
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Open Range Color", Order = 3, GroupName = "Parameters")]
        public Brush OpenRangeColor { get; set; } = Brushes.DeepPink;

        [Browsable(false)]
        public string OpenRangeColorSerialize
        {
            get { return Serialize.BrushToString(OpenRangeColor); }
            set { OpenRangeColor = Serialize.StringToBrush(value); }
        }

        // New property for Initial Balance level color
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Initial Balance Color", Order = 4, GroupName = "Parameters")]
        public Brush IBColor { get; set; } = Brushes.Cyan;

        [Browsable(false)]
        public string IBColorSerialize
        {
            get { return Serialize.BrushToString(IBColor); }
            set { IBColor = Serialize.StringToBrush(value); }
        }

        /////////// Checkbox Parameters for Groups ///////////
        [NinjaScriptProperty]
        [Display(Name = "Show Yesterday's POC/VAH/VAL", Order = 10, GroupName = "Display Groups")]
        public bool ShowYProfile { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's POC", Order = 12, GroupName = "Display Groups")]
        public bool ShowTPOC { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Open Range", Order = 13, GroupName = "Display Groups")]
        public bool ShowOpenRange { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Yesterday's OHLC", Order = 14, GroupName = "Display Groups")]
        public bool ShowYesterdayOHLC { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's OHL", Order = 15, GroupName = "Display Groups")]
        public bool ShowTodayOHL { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Today's Pivot Points", Order = 16, GroupName = "Display Groups")]
        public bool ShowTodayPivotPoints { get; set; } = true;

        // --------- New Parameter for Initial Balance Display ---------
        [NinjaScriptProperty]
        [Display(Name = "Show Initial Balance", Order = 17, GroupName = "Display Groups")]
        public bool ShowInitialBalance { get; set; } = true;

        /////////// Configurable Open Range Times ///////////
		[NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range Start Time", Order = 20, GroupName = "Open Range Settings", Description = "Configure the start time for the Open Range period")]
        public DateTime OpenRangeStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range End Time", Order = 21, GroupName = "Open Range Settings", Description = "Configure the end time for the Open Range period")]
        public DateTime OpenRangeEnd { get; set; } =  DateTime.Parse("09:35", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance Start Time", Order = 22, GroupName = "Initial Balance Settings", Description = "Configure the start time for the Initial Balance period")]
        public DateTime IBStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance End Time", Order = 23, GroupName = "Initial Balance Settings", Description = "Configure the end time for the Initial Balance period")]
        public DateTime IBEnd { get; set; } = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);

//		[Browsable(false)]
//		[XmlIgnore]
//		public override bool AutoScale
//		{
//		    get { return false; }  // Always off
//		    set { /* do nothing */ }
//		}
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "KeyLevels";
                Description = "Combines previous day OHLC, POC, VAH, and VAL with the current day's OHL, Pivot Point, R1, S1, POC, Opening Range and Initial Balance levels into a single collection of key levels. - By Alighten";
                Calculate = Calculate.OnEachTick; 
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
				IsAutoScale = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
                BarsRequiredToPlot = 1;
				

                // Add plots for each key level.
                // Indices:
                // 0: YOpen, 1: YHigh, 2: YLow, 3: YClose
                // 4: YPOC, 5: YVAH, 6: YVAL
                // 7: TOpen, 8: THigh, 9: TLow
                // 10: TPOC
                // 11: TPP, 12: TR1, 13: TS1
                // 14: ORHigh, 15: ORLow, 16: ORCenter
                // 17: IBHigh, 18: IBLow, 19: IBCenter
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YOpen");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YHigh");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YLow");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YClose");

                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YPOC");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YVAH");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "YVAL");

                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TOpen");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "THigh");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TLow");

                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TPOC");

                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TPP");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TR1");
                AddPlot(new Stroke(KeyLevelColor), PlotStyle.Line, "TS1");

                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORHigh");
                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORLow");
                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORCenter");

                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBHigh");
                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBLow");
                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBCenter");
            }
            else if (State == State.Configure)
            {
                // Configuration code (if needed) remains unchanged.
            }
            else if (State == State.DataLoaded)
            {
                ClearOutputWindow();

                currentDayOHL1 = CurrentDayOHL();
                priorDayOHLC1 = PriorDayOHLC();
                pivots1 = Pivots(PivotRange.Daily, HLCCalculationMode.CalcFromIntradayData, 0, 0, 0, 20);
                sessionIterator = new SessionIterator(Bars);
                myFont = new SimpleFont("Arial", 12) { Size = 12, Bold = false };
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            // Ensure we have enough bars.
            if (CurrentBars[0] < 6)
                return;
						
            if (Bars.IsFirstBarOfSession)
            {
                firstBarOfSession = CurrentBar;
                YPOC = 0;
                YVAL = 0;
                YVAH = 0;
                _ORHigh = double.MinValue; 
                _ORLow = double.MaxValue;
                _ORCenter = 0;
                ORCompleted = false;

                // ---- Reset Initial Balance variables at session start ----
                _IBHigh = double.MinValue;
                _IBLow = double.MaxValue;
                _IBCenter = 0;
                IBCompleted = false;
            }
			
            #region Yesterday's Volume Profile Calculations (for YPOC, YVAH, YVAL)
            if (Bars.IsFirstBarOfSession && ShowYProfile)
            {
                sessionIterator.GetNextSession(Time[0], true);
                DateTime currentSessionStartTime = sessionIterator.ActualSessionBegin;
                int currentSessionStartIndex = Bars.GetBar(currentSessionStartTime);
			
                int previousBarIndex = currentSessionStartIndex - 1;
                if (previousBarIndex < 0)
                {
                    Print("Not enough historical bars to calculate the previous session.");
                    return;
                }
                sessionIterator.GetNextSession(Bars.GetTime(previousBarIndex), true);
                DateTime prevSessionStartTime = sessionIterator.ActualSessionBegin;
                DateTime prevSessionEndTime = sessionIterator.ActualSessionEnd;
                int prevSessionStartIndex = Bars.GetBar(prevSessionStartTime);
			
                int prevSessionEndIndex = -1;
                for (int i = prevSessionStartIndex; i < CurrentBar; i++)
                {
                    if (Bars.GetTime(i) < prevSessionEndTime)
                        prevSessionEndIndex = i;
                    else
                        break;
                }
                if (prevSessionEndIndex < prevSessionStartIndex)
                {
                    Print("No bars found in the previous session.");
                    return;
                }
			
                double sessionHighY = double.MinValue;
                double sessionLowY  = double.MaxValue;
                for (int i = prevSessionStartIndex; i <= prevSessionEndIndex; i++)
                {
                    DateTime barTime = Bars.GetTime(i);
                    if (barTime >= prevSessionStartTime && barTime <= prevSessionEndTime)
                    {
                        sessionHighY = Math.Max(sessionHighY, Bars.GetHigh(i));
                        sessionLowY  = Math.Min(sessionLowY, Bars.GetLow(i));
                    }
                }
			
                int ticksInRangeY = (int)Math.Round((sessionHighY - sessionLowY) / TickSize, 0) + 1;
                double[] priceLevelsY = new double[ticksInRangeY];
                double[] volumeHitsY  = new double[ticksInRangeY];
                for (int i = 0; i < ticksInRangeY; i++)
                {
                    priceLevelsY[i] = sessionLowY + i * TickSize;
                    volumeHitsY[i]  = 0;
                }
			
                double totalVolY = 0;
                for (int i = prevSessionStartIndex; i <= prevSessionEndIndex; i++)
                {
                    DateTime barTime = Bars.GetTime(i);
                    if (barTime < prevSessionStartTime || barTime > prevSessionEndTime)
                        continue;
			
                    double vol = Bars.GetVolume(i);
                    totalVolY += vol;
			
                    int ticksInBar = (int)Math.Round((High[i] - Low[i]) / TickSize + 1, 0);
                    if (ticksInBar < 1)
                        ticksInBar = 1;
                    double volPerTick = vol / (double)ticksInBar;
			
                    double upperLimit = Math.Min(High[i] + TickSize / 2.0, sessionHighY);
                    for (double price = Low[i]; price <= upperLimit; price += TickSize)
                    {
                        int index = (int)Math.Round((price - sessionLowY) / TickSize, 0);
                        if (index >= 0 && index < ticksInRangeY)
                            volumeHitsY[index] += volPerTick;
                    }
                }
                if (totalVolY <= 0)
                {
                    Print("No volume data in previous session.");
                    return;
                }
			
                double maxVolY = 0;
                int pocIndexY = 0;
                for (int i = 0; i < ticksInRangeY; i++)
                {
                    if (volumeHitsY[i] > maxVolY)
                    {
                        maxVolY = volumeHitsY[i];
                        pocIndexY = i;
                    }
                }
                YPOC = priceLevelsY[pocIndexY];
			
                double cumulativeVolY = volumeHitsY[pocIndexY];
                double lowerBoundY = YPOC;
                double upperBoundY = YPOC;
                int lowerPointerY = pocIndexY - 1;
                int upperPointerY = pocIndexY + 1;
                while (cumulativeVolY < totalVolY * ValueAreaPercent && (lowerPointerY >= 0 || upperPointerY < ticksInRangeY))
                {
                    if (lowerPointerY < 0)
                    {
                        cumulativeVolY += volumeHitsY[upperPointerY];
                        upperBoundY = priceLevelsY[upperPointerY];
                        upperPointerY++;
                    }
                    else if (upperPointerY >= ticksInRangeY)
                    {
                        cumulativeVolY += volumeHitsY[lowerPointerY];
                        lowerBoundY = priceLevelsY[lowerPointerY];
                        lowerPointerY--;
                    }
                    else
                    {
                        double volLower = volumeHitsY[lowerPointerY];
                        double volUpper = volumeHitsY[upperPointerY];
                        if (volLower >= volUpper)
                        {
                            cumulativeVolY += volLower;
                            lowerBoundY = priceLevelsY[lowerPointerY];
                            lowerPointerY--;
                        }
                        else
                        {
                            cumulativeVolY += volUpper;
                            upperBoundY = priceLevelsY[upperPointerY];
                            upperPointerY++;
                        }
                    }
                }
			
                if (YPOC > sessionHighY)
                    YPOC = sessionHighY;
                if (upperBoundY > sessionHighY)
                    upperBoundY = sessionHighY;
                if (lowerBoundY < sessionLowY)
                    lowerBoundY = sessionLowY;
                YVAH = upperBoundY;
                YVAL = lowerBoundY;
            }
            #endregion

            #region Today's Volume Profile (for TPOC Calculation)
            {
                sessionIterator.GetNextSession(Time[0], true);
                DateTime todaySessionStartTime = sessionIterator.ActualSessionBegin;
                int todaySessionStartIndex = Bars.GetBar(todaySessionStartTime);
                int todaySessionEndIndex = CurrentBar;

                double sessionHighT = double.MinValue;
                double sessionLowT = double.MaxValue;
                for (int i = todaySessionStartIndex; i <= todaySessionEndIndex; i++)
                {
                    DateTime barTime = Bars.GetTime(i);
                    if (barTime >= todaySessionStartTime)
                    {
                        sessionHighT = Math.Max(sessionHighT, Bars.GetHigh(i));
                        sessionLowT = Math.Min(sessionLowT, Bars.GetLow(i));
                    }
                }
                if (sessionHighT == double.MinValue || sessionLowT == double.MaxValue)
                {
                    Print("No bars found in today's session for TPOC calculation.");
                }
                else
                {
                    int ticksInRangeT = (int)Math.Round((sessionHighT - sessionLowT) / TickSize, 0) + 1;
                    double[] priceLevelsT = new double[ticksInRangeT];
                    double[] volumeHitsT = new double[ticksInRangeT];
                    for (int i = 0; i < ticksInRangeT; i++)
                    {
                        priceLevelsT[i] = sessionLowT + i * TickSize;
                        volumeHitsT[i] = 0;
                    }
                    
                    double totalVolT = 0;
                    for (int i = todaySessionStartIndex; i <= todaySessionEndIndex; i++)
                    {
                        DateTime barTime = Bars.GetTime(i);
                        if (barTime < todaySessionStartTime)
                            continue;
                        double vol = Bars.GetVolume(i);
                        totalVolT += vol;
                        int ticksInBar = (int)Math.Round((High[i] - Low[i]) / TickSize + 1, 0);
                        if (ticksInBar < 1)
                            ticksInBar = 1;
                        double volPerTick = vol / (double)ticksInBar;
                        double upperLimit = Math.Min(High[i] + TickSize / 2.0, sessionHighT);
                        for (double price = Low[i]; price <= upperLimit; price += TickSize)
                        {
                            int index = (int)Math.Round((price - sessionLowT) / TickSize, 0);
                            if (index >= 0 && index < ticksInRangeT)
                                volumeHitsT[index] += volPerTick;
                        }
                    }
                    if (totalVolT <= 0)
                    {
                        Print("No volume data in today's session for TPOC calculation.");
                    }
                    else
                    {
                        double maxVolT = 0;
                        int pocIndexT = 0;
                        for (int i = 0; i < ticksInRangeT; i++)
                        {
                            if (volumeHitsT[i] > maxVolT)
                            {
                                maxVolT = volumeHitsT[i];
                                pocIndexT = i;
                            }
                        }
                        TPOC = priceLevelsT[pocIndexT];
                    }
                }
            }
            #endregion
			
            DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
            DateTime sessionOpenDT  = tradingDay.Date.Add(OpenRangeStart.TimeOfDay);
            DateTime sessionOREndDT = tradingDay.Date.Add(OpenRangeEnd.TimeOfDay);
			
            if (Time[0] > sessionOpenDT && Time[0] <= sessionOREndDT)
            {
                _ORHigh = Math.Max(_ORHigh, High[0]);
                _ORLow = Math.Min(_ORLow, Low[0]);
            }
            else if (Time[0] > sessionOREndDT && !ORCompleted)
            {
                _ORCenter = (_ORHigh + _ORLow) / 2;
                ORCompleted = true;
            }

            DateTime sessionIBStartDT = tradingDay.Date.Add(IBStart.TimeOfDay);
            DateTime sessionIBEndDT = tradingDay.Date.Add(IBEnd.TimeOfDay);
            if (Time[0] > sessionIBStartDT && Time[0] <= sessionIBEndDT)
            {
                _IBHigh = Math.Max(_IBHigh, High[0]);
                _IBLow = Math.Min(_IBLow, Low[0]);
            }
            else if (Time[0] > sessionIBEndDT && !IBCompleted)
            {
                _IBCenter = (_IBHigh + _IBLow) / 2;
                IBCompleted = true;
            }

            // Update remaining key level values.
            YOpen = priorDayOHLC1.PriorOpen[0];
            YHigh = priorDayOHLC1.PriorHigh[0];
            YLow = priorDayOHLC1.PriorLow[0];
            YClose = priorDayOHLC1.PriorClose[0];

            TOpen = currentDayOHL1.CurrentOpen[0];
            THigh = currentDayOHL1.CurrentHigh[0];
            TLow = currentDayOHL1.CurrentLow[0];

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
                ORHigh = double.NaN;
                ORLow = double.NaN;
                ORCenter = double.NaN;
            }

            // Optionally update the internal key levels collection if needed.
            keyLevelsList.Clear();
            if (ShowYesterdayOHLC)
            {
                keyLevelsList.Add(new KeyLevel { Name = "yOpen", Value = YOpen });
                keyLevelsList.Add(new KeyLevel { Name = "yHigh", Value = YHigh });
                keyLevelsList.Add(new KeyLevel { Name = "yLow",  Value = YLow });
                keyLevelsList.Add(new KeyLevel { Name = "yClose", Value = YClose });
            }
            if (ShowYProfile)
            {
                keyLevelsList.Add(new KeyLevel { Name = "yPOC", Value = YPOC });
                keyLevelsList.Add(new KeyLevel { Name = "yVAH", Value = YVAH });
                keyLevelsList.Add(new KeyLevel { Name = "yVAL", Value = YVAL });
            }
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
            if (ShowInitialBalance && IBCompleted)
            {
                keyLevelsList.Add(new KeyLevel { Name = "IBHigh", Value = IBHigh });
                keyLevelsList.Add(new KeyLevel { Name = "IBLow", Value = IBLow });
                keyLevelsList.Add(new KeyLevel { Name = "IBCenter", Value = IBCenter });
            }
			
            // ***********************
            // Update plots for historical tracking.
            // If an optional group is not enabled, assign NaN so that the plot does not display.
            // ***********************
            Values[0][0] = ShowYesterdayOHLC ? YOpen : double.NaN;
            Values[1][0] = ShowYesterdayOHLC ? YHigh : double.NaN;
            Values[2][0] = ShowYesterdayOHLC ? YLow  : double.NaN;
            Values[3][0] = ShowYesterdayOHLC ? YClose : double.NaN;

            Values[4][0] = ShowYProfile ? YPOC : double.NaN;
            Values[5][0] = ShowYProfile ? YVAH : double.NaN;
            Values[6][0] = ShowYProfile ? YVAL : double.NaN;

            Values[7][0] = ShowTodayOHL ? TOpen : double.NaN;
            Values[8][0] = ShowTodayOHL ? THigh : double.NaN;
            Values[9][0] = ShowTodayOHL ? TLow : double.NaN;

            Values[10][0] = ShowTPOC ? TPOC : double.NaN;

            Values[11][0] = ShowTodayPivotPoints ? TPP : double.NaN;
            Values[12][0] = ShowTodayPivotPoints ? TR1 : double.NaN;
            Values[13][0] = ShowTodayPivotPoints ? TS1 : double.NaN;

            Values[14][0] = (ShowOpenRange && ORCompleted) ? ORHigh : double.NaN;
            Values[15][0] = (ShowOpenRange && ORCompleted) ? ORLow : double.NaN;
            Values[16][0] = (ShowOpenRange && ORCompleted) ? ORCenter : double.NaN;

            Values[17][0] = (ShowInitialBalance && IBCompleted) ? IBHigh : double.NaN;
            Values[18][0] = (ShowInitialBalance && IBCompleted) ? IBLow : double.NaN;
            Values[19][0] = (ShowInitialBalance && IBCompleted) ? IBCenter : double.NaN;
			
			
			int barsAgoOffset = 3; int verticalYOffsetTicks = 10;
			if (ShowYesterdayOHLC) { Draw.Text(this, "label_yOpen", false, "yOpen", barsAgoOffset, YOpen, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowYesterdayOHLC) { Draw.Text(this, "label_yHigh", false, "yHigh", barsAgoOffset, YHigh, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowYesterdayOHLC) { Draw.Text(this, "label_yLow", false, "yLow", barsAgoOffset, YLow, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowYesterdayOHLC) { Draw.Text(this, "label_yClose", false, "yClose", barsAgoOffset, YClose, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowYProfile) { Draw.Text(this, "label_yPOC", false, "yPOC", barsAgoOffset, YPOC, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowYProfile) { Draw.Text(this, "label_yVAH", false, "yVAH", barsAgoOffset, YVAH, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowYProfile) { Draw.Text(this, "label_yVAL", false, "yVAL", barsAgoOffset, YVAL, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowTodayOHL) { Draw.Text(this, "label_tOpen", false, "tOpen", barsAgoOffset, TOpen, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowTodayOHL) { Draw.Text(this, "label_tHigh", false, "tHigh", barsAgoOffset, THigh, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowTodayOHL) { Draw.Text(this, "label_tLow", false, "tLow", barsAgoOffset, TLow, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowTPOC) { Draw.Text(this, "label_tPOC", false, "tPOC", barsAgoOffset, TPOC, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowTodayPivotPoints) { Draw.Text(this, "label_tPP", false, "tPP", barsAgoOffset, TPP, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowTodayPivotPoints) { Draw.Text(this, "label_tR1", false, "tR1", barsAgoOffset, TR1, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowTodayPivotPoints) { Draw.Text(this, "label_tS1", false, "tS1", barsAgoOffset, TS1, verticalYOffsetTicks, KeyLevelColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowOpenRange && ORCompleted) { Draw.Text(this, "label_ORHigh", false, "ORHigh", barsAgoOffset, ORHigh, verticalYOffsetTicks, OpenRangeColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowOpenRange && ORCompleted) { Draw.Text(this, "label_ORLow", false, "ORLow", barsAgoOffset, ORLow, verticalYOffsetTicks, OpenRangeColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowOpenRange && ORCompleted) { Draw.Text(this, "label_ORCenter", false, "ORCenter", barsAgoOffset, ORCenter, verticalYOffsetTicks, OpenRangeColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			
			if (ShowInitialBalance && IBCompleted) { Draw.Text(this, "label_IBHigh", false, "IBHigh", barsAgoOffset, IBHigh, verticalYOffsetTicks, IBColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowInitialBalance && IBCompleted) { Draw.Text(this, "label_IBLow", false, "IBLow", barsAgoOffset, IBLow, verticalYOffsetTicks, IBColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }
			if (ShowInitialBalance && IBCompleted) { Draw.Text(this, "label_IBCenter", false, "IBCenter", barsAgoOffset, IBCenter, verticalYOffsetTicks, IBColor, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0); }

			
			
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private KeyLevels[] cacheKeyLevels;
		public KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return KeyLevels(Input, drawLevels, keyLevelColor, openRangeColor, iBColor, showYProfile, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public KeyLevels KeyLevels(ISeries<double> input, bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			if (cacheKeyLevels != null)
				for (int idx = 0; idx < cacheKeyLevels.Length; idx++)
					if (cacheKeyLevels[idx] != null && cacheKeyLevels[idx].DrawLevels == drawLevels && cacheKeyLevels[idx].KeyLevelColor == keyLevelColor && cacheKeyLevels[idx].OpenRangeColor == openRangeColor && cacheKeyLevels[idx].IBColor == iBColor && cacheKeyLevels[idx].ShowYProfile == showYProfile && cacheKeyLevels[idx].ShowTPOC == showTPOC && cacheKeyLevels[idx].ShowOpenRange == showOpenRange && cacheKeyLevels[idx].ShowYesterdayOHLC == showYesterdayOHLC && cacheKeyLevels[idx].ShowTodayOHL == showTodayOHL && cacheKeyLevels[idx].ShowTodayPivotPoints == showTodayPivotPoints && cacheKeyLevels[idx].ShowInitialBalance == showInitialBalance && cacheKeyLevels[idx].OpenRangeStart == openRangeStart && cacheKeyLevels[idx].OpenRangeEnd == openRangeEnd && cacheKeyLevels[idx].IBStart == iBStart && cacheKeyLevels[idx].IBEnd == iBEnd && cacheKeyLevels[idx].EqualsInput(input))
						return cacheKeyLevels[idx];
			return CacheIndicator<KeyLevels>(new KeyLevels(){ DrawLevels = drawLevels, KeyLevelColor = keyLevelColor, OpenRangeColor = openRangeColor, IBColor = iBColor, ShowYProfile = showYProfile, ShowTPOC = showTPOC, ShowOpenRange = showOpenRange, ShowYesterdayOHLC = showYesterdayOHLC, ShowTodayOHL = showTodayOHL, ShowTodayPivotPoints = showTodayPivotPoints, ShowInitialBalance = showInitialBalance, OpenRangeStart = openRangeStart, OpenRangeEnd = openRangeEnd, IBStart = iBStart, IBEnd = iBEnd }, input, ref cacheKeyLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor, openRangeColor, iBColor, showYProfile, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor, openRangeColor, iBColor, showYProfile, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(Input, drawLevels, keyLevelColor, openRangeColor, iBColor, showYProfile, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLevels, Brush keyLevelColor, Brush openRangeColor, Brush iBColor, bool showYProfile, bool showTPOC, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd)
		{
			return indicator.KeyLevels(input, drawLevels, keyLevelColor, openRangeColor, iBColor, showYProfile, showTPOC, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, openRangeStart, openRangeEnd, iBStart, iBEnd);
		}
	}
}

#endregion
