### Monitor Tab (UI)

- The monitor tab should present summarized, highly readable data so the client can quickly understand what happened in each cycle without being exposed to technical details. Instead of verbose logs, display one compact item per cycle that covers the entire history: session start (when relevant), MT5 snapshot received, next event, trade valid status with reasons, aborts with reasons, etc. The goal is simplicity and clarity.
- Provide filters for **Today** and **Last Week** (display the actual date ranges, similar to the MT5 mobile app history screen). Remove other filters such as "All" or "Trades", and eliminate redundant icons at the top now that each item contains its own summary.
- Enable a copy/export option for each entry, as well as a bulk copy of all items matching the current filter. This export should output the full underlying logs (not the compact summary) so they can be passed to an AI or support team for deeper analysis.
- Default the view to the "Today" filter on load.

### Mobile UI – Capital Dashboard

- Clarify the source of the figures shown in the capital dashboard section. Are these values pulled directly from MT5? The same question applies to all other sections of the mobile UI: identify and document their data origins.

### Trades Tab – Live Runtime

- Remove the ability to edit the symbol; this control is unnecessary and causes confusion.
- Explain what the "auto sessions" field represents and why locations like Japan or India appear there. Confirm whether this information is being sourced from MT5 or generated locally.

### Sampling Gram Configuration

- Allow the user to adjust the gram parameter. The current minimum is 100 g; make this value configurable so different weights can be tested during analysis. Add this feature near to auto trade toggle switch so that both can be changed at the same place.

### UI Layout and Spacing

- Many pages currently render cards with no spacing between them, which looks cluttered. Add consistent gaps on all screens to improve visual cleanliness and usability.

### Data Source Verification

- Verify that every piece of data shown in the application is coming from the correct source. For example, confirm that the values on the ledger screen (and similar pages) are accurate and match their source systems.

### Live Data Refresh

- Wherever live or near‑real‑time data is displayed, implement an automatic refresh mechanism and show a "refreshed at" timestamp. Users should always know how fresh the information is.

Make the UI more simpler and useful overall, exactly with regards to what clients currently want.