# KeyLevels
NinjaTrader 8 indicator that combines the previous day's OHLC with the current day's OHL, Pivot Point, R1, S1, Opening Range High/Low/Center, and Initial Balance High/Low/Center into a single collection of key levels.

## Updates
1. Added new plots for London Open, High, and Low.
2. Temporarily removed Value Area and POC plots. They didn't perfectly align with the NT Volume Profile values.

**Installation:**

Download the .CS file and place it in your indicators folder.

C:\Users\<your user name>\Documents\NinjaTrader 8\bin\Custom\Indicators

![image](https://github.com/user-attachments/assets/378b6630-ff5c-4d0e-ae12-a72d363854dc)

**Strategies Access:**

To access a key level from your strategy, instantiate the KeyLevel object and type the following in your OnBarUpdate method:

```
keyLevels1.Update();
tPOC = keyLevels1.TPOC;
```
