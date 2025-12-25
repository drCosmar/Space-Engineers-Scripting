# Project Session Notes & AI Handoff

## Project Scope
**Goal:** Create a comprehensive control system for a dual-projector (Large/Small grid) printer setup in Space Engineers.
**Hardware:**
*   2 Projectors (tagged `[LG]` and `[SM]`).
*   1 Main Button Panel (Control Mode/Selection).
*   1 Nudge Button Panel (XYZ Movement).
*   1 Indicator Light (Status feedback).
*   1 LCD (Detailed status).
*   1 Nanobot Build and Repair Unit (Modded block).

## Technical Learnings & Pitfalls

### 1. Update Frequency
*   **Issue:** `Update100` (approx 1.6s) is too slow for UI feedback.
*   **Solution:** Use `Update10` (approx 0.16s) or faster.
*   **Note:** Ensure display update logic is throttled (e.g., every 6 ticks of Update10) if using Update1 to avoid performance costs, but Update10 is generally a good balance for button panels.

### 2. Modded Block Integration (Nanobot Build and Repair)
*   **Issue:** Accessing properties on modded blocks can cause crashes if the property ID is incorrect or the block isn't fully initialized.
*   **Critical:** Always check `block.GetProperty("PropertyID") != null` before calling `block.GetValue<T>("PropertyID")`.
*   **Property ID:** The "Build New" toggle is accessed via `BuildAndRepair.AllowBuild` (Boolean).

### 3. String Parsing & Tagging
*   **Issue:** Strict string matching (e.g., `Contains("[lg]")`) failed when displays truncated names or formatting changed.
*   **Solution:** Use looser matching for prefixes where safe (e.g., `Contains("[lg")`) to handle variations like `[LG] [Printer]`.

### 4. Display Logic
*   **State:** The script maintains internal state for `AdjustMode` (Translate/Rotate) and `AdjustDirection` (+/-).
*   **Feedback:** Button panels are updated dynamically to show the current function of the button (e.g., Button 2 shows "Mode: Move" or "Mode: Rotate").

## Current Status
*   Nudge controls are implemented for Translation and Rotation.
*   Nanobot toggle is functional and reading state correctly.
*   Indicator lights handle multi-tag logic (Green for LG, Blue for SM).