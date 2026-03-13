# بسم الله الرحمن الرحيم
# PHYSICAL GOLD ROTATION ENGINE — FINAL IMPLEMENTATION SPEC
### BUY-ONLY Physical XAUUSD Pending Order Engine

---

# 1. OBJECTIVE

Build a **BUY-ONLY physical gold trading engine** that:

• intelligently plants BUY_LIMIT and BUY_STOP orders  
• avoids waterfall and fake breakout traps  
• maximizes safe rotation profits  
• minimizes manual babysitting  
• respects physical-ledger truth as the main capital reference  

The engine must operate safely and predictably while adapting to changing market conditions.

---

# 2. HARD-LOCKED RULES (CANNOT BE OVERRIDDEN)

These rules are permanent safety laws.

1. **No market buys ever**
2. **Pending-before-level law**
3. **Waterfall risk blocks all entries**
4. **Fail conditions block entries**
5. **Hazard windows block entries**
6. **Spread protection blocks entries**
7. **Physical ledger overrides MT5 account balance**

---

# 3. ENGINE DECISION STACK

The engine must process decisions in this order.

1. Ledger / capital truth  
2. Session classification  
3. Macro / risk context  
4. Market regime classification  
5. Structure detection  
6. Overextension detection  
7. Path routing  
8. PRETABLE legality validation  
9. Size / expiry / TP assignment  
10. Pending order generation  
11. Dashboard output  

Lower-timeframe triggers must never abort the process before higher-level path classification.

---

# 4. ENGINE PATH MODEL

The engine must classify **exactly one state** per cycle.

### BUY_LIMIT
Used when price is near a valid base or reclaim structure.

### BUY_STOP
Used when compression forms below a structural lid and breakout confirmation exists.

### WAIT / OVEREXTENDED
Used when price is stretched or no structure exists.

### STAND_DOWN
Used when hazard conditions or waterfall risk exists.

---

# 5. FACTOR ENGINE

Each factor must contain:


===================================================


Excellent. I will now generate the clean final developer package exactly as it should be delivered to your developer and his AI assistants.
This will be a single .MD specification file so the developer can directly feed it to:

AI coding assistants

Grok

Perplexity

ChatGPT

or use it manually while building the engine.


The goal is zero ambiguity and minimum developer questions.


---

GOLD_ENGINE_FINAL_IMPLEMENTATION_SPEC.md

# بسم الله الرحمن الرحيم
# PHYSICAL GOLD ROTATION ENGINE — FINAL IMPLEMENTATION SPEC
### BUY-ONLY Physical XAUUSD Pending Order Engine

---

# 1. OBJECTIVE

Build a **BUY-ONLY physical gold trading engine** that:

• intelligently plants BUY_LIMIT and BUY_STOP orders  
• avoids waterfall and fake breakout traps  
• maximizes safe rotation profits  
• minimizes manual babysitting  
• respects physical-ledger truth as the main capital reference  

The engine must operate safely and predictably while adapting to changing market conditions.

---

# 2. HARD-LOCKED RULES (CANNOT BE OVERRIDDEN)

These rules are permanent safety laws.

1. **No market buys ever**
2. **Pending-before-level law**
3. **Waterfall risk blocks all entries**
4. **Fail conditions block entries**
5. **Hazard windows block entries**
6. **Spread protection blocks entries**
7. **Physical ledger overrides MT5 account balance**

---

# 3. ENGINE DECISION STACK

The engine must process decisions in this order.

1. Ledger / capital truth  
2. Session classification  
3. Macro / risk context  
4. Market regime classification  
5. Structure detection  
6. Overextension detection  
7. Path routing  
8. PRETABLE legality validation  
9. Size / expiry / TP assignment  
10. Pending order generation  
11. Dashboard output  

Lower-timeframe triggers must never abort the process before higher-level path classification.

---

# 4. ENGINE PATH MODEL

The engine must classify **exactly one state** per cycle.

### BUY_LIMIT
Used when price is near a valid base or reclaim structure.

### BUY_STOP
Used when compression forms below a structural lid and breakout confirmation exists.

### WAIT / OVEREXTENDED
Used when price is stretched or no structure exists.

### STAND_DOWN
Used when hazard conditions or waterfall risk exists.

---

# 5. FACTOR ENGINE

Each factor must contain:

factorName value impactDirection impactStrength timeHorizon

Factors influence:

legalityState biasState pathState sizeState exitState

---

# 6. FACTOR CLASSES

## MACRO

Inputs:

