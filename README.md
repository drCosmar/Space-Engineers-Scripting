# Printer Control Script

**REQUIRED MOD:** Nanobot Build and Repair System (Mod ID: 2111073562).
This script will **not** function correctly without this mod installed and a Build and Repair block present on the grid.

## Usage

### Setup
Ensure all relevant blocks are tagged according to the configuration at the top of the script (default: `[Printer]`, `[PrinterMain]`, `[PrinterNudge]`, `[Indicator]`, `[Nano]`).

### Controls
The script is designed for two button panels and an LCD.

**Main Panel (4-Button):**
1.  **Projector Select:** Cycles active projector or toggles specific grid sizes if arguments are bound.
2.  **Mode Toggle:** Switches between **Translation** (Move) and **Rotation** mode.
3.  **Nano Toggle:** Toggles the "Help Others" / "Build New" capability of the Nanobot system.
4.  *(Reserved)*

**Nudge Panel (4-Button):**
1.  **Direction:** Toggles between Positive (+) and Negative (-) adjustment.
2.  **X Axis:** Applies adjustment to X axis.
3.  **Y Axis:** Applies adjustment to Y axis.
4.  **Z Axis:** Applies adjustment to Z axis.

### Arguments
Bind button actions to `Run` with the following arguments:
*   `proj_next`: Cycle projectors.
*   `toggle_mode`: Switch Move/Rotate.
*   `toggle_dir`: Switch +/-.
*   `adjust_x`, `adjust_y`, `adjust_z`: Nudge the projection.
*   `reset_pos`: Zero out offsets.