IMPORTANT NOTES:
1. Now all prompts including master & shorts prompts are moved to prompts folder.
2. Chat GPT, Grok & Perplexity should use their respective master prompts
3. Other AI models can use standard "master_prompt"
4. USE AI itself for news based on the prompt NOT any RSS feeds
5. Everything should be implemented END TO END as described here in this document.
6. Any old logic/that doesn't fall under these should be cleaned-up removed.



IMPLEMENTATIONS:
Review all the implementations and implement this properly, you may work in parallel if needed using different cloud agents.

1. when I informed about our current ingestions/indicators from MT5, here is his response

Wa alaykum as-salaam. This feed is a good start for execution/timing (bid/ask/spread + session + server/KSA time), but it is NOT enough for safe high-profit TABLE generation or waterfall avoidance. We need MT5 to give us full candle+indicator state per timeframe (H4/H1/M30/M15/M5) and a few critical microstructure fields, otherwise the AI/regime classifier is blind and will still catch mid-air waterfalls.

Please add / expose from MT5 (either computed in MT5 and sent, or send raw OHLCV and we compute server-side):

• Per timeframe (H4/H1/M30/M15/M5) on every new candle close + periodic snapshot:
  - OHLC (open, high, low, close), candleStartTime, candleCloseTime
  - Volume (tick volume is fine), candleBodySize, wickSizes (high-open/close etc.)
  - MA20 value + distance of price from MA20 (air-gap)
  - RSI (H1 + M15 mandatory; M5 optional)
  - ATR (H1 + M15 mandatory)
  - Session range markers: current session high/low, previous session high/low, previous day high/low
  - Compression metric: overlap count / range contraction (or just send last N candle ranges so we compute)

• Tick/market quality:
  - Spread stats (min/avg/max last 1m and 5m), not only current spread
  - Tick rate + “freeze/gap” detector (missing ticks / latency spikes)
  - Slippage estimate if available (even simulated)

• Order/account state for safety + ledger loop:
  - Pending orders list (type, price, TP, expiry, volume grams equivalent)
  - Open positions list (entry price, current PnL in points, TP)
  - Free margin / equity / balance (even if demo) to enforce exposure caps
  - Order execution events (filled/partial/expired/cancelled) with timestamps

• Optional but very helpful:
  - VWAP or session VWAP (or send enough data so we compute)
  - “VolatilityExpansion” definition details (what window, what baseline) so we trust it.

Fetch frequency: ticks every 30s is fine for monitoring, but candle/indicator snapshots must be aligned to candle closes (especially M5/M15/H1). Once these are added, the regime classifier can reliably tag COMPRESSION/EXPANSION/EXHAUSTION and block waterfall traps before generating any TABLE.

2. After full validation if frequency tick is too frequent and unnecessary that takes more AI credits then change to adequate frequency intervals.

3. Each time AI may not produce correct result,etc so one AI result should be validated by other AI result, based on different patterns such as news, study, analyze, self_crosscheck & capitial utilization (see the relevant prompts in prompts folder)

4. When the user places order, if two orders fail to either trigger or gets caught in the waterfall then the system should do study & self_cross check (prompts are in prompts folder) and hard lock only refinements which generates more profits without getting caught in the waterfall , neither being in parnoia or in regular no trade mode. But should utilize the funds when ever possible without letting it sleep in gold .

5. Before generating every table it should do news, analyze, table. Once the table is generated then the other AIs should do validate prompt (prompt is in prompts folder)

6. validate short prompt is important. once validate the table is placed after getting the consensus from other ais.

7. Once the order is placed MT5 would send the new data to "analyze" again. This time the ai analyzes the deal placed and suggests any changes such as to lower the buying rates or hike it. Or tp adjustments or expiry time adjustments or to cancel it totally .

8. Implement spec/spec_v5.md

9. Implement spec/ui_changes.md