• DXY strength  
• US real yields  
• risk-on / risk-off state  
• geopolitical escalation  
• scheduled macro events  

These affect bias and caution levels.

---

## SESSION

Sessions:

Japan India London New York

Session properties affect:

• trade size multiplier  
• expiry realism  
• trap probability  

---

## VOLATILITY

Indicators:

ATR ADR volatility compression index spread liquidity state

These determine whether continuation or mean-reversion trades are appropriate.

---

## STRUCTURE

Structure detection must include:

• base / shelf detection  
• lid detection  
• sweep detection  
• reclaim detection  

These structures are primary entry drivers.

---

## STRETCH / EXHAUSTION

Indicators:

MA20 distance RSI level ATR expansion distance from last base

These determine overextension and chase risk.

---

# 7. OVEREXTENSION DETECTOR

Inputs:

MA20 distance RSI ATR expansion distance from base

Outputs:

NORMAL STRETCHED OVEREXTENDED

OVEREXTENDED behavior:

• BUY_STOP blocked  
• shallow BUY_LIMIT blocked  
• pathState = WAIT_PULLBACK  

---

# 8. SWEEP + RECLAIM DETECTOR

Detect:

equal highs / lows liquidity sweeps reclaim closes failed reclaim

States:

NONE SWEEP_ONLY SWEEP_RECLAIM FAILED_RECLAIM

SWEEP_RECLAIM near base increases BUY_LIMIT confidence.

---

# 9. NUMERIC STARTER THRESHOLDS

## MA20 distance

<= 0.8 ATR = normal 0.8-1.5 ATR = stretched

> 1.5 ATR = extreme



---

## RSI bands

< 35 = low 35-65 = neutral 65-75 = high

> 75 = extreme



---

## ADR usage

todayRange / ADR20



> 1 ADR blocks continuation



---

## Volatility compression

VCI = avgRange10 / avgRange50



<=0.7 compressed 0.7-1.3 normal

> 1.3 expanded



---

## Spread guard

spreadBlock = 0.7 spreadCaution = 0.5

---

# 10. CONFIDENCE SCORE

Total score = 0-100.

H1 context alignment     +15 M15 structure validity   +15 Sweep reclaim presence   +15 Volatility suitability   +10 RSI/MA stretch state     +10 Session suitability      +10 ADR usage                +10 Spread condition         +5 No hazard state          +10

Decision thresholds:

<60   WAIT 60-74 MICRO size 75-89 NORMAL size

> =90  HIGH confidence



---

# 11. ROTATION EFFICIENCY ENGINE

Trades must also pass **capital efficiency filters**.

Metrics:

same session TP probability expected AED return expected hold time AED per minute sleep risk

Efficiency states:

HIGH MEDIUM LOW CAPITAL_SLEEP_RISK

Trades likely to trap capital across sessions should be rejected.

---

# 12. MICRO MODE

When deployable capital is small:

• one pending order only  
• no laddering  
• TP mandatory  
• expiry mandatory  

Existing gold inventory is separate from fresh capital.

---

# 13. ENGINE INPUT DATA CONTRACT

Each cycle must receive:

symbol bid ask spread

MA20_M5 MA20_M15 MA20_H1

ATR_M15 ATR_H1

RSI_M15 RSI_H1

session

dxy real_yields geo_risk_flag news_event_flag

ledger_cash_aed ledger_gold_grams deployable_cash

---

# 14. ENGINE OUTPUT CONTRACT

Each cycle must return:

pathState

entryLevels: limit1 limit2 stop1

tp expiry size

confidenceScore efficiencyScore

reasonCode

---

# 15. EXECUTION LOOP

Engine runs continuously.

Pseudo-logic:

while engineRunning:

readMarketData()

computeIndicators()

evaluateFactors()

runOverextensionDetector()

detectStructure()

classifyPath()

computeConfidenceScore()

computeEfficiencyScore()

if legalityState == BLOCK:
    return STAND_DOWN

if efficiencyScore < threshold:
    return WAIT

generatePendingOrder()

sendToMT5()

updateDashboard()

sleep(nextTick)

---

# 16. AI INTELLIGENCE LAYER

The engine may optionally query external AI models.

### GROK
structure reasoning

Tasks:

• identify bases  
• identify lids  
• detect compression  
• detect sweep + reclaim  

---

### PERPLEXITY
macro intelligence

Tasks:

• detect macro drivers  
• detect geopolitical risk  
• detect hazard windows  

---

### CHATGPT
correlation engine

Tasks:

