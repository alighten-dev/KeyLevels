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
//        [Browsable(false)]
//        [XmlIgnore]
//        public double YPOC { get; private set; }
//        [Browsable(false)]
//        [XmlIgnore]
//        public double YVAH { get; private set; }
//        [Browsable(false)]
//        [XmlIgnore]
//        public double YVAL { get; private set; }

        [Browsable(false)]
        [XmlIgnore]
        public double TOpen { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double THigh { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double TLow { get; private set; }
//        [Browsable(false)]
//        [XmlIgnore]
//        public double TPOC { get; private set; }
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

        // ---- New London Session Public Properties ----
        [Browsable(false)]
        [XmlIgnore]
        public double LondonOpen { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double LondonHigh { get; private set; }
        [Browsable(false)]
        [XmlIgnore]
        public double LondonLow { get; private set; }
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

        // ---- Variables for the London Session ----
        private double _londonOpen = double.NaN;
        private double _londonHigh = double.MinValue;
        private double _londonLow = double.MaxValue;
        private bool londonSessionActive = false;
        private bool londonSessionProcessedForDay = false; // Flag to ensure open is set only once per session

        // Optional key levels collection (if needed for internal use)
        private List<KeyLevel> keyLevelsList = new List<KeyLevel>();

        // Occupied price levels for label spacing (currently unused but kept from original)
        List<double> occupiedPriceLevels = new List<double>();

        #endregion

        #region Indicator Parameters

        [NinjaScriptProperty]
        [Display(Name = "Draw Labels", Description = "Draw text labels for each key level", Order = 1, GroupName = "Parameters")]
        public bool DrawLabels { get; set; } = true;

        // --- Color Parameters ---
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Y OHLC Color", Order = 2, GroupName = "Parameters")]
        public Brush YOHLCColor { get; set; } = Brushes.Magenta;
        [Browsable(false)]
        public string YOHLCColorSerialize
        {
            get { return Serialize.BrushToString(YOHLCColor); }
            set { YOHLCColor = Serialize.StringToBrush(value); }
        }

//        [XmlIgnore]
//        [NinjaScriptProperty]
//        [Display(Name = "Y Profile Color", Order = 3, GroupName = "Parameters")]
//        public Brush YProfileColor { get; set; } = Brushes.DarkGoldenrod;
//        [Browsable(false)]
//        public string YProfileColorSerialize
//        {
//            get { return Serialize.BrushToString(YProfileColor); }
//            set { YProfileColor = Serialize.StringToBrush(value); }
//        }

        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "T OHL Color", Order = 4, GroupName = "Parameters")]
        public Brush TOHLColor { get; set; } = Brushes.LimeGreen;
        [Browsable(false)]
        public string TOHLColorSerialize
        {
            get { return Serialize.BrushToString(TOHLColor); }
            set { TOHLColor = Serialize.StringToBrush(value); }
        }

//        [XmlIgnore]
//        [NinjaScriptProperty]
//        [Display(Name = "T POC Color", Order = 5, GroupName = "Parameters")]
//        public Brush TPOCColor { get; set; } = Brushes.OrangeRed;
//        [Browsable(false)]
//        public string TPOCColorSerialize
//        {
//            get { return Serialize.BrushToString(TPOCColor); }
//            set { TPOCColor = Serialize.StringToBrush(value); }
//        }

        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "T Pivot Color", Order = 6, GroupName = "Parameters")]
        public Brush TPivotColor { get; set; } = Brushes.YellowGreen;
        [Browsable(false)]
        public string TPivotColorSerialize
        {
            get { return Serialize.BrushToString(TPivotColor); }
            set { TPivotColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Open Range Color", Order = 7, GroupName = "Parameters")]
        public Brush OpenRangeColor { get; set; } = Brushes.DeepPink;
        [Browsable(false)]
        public string OpenRangeColorSerialize
        {
            get { return Serialize.BrushToString(OpenRangeColor); }
            set { OpenRangeColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Initial Balance Color", Order = 8, GroupName = "Parameters")]
        public Brush IBColor { get; set; } = Brushes.Cyan;
        [Browsable(false)]
        public string IBColorSerialize
        {
            get { return Serialize.BrushToString(IBColor); }
            set { IBColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "London Session Color", Order = 9, GroupName = "Parameters")]
        public Brush LondonSessionColor { get; set; } = Brushes.DodgerBlue;
        [Browsable(false)]
        public string LondonSessionColorSerialize
        {
            get { return Serialize.BrushToString(LondonSessionColor); }
            set { LondonSessionColor = Serialize.StringToBrush(value); }
        }


        /////////// Checkbox Parameters for Groups ///////////
//        [NinjaScriptProperty]
//        [Display(Name = "Show Yesterday's POC/VAH/VAL", Order = 10, GroupName = "Display Groups")]
//        public bool ShowYProfile { get; set; } = true;

//        [NinjaScriptProperty]
//        [Display(Name = "Show Today's POC", Order = 12, GroupName = "Display Groups")]
//        public bool ShowTPOC { get; set; } = true;

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

        [NinjaScriptProperty]
        [Display(Name = "Show Initial Balance", Order = 17, GroupName = "Display Groups")]
        public bool ShowInitialBalance { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show London Session", Order = 18, GroupName = "Display Groups")]
        public bool ShowLondonSession { get; set; } = true;


        /////////// Configurable Session Times ///////////
        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range Start Time", Order = 20, GroupName = "Session Settings", Description = "Configure the start time for the Open Range period")]
        public DateTime OpenRangeStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Open Range End Time", Order = 21, GroupName = "Session Settings", Description = "Configure the end time for the Open Range period")]
        public DateTime OpenRangeEnd { get; set; } = DateTime.Parse("09:35", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance Start Time", Order = 22, GroupName = "Session Settings", Description = "Configure the start time for the Initial Balance period")]
        public DateTime IBStart { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Initial Balance End Time", Order = 23, GroupName = "Session Settings", Description = "Configure the end time for the Initial Balance period")]
        public DateTime IBEnd { get; set; } = DateTime.Parse("10:30", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "London Session Start Time", Order = 24, GroupName = "Session Settings", Description = "Configure the start time for the London Session period")]
        public DateTime LondonSessionStart { get; set; } = DateTime.Parse("3:00", System.Globalization.CultureInfo.InvariantCulture);

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "London Session End Time", Order = 25, GroupName = "Session Settings", Description = "Configure the end time for the London Session period")]
        public DateTime LondonSessionEnd { get; set; } = DateTime.Parse("9:30", System.Globalization.CultureInfo.InvariantCulture);

        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "KeyLevels";
                Description = "Combines previous day OHLC, POC, VAH, and VAL with the current day's OHL, Pivot Point, R1, S1, POC, Opening Range, Initial Balance and London Session levels into a single collection of key levels. - By Alighten (Modified)";
                Calculate = Calculate.OnEachTick; // Required for dynamic updates of London/OR/IB
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                IsAutoScale = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
                BarsRequiredToPlot = 1;


                // Add plots for each key level.
                // Indices:
                // 0: YOpen, 1: YHigh, 2: YLow, 3: YClose         (YOHLCColor)
                // 4: YPOC, 5: YVAH, 6: YVAL                     (YProfileColor)
                // 7: TOpen, 8: THigh, 9: TLow                     (TOHLColor)
                // 10: TPOC                                       (TPOCColor)
                // 11: TPP, 12: TR1, 13: TS1                      (TPivotColor)
                // 14: ORHigh, 15: ORLow, 16: ORCenter            (OpenRangeColor)
                // 17: IBHigh, 18: IBLow, 19: IBCenter            (IBColor)
                // 20: LondonHigh, 21: LondonLow, 22: LondonOpen (LondonSessionColor)

                // Yesterday OHLC
                AddPlot(new Stroke(YOHLCColor), PlotStyle.Line, "YOpen");
                AddPlot(new Stroke(YOHLCColor), PlotStyle.Line, "YHigh");
                AddPlot(new Stroke(YOHLCColor), PlotStyle.Line, "YLow");
                AddPlot(new Stroke(YOHLCColor), PlotStyle.Line, "YClose");

                // Yesterday Profile
//                AddPlot(new Stroke(YProfileColor), PlotStyle.Line, "YPOC");
//                AddPlot(new Stroke(YProfileColor), PlotStyle.Line, "YVAH");
//                AddPlot(new Stroke(YProfileColor), PlotStyle.Line, "YVAL");

                // Today OHL
                AddPlot(new Stroke(TOHLColor), PlotStyle.Line, "TOpen");
                AddPlot(new Stroke(TOHLColor), PlotStyle.Line, "THigh");
                AddPlot(new Stroke(TOHLColor), PlotStyle.Line, "TLow");

                // Today POC
//                AddPlot(new Stroke(TPOCColor), PlotStyle.Line, "TPOC");

                // Today Pivots
                AddPlot(new Stroke(TPivotColor), PlotStyle.Line, "TPP");
                AddPlot(new Stroke(TPivotColor), PlotStyle.Line, "TR1");
                AddPlot(new Stroke(TPivotColor), PlotStyle.Line, "TS1");

                // Open Range
                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORHigh");
                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORLow");
                AddPlot(new Stroke(OpenRangeColor), PlotStyle.Line, "ORCenter");

                // Initial Balance
                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBHigh");
                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBLow");
                AddPlot(new Stroke(IBColor), PlotStyle.Line, "IBCenter");

                // London Session (Dynamic)
                AddPlot(new Stroke(LondonSessionColor), PlotStyle.Line, "LondonHigh");
                AddPlot(new Stroke(LondonSessionColor), PlotStyle.Line, "LondonLow");
                AddPlot(new Stroke(LondonSessionColor), PlotStyle.Line, "LondonOpen");
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

            // --- Session Start Reset ---
            if (Bars.IsFirstBarOfSession)
            {
                firstBarOfSession = CurrentBar;
//                YPOC = 0;
//                YVAL = 0;
//                YVAH = 0;

                _ORHigh = double.MinValue;
                _ORLow = double.MaxValue;
                _ORCenter = 0;
                ORCompleted = false;

                _IBHigh = double.MinValue;
                _IBLow = double.MaxValue;
                _IBCenter = 0;
                IBCompleted = false;

                // Reset London Session variables at session start
//                _londonOpen = double.NaN;
//                _londonHigh = double.MinValue;
//                _londonLow = double.MaxValue;
                londonSessionActive = false;
                londonSessionProcessedForDay = false; // Allow processing for the new day
            }

            #region Yesterday's Volume Profile Calculations (for YPOC, YVAH, YVAL)
            // This calculation only needs to run once per session
//            if (Bars.IsFirstBarOfSession && ShowYProfile && YPOC == 0) // Check YPOC==0 to avoid recalc if already done
//            {
//                sessionIterator.GetNextSession(Time[0], true);
//                DateTime currentSessionStartTime = sessionIterator.ActualSessionBegin;
//                int currentSessionStartIndex = Bars.GetBar(currentSessionStartTime);

//                int previousBarIndex = currentSessionStartIndex - 1;
//                if (previousBarIndex < 0)
//                {
//                    Print("Not enough historical bars to calculate the previous session.");
//                }
//                else
//                {
//                    sessionIterator.GetNextSession(Bars.GetTime(previousBarIndex), true);
//                    DateTime prevSessionStartTime = sessionIterator.ActualSessionBegin;
//                    DateTime prevSessionEndTime = sessionIterator.ActualSessionEnd;
//                    int prevSessionStartIndex = Bars.GetBar(prevSessionStartTime);

//                    int prevSessionEndIndex = -1;
//                    for (int i = prevSessionStartIndex; i < CurrentBar; i++)
//                    {
//                        if (Bars.GetTime(i) < prevSessionEndTime)
//                            prevSessionEndIndex = i;
//                        else
//                            break;
//                    }
//                    if (prevSessionEndIndex < prevSessionStartIndex)
//                    {
//                        Print("No bars found in the previous session.");
//                    }
//                    else
//                    {
//                        double sessionHighY = double.MinValue;
//                        double sessionLowY = double.MaxValue;
//                        for (int i = prevSessionStartIndex; i <= prevSessionEndIndex; i++)
//                        {
//                            DateTime barTime = Bars.GetTime(i);
//                            if (barTime >= prevSessionStartTime && barTime <= prevSessionEndTime)
//                            {
//                                sessionHighY = Math.Max(sessionHighY, Bars.GetHigh(i));
//                                sessionLowY = Math.Min(sessionLowY, Bars.GetLow(i));
//                            }
//                        }

//                        if (sessionHighY > double.MinValue && sessionLowY < double.MaxValue) // Check if valid range found
//                        {
//                            int ticksInRangeY = (int)Math.Round((sessionHighY - sessionLowY) / TickSize, 0) + 1;
//                            double[] priceLevelsY = new double[ticksInRangeY];
//                            double[] volumeHitsY = new double[ticksInRangeY];
//                            for (int i = 0; i < ticksInRangeY; i++)
//                            {
//                                priceLevelsY[i] = sessionLowY + i * TickSize;
//                                volumeHitsY[i] = 0;
//                            }

//                            double totalVolY = 0;
//                            for (int i = prevSessionStartIndex; i <= prevSessionEndIndex; i++)
//                            {
//                                DateTime barTime = Bars.GetTime(i);
//                                if (barTime < prevSessionStartTime || barTime > prevSessionEndTime)
//                                    continue;

//                                double vol = Bars.GetVolume(i);
//                                totalVolY += vol;

//                                int ticksInBar = (int)Math.Round((High[i] - Low[i]) / TickSize + 1, 0);
//                                if (ticksInBar < 1)
//                                    ticksInBar = 1;
//                                double volPerTick = (ticksInBar > 0) ? vol / (double)ticksInBar : 0; // Avoid division by zero

//                                double upperLimit = Math.Min(High[i] + TickSize / 2.0, sessionHighY);
//                                for (double price = Low[i]; price <= upperLimit; price += TickSize)
//                                {
//                                    int index = (int)Math.Round((price - sessionLowY) / TickSize, 0);
//                                    if (index >= 0 && index < ticksInRangeY)
//                                        volumeHitsY[index] += volPerTick;
//                                }
//                            }
//                            if (totalVolY <= 0)
//                            {
//                                Print("No volume data in previous session.");
//                            }
//                            else
//                            {
//                                double maxVolY = 0;
//                                int pocIndexY = 0;
//                                for (int i = 0; i < ticksInRangeY; i++)
//                                {
//                                    if (volumeHitsY[i] > maxVolY)
//                                    {
//                                        maxVolY = volumeHitsY[i];
//                                        pocIndexY = i;
//                                    }
//                                }
//                                YPOC = priceLevelsY[pocIndexY];

//                                double cumulativeVolY = volumeHitsY[pocIndexY];
//                                double lowerBoundY = YPOC;
//                                double upperBoundY = YPOC;
//                                int lowerPointerY = pocIndexY - 1;
//                                int upperPointerY = pocIndexY + 1;
//                                while (cumulativeVolY < totalVolY * ValueAreaPercent && (lowerPointerY >= 0 || upperPointerY < ticksInRangeY))
//                                {
//                                    if (lowerPointerY < 0 && upperPointerY < ticksInRangeY) // Only upper available
//                                    {
//                                        cumulativeVolY += volumeHitsY[upperPointerY];
//                                        upperBoundY = priceLevelsY[upperPointerY];
//                                        upperPointerY++;
//                                    }
//                                    else if (upperPointerY >= ticksInRangeY && lowerPointerY >= 0) // Only lower available
//                                    {
//                                        cumulativeVolY += volumeHitsY[lowerPointerY];
//                                        lowerBoundY = priceLevelsY[lowerPointerY];
//                                        lowerPointerY--;
//                                    }
//                                    else if (lowerPointerY >= 0 && upperPointerY < ticksInRangeY) // Both available
//                                    {
//                                        double volLower = volumeHitsY[lowerPointerY];
//                                        double volUpper = volumeHitsY[upperPointerY];
//                                        if (volLower >= volUpper)
//                                        {
//                                            cumulativeVolY += volLower;
//                                            lowerBoundY = priceLevelsY[lowerPointerY];
//                                            lowerPointerY--;
//                                        }
//                                        else
//                                        {
//                                            cumulativeVolY += volUpper;
//                                            upperBoundY = priceLevelsY[upperPointerY];
//                                            upperPointerY++;
//                                        }
//                                    }
//                                    else // Neither pointer is valid, break loop
//                                    {
//                                        break;
//                                    }
//                                }

//                                // Ensure bounds are within session range
//                                YPOC = Math.Max(sessionLowY, Math.Min(sessionHighY, YPOC));
//                                YVAH = Math.Max(sessionLowY, Math.Min(sessionHighY, upperBoundY));
//                                YVAL = Math.Max(sessionLowY, Math.Min(sessionHighY, lowerBoundY));
//                            }
//                        }
//                        else
//                        {
//                             Print("Could not determine valid High/Low range for previous session.");
//                        }
//                    }
//                }
//            }
            #endregion

            #region Today's Volume Profile (for TPOC Calculation)
            // This calculation runs on every update to reflect current TPOC
//            if (ShowTPOC)
//            {
//                sessionIterator.GetNextSession(Time[0], true);
//                DateTime todaySessionStartTime = sessionIterator.ActualSessionBegin;
//                int todaySessionStartIndex = Bars.GetBar(todaySessionStartTime);
//                int todaySessionEndIndex = CurrentBar; // Use current bar as end index

//                if (todaySessionStartIndex >= 0 && todaySessionEndIndex >= todaySessionStartIndex) // Ensure valid indices
//                {
//                    double sessionHighT = double.MinValue;
//                    double sessionLowT = double.MaxValue;
//                    for (int i = todaySessionStartIndex; i <= todaySessionEndIndex; i++)
//                    {
//                        DateTime barTime = Bars.GetTime(i);
//                        if (barTime >= todaySessionStartTime) // Ensure bar is within current session
//                        {
//                            sessionHighT = Math.Max(sessionHighT, Bars.GetHigh(i));
//                            sessionLowT = Math.Min(sessionLowT, Bars.GetLow(i));
//                        }
//                    }

//                    if (sessionHighT == double.MinValue || sessionLowT == double.MaxValue)
//                    {
//                        // Not enough data yet in today's session
//                        TPOC = double.NaN; // Set to NaN if no valid range
//                    }
//                    else
//                    {
//                        int ticksInRangeT = (int)Math.Round((sessionHighT - sessionLowT) / TickSize, 0) + 1;
//                        if (ticksInRangeT <= 0) ticksInRangeT = 1; // Handle zero range case

//                        double[] priceLevelsT = new double[ticksInRangeT];
//                        double[] volumeHitsT = new double[ticksInRangeT];
//                        for (int i = 0; i < ticksInRangeT; i++)
//                        {
//                            priceLevelsT[i] = sessionLowT + i * TickSize; // Index 0 = sessionLowT
//                            volumeHitsT[i] = 0;
//                        }

//                        double totalVolT = 0;
//                        for (int i = todaySessionStartIndex; i <= todaySessionEndIndex; i++)
//                        {
//                            DateTime barTime = Bars.GetTime(i);
//                            if (barTime < todaySessionStartTime) // Skip bars before session start
//                                continue;

//                            double vol = Bars.GetVolume(i);
//                            totalVolT += vol;
//                            int ticksInBar = (int)Math.Round((High[i] - Low[i]) / TickSize + 1, 0);
//                            if (ticksInBar < 1)
//                                ticksInBar = 1;
//                            double volPerTick = (ticksInBar > 0) ? vol / (double)ticksInBar : 0; // Avoid division by zero

//                            // Refined upper limit for volume distribution loop
//                            double upperLimit = Math.Min(High[i], sessionHighT);

//                            for (double price = Low[i]; price <= upperLimit; price += TickSize)
//                            {
//                                int index = (int)Math.Round((price - sessionLowT) / TickSize, 0);
//                                if (index >= 0 && index < ticksInRangeT)
//                                    volumeHitsT[index] += volPerTick;
//                            }
//                        }

//                        if (totalVolT <= 0)
//                        {
//                            TPOC = double.NaN; // No volume yet
//                        }
//                        else
//                        {
//                            double maxVolT = 0;
//                            int pocIndexT = 0; // Initialized to 0
//                            for (int i = 0; i < ticksInRangeT; i++)
//                            {
//                                // This finds the FIRST index (lowest price) that has the maximum volume
//                                if (volumeHitsT[i] > maxVolT)
//                                {
//                                    maxVolT = volumeHitsT[i];
//                                    pocIndexT = i;
//                                }
//                            }
//                            // Ensure POC index is valid before accessing priceLevelsT
//                            if (pocIndexT >= 0 && pocIndexT < ticksInRangeT)
//                            {
//                                TPOC = priceLevelsT[pocIndexT]; // Assign price based on the found index
//                            }
//                            else
//                            {
//                                TPOC = double.NaN; // Should not happen if ticksInRangeT > 0 and volume > 0
//                            }
//                        }
//                    }
//                }
//                else
//                {
//                    TPOC = double.NaN; // Session start index not found or invalid
//                }
//            }
//            else
//            {
//                TPOC = double.NaN; // Not showing TPOC
//            }
            #endregion

            // --- Get Current Trading Day and Session Times ---
            DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
            DateTime sessionOpenDT = tradingDay.Date.Add(OpenRangeStart.TimeOfDay);
            DateTime sessionOREndDT = tradingDay.Date.Add(OpenRangeEnd.TimeOfDay);
            DateTime sessionIBStartDT = tradingDay.Date.Add(IBStart.TimeOfDay);
            DateTime sessionIBEndDT = tradingDay.Date.Add(IBEnd.TimeOfDay);
            DateTime sessionLondonStartDT = tradingDay.Date.Add(LondonSessionStart.TimeOfDay);
            DateTime sessionLondonEndDT = tradingDay.Date.Add(LondonSessionEnd.TimeOfDay);

            // --- Opening Range Calculation ---
            if (ShowOpenRange)
            {
                // Check if within OR time window
                if (Time[0] >= sessionOpenDT && Time[0] <= sessionOREndDT)
                {
                    _ORHigh = Math.Max(_ORHigh, High[0]);
                    _ORLow = Math.Min(_ORLow, Low[0]);
                    ORCompleted = false; // Still developing
                }
                // Check if OR time has passed and it wasn't completed yet
                else if (Time[0] > sessionOREndDT && !ORCompleted)
                {
                    if (_ORHigh > double.MinValue && _ORLow < double.MaxValue) // Ensure valid high/low were found
                    {
                         _ORCenter = (_ORHigh + _ORLow) / 2;
                    }
                    else // Handle case where no bars fell in the OR window
                    {
                        _ORHigh = double.NaN;
                        _ORLow = double.NaN;
                        _ORCenter = double.NaN;
                    }
                    ORCompleted = true;
                }
            }

            // --- Initial Balance Calculation ---
            if (ShowInitialBalance)
            {
                // Check if within IB time window
                if (Time[0] >= sessionIBStartDT && Time[0] <= sessionIBEndDT)
                {
                    _IBHigh = Math.Max(_IBHigh, High[0]);
                    _IBLow = Math.Min(_IBLow, Low[0]);
                    IBCompleted = false; // Still developing
                }
                // Check if IB time has passed and it wasn't completed yet
                else if (Time[0] > sessionIBEndDT && !IBCompleted)
                {
                     if (_IBHigh > double.MinValue && _IBLow < double.MaxValue) // Ensure valid high/low were found
                    {
                        _IBCenter = (_IBHigh + _IBLow) / 2;
                    }
                    else // Handle case where no bars fell in the IB window
                    {
                        _IBHigh = double.NaN;
                        _IBLow = double.NaN;
                        _IBCenter = double.NaN;
                    }
                    IBCompleted = true;
                }
            }

            // --- London Session Dynamic Calculation ---
            if (ShowLondonSession)
            {
                // Check if within London time window
                if (Time[0] >= sessionLondonStartDT && Time[0] <= sessionLondonEndDT)
                {
                    if (!londonSessionProcessedForDay) // First bar within the London session for this day
                    {
                        _londonOpen = Open[0];
                        _londonHigh = High[0]; // Initialize with first bar's H/L
                        _londonLow = Low[0];
                        londonSessionActive = true;
                        londonSessionProcessedForDay = true; // Mark as processed for this day
                    }
                    else if (londonSessionActive) // Subsequent bars within the session
                    {
                        _londonHigh = Math.Max(_londonHigh, High[0]);
                        _londonLow = Math.Min(_londonLow, Low[0]);
                        // _londonOpen remains the same
                    }
                }
                // Check if London time has passed
                else if (Time[0] > sessionLondonEndDT)
                {
                    londonSessionActive = false; // Session is over for the day
                    // Keep the last calculated values (_londonHigh, _londonLow, _londonOpen)
                }
            }


            // --- Update remaining key level values from helper indicators ---
            YOpen = priorDayOHLC1.PriorOpen[0];
            YHigh = priorDayOHLC1.PriorHigh[0];
            YLow = priorDayOHLC1.PriorLow[0];
            YClose = priorDayOHLC1.PriorClose[0];

            TOpen = currentDayOHL1.CurrentOpen[0];
            THigh = currentDayOHL1.CurrentHigh[0];
            TLow = currentDayOHL1.CurrentLow[0];
			
			pivots1.Update();
            TPP = pivots1.Pp[0];
            TR1 = pivots1.R1[0];
            TS1 = pivots1.S1[0];

            // --- Assign final OR/IB values to public properties (after completion) ---
            ORHigh = (ORCompleted && _ORHigh > double.MinValue) ? _ORHigh : double.NaN;
            ORLow = (ORCompleted && _ORLow < double.MaxValue) ? _ORLow : double.NaN;
            ORCenter = (ORCompleted && !double.IsNaN(_ORCenter)) ? _ORCenter : double.NaN;

            // Public IB properties are handled via getter logic checking IBCompleted

            // --- Assign London values to public properties (dynamic) ---
            // These are updated continuously while active or hold last value after session ends
            LondonOpen = !double.IsNaN(_londonOpen) ? _londonOpen : double.NaN;
            LondonHigh = _londonHigh != double.MinValue ? _londonHigh : double.NaN;
            LondonLow = _londonLow != double.MaxValue ? _londonLow : double.NaN;


            // --- Optionally update the internal key levels collection (if needed) ---
            keyLevelsList.Clear();
            if (ShowYesterdayOHLC)
            {
                keyLevelsList.Add(new KeyLevel { Name = "yOpen", Value = YOpen });
                keyLevelsList.Add(new KeyLevel { Name = "yHigh", Value = YHigh });
                keyLevelsList.Add(new KeyLevel { Name = "yLow", Value = YLow });
                keyLevelsList.Add(new KeyLevel { Name = "yClose", Value = YClose });
            }
//            if (ShowYProfile)
//            {
//                keyLevelsList.Add(new KeyLevel { Name = "yPOC", Value = YPOC });
//                keyLevelsList.Add(new KeyLevel { Name = "yVAH", Value = YVAH });
//                keyLevelsList.Add(new KeyLevel { Name = "yVAL", Value = YVAL });
//            }
            if (ShowTodayOHL)
            {
                keyLevelsList.Add(new KeyLevel { Name = "tOpen", Value = TOpen });
                keyLevelsList.Add(new KeyLevel { Name = "tHigh", Value = THigh });
                keyLevelsList.Add(new KeyLevel { Name = "tLow", Value = TLow });
            }
//            if (ShowTPOC)
//                keyLevelsList.Add(new KeyLevel { Name = "tPOC", Value = TPOC });
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
                keyLevelsList.Add(new KeyLevel { Name = "IBHigh", Value = IBHigh }); // Uses getter
                keyLevelsList.Add(new KeyLevel { Name = "IBLow", Value = IBLow });   // Uses getter
                keyLevelsList.Add(new KeyLevel { Name = "IBCenter", Value = IBCenter }); // Uses getter
            }
            if (ShowLondonSession && londonSessionProcessedForDay) // Add London levels if session has started
            {
                keyLevelsList.Add(new KeyLevel { Name = "LonOpen", Value = LondonOpen });
                keyLevelsList.Add(new KeyLevel { Name = "LonHigh", Value = LondonHigh });
                keyLevelsList.Add(new KeyLevel { Name = "LonLow", Value = LondonLow });
            }

            // ***********************
            // Update plots for historical tracking.
            // If an optional group is not enabled, assign NaN so that the plot does not display.
            // ***********************
            // Indices: 0-3 YOHLC, 4-6 YProfile, 7-9 TOHL, 10 TPOC, 11-13 TPivot, 14-16 OR, 17-19 IB, 20-22 London
            Values[0][0] = ShowYesterdayOHLC ? YOpen : double.NaN;
            Values[1][0] = ShowYesterdayOHLC ? YHigh : double.NaN;
            Values[2][0] = ShowYesterdayOHLC ? YLow : double.NaN;
            Values[3][0] = ShowYesterdayOHLC ? YClose : double.NaN;

//            Values[4][0] = ShowYProfile ? YPOC : double.NaN;
//            Values[5][0] = ShowYProfile ? YVAH : double.NaN;
//            Values[6][0] = ShowYProfile ? YVAL : double.NaN;

            Values[4][0] = ShowTodayOHL ? TOpen : double.NaN;
            Values[5][0] = ShowTodayOHL ? THigh : double.NaN;
            Values[6][0] = ShowTodayOHL ? TLow : double.NaN;

//            Values[10][0] = ShowTPOC ? TPOC : double.NaN;

            Values[7][0] = ShowTodayPivotPoints ? TPP : double.NaN;
            Values[8][0] = ShowTodayPivotPoints ? TR1 : double.NaN;
            Values[9][0] = ShowTodayPivotPoints ? TS1 : double.NaN;

            Values[10][0] = (ShowOpenRange && ORCompleted) ? ORHigh : double.NaN;
            Values[11][0] = (ShowOpenRange && ORCompleted) ? ORLow : double.NaN;
            Values[12][0] = (ShowOpenRange && ORCompleted) ? ORCenter : double.NaN;

            Values[13][0] = (ShowInitialBalance && IBCompleted) ? IBHigh : double.NaN; // Uses getter
            Values[14][0] = (ShowInitialBalance && IBCompleted) ? IBLow : double.NaN;  // Uses getter
            Values[15][0] = (ShowInitialBalance && IBCompleted) ? IBCenter : double.NaN;// Uses getter

            // London plots update dynamically
            Values[16][0] = ShowLondonSession && !double.IsNaN(LondonHigh) ? LondonHigh : double.NaN;
            Values[17][0] = ShowLondonSession && !double.IsNaN(LondonLow) ? LondonLow : double.NaN;
            Values[18][0] = ShowLondonSession && !double.IsNaN(LondonOpen) ? LondonOpen : double.NaN;


            // --- Draw Text Labels ---
            if (DrawLabels)
            {
                int barsAgoOffset = 3; int verticalYOffsetTicks = 8; // Reduced offset slightly

                // Helper function to draw text if value is valid
                Action<string, double, Brush, string> DrawLevelText = (tag, value, color, label) =>
                {
                    if (!double.IsNaN(value) && value != 0 && value != double.MinValue && value != double.MaxValue) // Check for valid plot values
                    {
                        Draw.Text(this, tag, false, label, barsAgoOffset, value, verticalYOffsetTicks, color, myFont, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
                    }
                    else
                    {
                        RemoveDrawObject(tag); // Remove label if value is invalid
                    }
                };

                // Draw labels based on Show flags and valid data
                if (ShowYesterdayOHLC)
                {
                    DrawLevelText("label_yOpen", YOpen, YOHLCColor, "yOpen");
                    DrawLevelText("label_yHigh", YHigh, YOHLCColor, "yHigh");
                    DrawLevelText("label_yLow", YLow, YOHLCColor, "yLow");
                    DrawLevelText("label_yClose", YClose, YOHLCColor, "yClose");
                } else { RemoveDrawObject("label_yOpen"); RemoveDrawObject("label_yHigh"); RemoveDrawObject("label_yLow"); RemoveDrawObject("label_yClose"); }

//                if (ShowYProfile)
//                {
//                    DrawLevelText("label_yPOC", YPOC, YProfileColor, "yPOC");
//                    DrawLevelText("label_yVAH", YVAH, YProfileColor, "yVAH");
//                    DrawLevelText("label_yVAL", YVAL, YProfileColor, "yVAL");
//                } else { RemoveDrawObject("label_yPOC"); RemoveDrawObject("label_yVAH"); RemoveDrawObject("label_yVAL"); }

                if (ShowTodayOHL)
                {
                    DrawLevelText("label_tOpen", TOpen, TOHLColor, "tOpen");
                    DrawLevelText("label_tHigh", THigh, TOHLColor, "tHigh");
                    DrawLevelText("label_tLow", TLow, TOHLColor, "tLow");
                } else { RemoveDrawObject("label_tOpen"); RemoveDrawObject("label_tHigh"); RemoveDrawObject("label_tLow"); }

//                if (ShowTPOC)
//                {
//                    DrawLevelText("label_tPOC", TPOC, TPOCColor, "tPOC");
//                } else { RemoveDrawObject("label_tPOC"); }

                if (ShowTodayPivotPoints)
                {
                    DrawLevelText("label_tPP", TPP, TPivotColor, "tPP");
                    DrawLevelText("label_tR1", TR1, TPivotColor, "tR1");
                    DrawLevelText("label_tS1", TS1, TPivotColor, "tS1");
                } else { RemoveDrawObject("label_tPP"); RemoveDrawObject("label_tR1"); RemoveDrawObject("label_tS1"); }

                if (ShowOpenRange && ORCompleted)
                {
                    DrawLevelText("label_ORHigh", ORHigh, OpenRangeColor, "ORHigh");
                    DrawLevelText("label_ORLow", ORLow, OpenRangeColor, "ORLow");
                    DrawLevelText("label_ORCenter", ORCenter, OpenRangeColor, "ORCenter");
                } else { RemoveDrawObject("label_ORHigh"); RemoveDrawObject("label_ORLow"); RemoveDrawObject("label_ORCenter"); }

                if (ShowInitialBalance && IBCompleted)
                {
                    DrawLevelText("label_IBHigh", IBHigh, IBColor, "IBHigh"); // Uses getter
                    DrawLevelText("label_IBLow", IBLow, IBColor, "IBLow");   // Uses getter
                    DrawLevelText("label_IBCenter", IBCenter, IBColor, "IBCenter"); // Uses getter
                } else { RemoveDrawObject("label_IBHigh"); RemoveDrawObject("label_IBLow"); RemoveDrawObject("label_IBCenter"); }

                if (ShowLondonSession && londonSessionProcessedForDay) // Draw London labels only after session starts
                {
                    DrawLevelText("label_LonHigh", LondonHigh, LondonSessionColor, "LonHigh");
                    DrawLevelText("label_LonLow", LondonLow, LondonSessionColor, "LonLow");
                    DrawLevelText("label_LonOpen", LondonOpen, LondonSessionColor, "LonOpen");
                } else { RemoveDrawObject("label_LonHigh"); RemoveDrawObject("label_LonLow"); RemoveDrawObject("label_LonOpen"); }
            }
            else // Remove all labels if DrawLabels is false
            {
                 RemoveDrawObject("label_yOpen"); RemoveDrawObject("label_yHigh"); RemoveDrawObject("label_yLow"); RemoveDrawObject("label_yClose");
//                 RemoveDrawObject("label_yPOC"); RemoveDrawObject("label_yVAH"); RemoveDrawObject("label_yVAL");
                 RemoveDrawObject("label_tOpen"); RemoveDrawObject("label_tHigh"); RemoveDrawObject("label_tLow");
//                 RemoveDrawObject("label_tPOC");
                 RemoveDrawObject("label_tPP"); RemoveDrawObject("label_tR1"); RemoveDrawObject("label_tS1");
                 RemoveDrawObject("label_ORHigh"); RemoveDrawObject("label_ORLow"); RemoveDrawObject("label_ORCenter");
                 RemoveDrawObject("label_IBHigh"); RemoveDrawObject("label_IBLow"); RemoveDrawObject("label_IBCenter");
                 RemoveDrawObject("label_LonHigh"); RemoveDrawObject("label_LonLow"); RemoveDrawObject("label_LonOpen");
            }
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
		public KeyLevels KeyLevels(bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			return KeyLevels(Input, drawLabels, yOHLCColor, tOHLColor, tPivotColor, openRangeColor, iBColor, londonSessionColor, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, showLondonSession, openRangeStart, openRangeEnd, iBStart, iBEnd, londonSessionStart, londonSessionEnd);
		}

		public KeyLevels KeyLevels(ISeries<double> input, bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			if (cacheKeyLevels != null)
				for (int idx = 0; idx < cacheKeyLevels.Length; idx++)
					if (cacheKeyLevels[idx] != null && cacheKeyLevels[idx].DrawLabels == drawLabels && cacheKeyLevels[idx].YOHLCColor == yOHLCColor && cacheKeyLevels[idx].TOHLColor == tOHLColor && cacheKeyLevels[idx].TPivotColor == tPivotColor && cacheKeyLevels[idx].OpenRangeColor == openRangeColor && cacheKeyLevels[idx].IBColor == iBColor && cacheKeyLevels[idx].LondonSessionColor == londonSessionColor && cacheKeyLevels[idx].ShowOpenRange == showOpenRange && cacheKeyLevels[idx].ShowYesterdayOHLC == showYesterdayOHLC && cacheKeyLevels[idx].ShowTodayOHL == showTodayOHL && cacheKeyLevels[idx].ShowTodayPivotPoints == showTodayPivotPoints && cacheKeyLevels[idx].ShowInitialBalance == showInitialBalance && cacheKeyLevels[idx].ShowLondonSession == showLondonSession && cacheKeyLevels[idx].OpenRangeStart == openRangeStart && cacheKeyLevels[idx].OpenRangeEnd == openRangeEnd && cacheKeyLevels[idx].IBStart == iBStart && cacheKeyLevels[idx].IBEnd == iBEnd && cacheKeyLevels[idx].LondonSessionStart == londonSessionStart && cacheKeyLevels[idx].LondonSessionEnd == londonSessionEnd && cacheKeyLevels[idx].EqualsInput(input))
						return cacheKeyLevels[idx];
			return CacheIndicator<KeyLevels>(new KeyLevels(){ DrawLabels = drawLabels, YOHLCColor = yOHLCColor, TOHLColor = tOHLColor, TPivotColor = tPivotColor, OpenRangeColor = openRangeColor, IBColor = iBColor, LondonSessionColor = londonSessionColor, ShowOpenRange = showOpenRange, ShowYesterdayOHLC = showYesterdayOHLC, ShowTodayOHL = showTodayOHL, ShowTodayPivotPoints = showTodayPivotPoints, ShowInitialBalance = showInitialBalance, ShowLondonSession = showLondonSession, OpenRangeStart = openRangeStart, OpenRangeEnd = openRangeEnd, IBStart = iBStart, IBEnd = iBEnd, LondonSessionStart = londonSessionStart, LondonSessionEnd = londonSessionEnd }, input, ref cacheKeyLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			return indicator.KeyLevels(Input, drawLabels, yOHLCColor, tOHLColor, tPivotColor, openRangeColor, iBColor, londonSessionColor, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, showLondonSession, openRangeStart, openRangeEnd, iBStart, iBEnd, londonSessionStart, londonSessionEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			return indicator.KeyLevels(input, drawLabels, yOHLCColor, tOHLColor, tPivotColor, openRangeColor, iBColor, londonSessionColor, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, showLondonSession, openRangeStart, openRangeEnd, iBStart, iBEnd, londonSessionStart, londonSessionEnd);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KeyLevels KeyLevels(bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			return indicator.KeyLevels(Input, drawLabels, yOHLCColor, tOHLColor, tPivotColor, openRangeColor, iBColor, londonSessionColor, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, showLondonSession, openRangeStart, openRangeEnd, iBStart, iBEnd, londonSessionStart, londonSessionEnd);
		}

		public Indicators.KeyLevels KeyLevels(ISeries<double> input , bool drawLabels, Brush yOHLCColor, Brush tOHLColor, Brush tPivotColor, Brush openRangeColor, Brush iBColor, Brush londonSessionColor, bool showOpenRange, bool showYesterdayOHLC, bool showTodayOHL, bool showTodayPivotPoints, bool showInitialBalance, bool showLondonSession, DateTime openRangeStart, DateTime openRangeEnd, DateTime iBStart, DateTime iBEnd, DateTime londonSessionStart, DateTime londonSessionEnd)
		{
			return indicator.KeyLevels(input, drawLabels, yOHLCColor, tOHLColor, tPivotColor, openRangeColor, iBColor, londonSessionColor, showOpenRange, showYesterdayOHLC, showTodayOHL, showTodayPivotPoints, showInitialBalance, showLondonSession, openRangeStart, openRangeEnd, iBStart, iBEnd, londonSessionStart, londonSessionEnd);
		}
	}
}

#endregion