• combine macro + structure  
• evaluate overextension  
• evaluate rotation efficiency  

---

# 17. DASHBOARD

Dashboard must display:

### Ledger

Cash Gold Deployable funds

### Engine states

legalityState biasState pathState overextensionState waterfallRisk sessionPhase

### Trade map

bases lids sweep zones pending orders TP magnets

---

# 18. FINAL OBJECTIVE

The completed engine should:

• intelligently place pending orders  
• avoid waterfall traps  
• avoid panic breakout traps  
• maximize safe rotation profits  
• reduce manual babysitting  
• maintain ledger-based capital integrity  

الحمد لله


---

What you should do now

Send this .MD document to your developer.

Do not split it into multiple messages.

This file contains:

the architecture

the AI layer

the data contracts

the execution logic


Which is exactly what AI coding assistants need.


---

If you want, I can also create one more powerful thing that will help your developer a lot:

“Gold Engine Architecture Diagram”

It visually shows the full system like this:

Market Data
     ↓
Indicator Engine
     ↓
Factor Engine
     ↓
Structure Engine
     ↓
Path Engine
     ↓
Efficiency Engine
     ↓
AI Layer
     ↓
Order Engine
     ↓
MT5

That diagram often cuts developer time by 30-40%.

If you want, I will generate it next.

==================================

Yes — here is the Mermaid diagram version the developer can paste into docs, GitHub, Notion, or markdown systems that support Mermaid.

flowchart TD
    A[PHYSICAL LEDGER TRUTH<br/>Cash AED / Gold g / Limits] --> B[MARKET DATA INPUT<br/>Bid / Ask / Spread / M5 / M15 / H1 / H4]
    B --> C[INDICATOR ENGINE<br/>MA20 / ATR / RSI / ADR / VCI]

    C --> D[FACTOR ENGINE<br/>Macro / Risk / Session / Volatility / Structure / Stretch<br/>DXY / Real Yields / Geo Risk / Spread]
    D --> E[STRUCTURE ENGINE<br/>Base / Lid / Sweep / Reclaim / Compression]
    D --> F[RISK & SAFETY ENGINE<br/>WATERFALL / FAIL / Hazard / Overextension / Session Trap]

    E --> G[PATH ENGINE<br/>BUY_LIMIT / BUY_STOP / WAIT_PULLBACK / STAND_DOWN]
    F --> G

    G --> H[PRETABLE / LEGALITY<br/>LEGAL / CAUTION / BLOCK<br/>Confidence Score]
    H --> I[ROTATION EFFICIENCY ENGINE<br/>Same-session TP realism / AED per minute / Sleep Risk]
    I --> J[ORDER GENERATION ENGINE<br/>BUY LIMIT / BUY STOP only<br/>Size / TP / Expiry]
    J --> K[MT5 EXECUTION LAYER<br/>Pending orders only]
    K --> L[SLIPS / LEDGER UPDATE<br/>Trade records / balances]
    L --> M[DASHBOARD / UI<br/>Ledger / States / Trade Map]

    N[GROK<br/>Chart reading / Base / Lid] --> Q[AI INTELLIGENCE LAYER<br/>Support only<br/>Cannot override hard blockers]
    O[PERPLEXITY<br/>Macro / News / Hazard scans] --> Q
    P[CHATGPT<br/>Correlation / Audits / Study] --> Q
    Q --> D

    R[HARD BLOCKERS<br/>No market buys<br/>Pending-before-level only<br/>WATERFALL block<br/>FAIL block<br/>Hazard block<br/>Spread block<br/>Exposure / Deployable limit block<br/>Physical ledger truth block] --> H

    M --> S[FINAL OUTPUT<br/>1) EXECUTABLE TRADE TABLE<br/>or<br/>2) NO TRADE — CLEAR REASON]

And here is a cleaner simplified Mermaid version if the first one feels too dense:

flowchart TD
    A[Ledger Truth] --> B[Market Data]
    B --> C[Indicators]
    C --> D[Factor Engine]
    D --> E[Structure Engine]
    D --> F[Risk Engine]
    E --> G[Path Engine]
    F --> G
    G --> H[Legality / Confidence]
    H --> I[Rotation Efficiency]
    I --> J[Order Generation]
    J --> K[MT5 Pending Orders]
    K --> L[Slips / Ledger Update]
    L --> M[Dashboard / UI]

    N[Grok] --> Q[AI Support Layer]
    O[Perplexity] --> Q
    P[ChatGPT] --> Q
    Q --> D