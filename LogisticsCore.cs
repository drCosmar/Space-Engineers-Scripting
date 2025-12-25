/*
 * LOGISTICS CORE v1.2 (Refactor + Fix)
 * Purpose preserved:
 * - Sorting inventory into tagged storage containers.
 * - Assembler vacuuming (pull ores/ingots out of assembler input) with a FORCE option.
 * - Maintenance LCD with a dashboard + manifest view.
 *
 * Refactor goals:
 * - Deterministic scheduling (no DateTime.Now.Second modulo).
 * - Cache container lists; refresh periodically.
 * - Bound work per tick.
 * - Keep "Powerbank_" protection and [Ignore] exclusions.
 * - FIXED: Assembler vacuum now runs regardless of volume fill (was waiting for 80%).
 *
 * SETUP:
 * - Place in PB #2.
 * - LCD Name: "Maintenance LCD"
 * - Containers tagged in CustomName:
 * [Ore], [Ingot], [Comp], [Ammo], [Tool], [Ice], [Fuel]
 * - Optional ignore tag for blocks:
 * [Ignore]
 * - Button panel:
 * - "FLUSH" -> vacuum assemblers aggressively
 * - "CYCLE" -> switch LCD view
 */

const string TAG_ORE    = "[Ore]";
const string TAG_INGOT  = "[Ingot]";
const string TAG_COMP   = "[Comp]";
const string TAG_AMMO   = "[Ammo]";
const string TAG_TOOL   = "[Tool]";
const string TAG_ICE    = "[Ice]";
const string TAG_FUEL   = "[Fuel]";
const string TAG_IGNORE = "[Ignore]";

const string LCD_NAME = "Maintenance LCD";

const int ITEMS_PER_TICK = 25;
const int CACHE_REFRESH_TICKS = 300;     // refresh block/container caches
const int VACUUM_INTERVAL_TICKS = 120;   // every ~20 seconds at Update10

int totalMoved = 0;
int displayMode = 0;
List<string> displayModes = new List<string> { "DASHBOARD", "MANIFEST" };

int tick = 0;

// Cached destinations
List<IMyTerminalBlock> destOre = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destIngot = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destComp = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destAmmo = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destTool = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destIce = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> destFuel = new List<IMyTerminalBlock>();

// Cached sources and assemblers
List<IMyTerminalBlock> sortableSources = new List<IMyTerminalBlock>();
List<IMyAssembler> assemblers = new List<IMyAssembler>();

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    RefreshCaches();
}

public void Main(string argument, UpdateType updateSource)
{
    tick++;

    if (!string.IsNullOrWhiteSpace(argument))
    {
        switch (argument.Trim().ToUpper())
        {
            case "CYCLE":
                displayMode++;
                if (displayMode >= displayModes.Count) displayMode = 0;
                UpdateDisplay();
                return;

            case "FLUSH":
                VacuumAssemblers(true);
                UpdateDisplay();
                return;

            case "REBUILD":
                RefreshCaches();
                UpdateDisplay();
                return;
        }
    }

    if (tick % CACHE_REFRESH_TICKS == 0) RefreshCaches();

    SortInventory();

    if (tick % VACUUM_INTERVAL_TICKS == 0)
        VacuumAssemblers(false);

    UpdateDisplay();
}

void RefreshCaches()
{
    // Destination containers
    destOre = GetContainersWithTag(TAG_ORE);
    destIngot = GetContainersWithTag(TAG_INGOT);
    destComp = GetContainersWithTag(TAG_COMP);
    destAmmo = GetContainersWithTag(TAG_AMMO);
    destTool = GetContainersWithTag(TAG_TOOL);
    destIce = GetContainersWithTag(TAG_ICE);
    destFuel = GetContainersWithTag(TAG_FUEL);

    // Sortable sources
    sortableSources.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(sortableSources, b =>
        b.IsSameConstructAs(Me) && b.HasInventory && IsSortable(b));

    // Assemblers for vacuum
    assemblers.Clear();
    GridTerminalSystem.GetBlocksOfType(assemblers, a => a.IsSameConstructAs(Me));
}

void SortInventory()
{
    int tickMoved = 0;

    foreach (var source in sortableSources)
    {
        string name = source.CustomName;

        for (int i = 0; i < source.InventoryCount; i++)
        {
            var inv = source.GetInventory(i);
            if (inv == null || inv.ItemCount == 0) continue;

            var items = new List<MyInventoryItem>();
            inv.GetItems(items);

            for (int j = items.Count - 1; j >= 0; j--)
            {
                var item = items[j];

                string typeId = item.Type.TypeId.ToString();   // e.g. "MyObjectBuilder_Ore"
                string subtype = item.Type.SubtypeId.ToString();

                // Protected items
                if (subtype.Contains("Powerbank_")) continue;

                bool moved = false;

                // Fuel detection (subtype-based heuristic)
                if (IsFuelSubtype(subtype))
                {
                    if (destFuel.Count > 0) moved = TryMove(inv, item, destFuel, name, TAG_FUEL);
                }
                else if (typeId.EndsWith("Ore"))
                {
                    if (subtype == "Ice")
                    {
                        if (destIce.Count > 0) moved = TryMove(inv, item, destIce, name, TAG_ICE);
                        else if (destOre.Count > 0) moved = TryMove(inv, item, destOre, name, TAG_ORE);
                    }
                    else
                    {
                        if (destOre.Count > 0) moved = TryMove(inv, item, destOre, name, TAG_ORE);
                    }
                }
                else if (typeId.EndsWith("Ingot"))
                {
                    if (destIngot.Count > 0) moved = TryMove(inv, item, destIngot, name, TAG_INGOT);
                }
                else if (typeId.EndsWith("Component"))
                {
                    if (destComp.Count > 0) moved = TryMove(inv, item, destComp, name, TAG_COMP);
                }
                else if (typeId.EndsWith("AmmoMagazine"))
                {
                    if (destAmmo.Count > 0) moved = TryMove(inv, item, destAmmo, name, TAG_AMMO);
                }
                else if (typeId.EndsWith("PhysicalGunObject") || typeId.EndsWith("GasContainerObject") || typeId.EndsWith("OxygenContainerObject"))
                {
                    if (destTool.Count > 0) moved = TryMove(inv, item, destTool, name, TAG_TOOL);
                }

                if (moved)
                {
                    tickMoved++;
                    totalMoved++;
                }

                if (tickMoved >= ITEMS_PER_TICK) return;
            }
        }
    }
}

bool IsFuelSubtype(string subtype)
{
    // Keep your original heuristic; can be expanded if needed.
    return subtype.Contains("Petroleum") ||
           subtype.Contains("Petrol") ||
           subtype.Contains("Kerosene") ||
           subtype.Contains("Oil") ||
           subtype.Contains("Fuel") ||
           subtype.Contains("Diesel");
}

void VacuumAssemblers(bool force)
{
    // Safety check: ensure we have somewhere to put things
    if (destIngot.Count == 0 && destComp.Count == 0) return;

    foreach (var asm in assemblers)
    {
        // EXCLUSION: Don't touch the Fuel/Chemist assemblers
        if (asm.CustomName.Contains("[FuelAssembly]")) continue;

        // --- STEP 1: Clean INPUT (Inventory 0) ---
        // CRITICAL FIX: Only clean input if the assembler is NOT trying to work.
        // If the queue has items, let the assembler keep its ingots!
        if (force || asm.IsQueueEmpty)
        {
            var input = asm.GetInventory(0);
            if (input != null && input.ItemCount > 0)
            {
                var items = new List<MyInventoryItem>();
                input.GetItems(items);
    
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    string typeId = item.Type.TypeId.ToString();
    
                    // Only pull raw resources out of input
                    if (typeId.EndsWith("Ingot") || typeId.EndsWith("Ore"))
                    {
                        // Pass "" as currentName to bypass tag checks. 
                        // We want to force-empty the assembler even if it's named "Assembler [Ingot]"
                        TryMove(input, item, destIngot, "", TAG_INGOT);
                    }
                }
            }
        }

        // --- STEP 2: Clean OUTPUT (Inventory 1) ---
        // Always clean output, even if producing.
        var output = asm.GetInventory(1);
        if (output != null && output.ItemCount > 0)
        {
            var items = new List<MyInventoryItem>();
            output.GetItems(items);

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                string typeId = item.Type.TypeId.ToString();
                
                // Route based on type
                if (typeId.EndsWith("Component"))
                {
                    TryMove(output, item, destComp, "", TAG_COMP);
                }
                else if (typeId.EndsWith("AmmoMagazine"))
                {
                    TryMove(output, item, destAmmo, "", TAG_AMMO);
                }
                else if (typeId.EndsWith("PhysicalGunObject") || 
                         typeId.EndsWith("GasContainerObject") || 
                         typeId.EndsWith("OxygenContainerObject"))
                {
                    TryMove(output, item, destTool, "", TAG_TOOL);
                }
                else 
                {
                    // Fallback for weird modded items
                    TryMove(output, item, destComp, "", TAG_COMP);
                }
            }
        }
    }
}

void UpdateDisplay()
{
    var lcdBlock = GridTerminalSystem.GetBlockWithName(LCD_NAME);
    var lcd = lcdBlock as IMyTextSurface;
    if (lcd == null)
    {
        var provider = lcdBlock as IMyTextSurfaceProvider;
        if (provider != null) lcd = provider.GetSurface(0);
    }
    if (lcd == null) return;

    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.Font = "Monospace";
    lcd.FontSize = 0.5f;

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"╔═ LOGISTICS CORE ════ [{displayModes[displayMode][0]}] ╗");
    sb.AppendLine($"║ Moved: {totalMoved,-8}            ║");
    sb.AppendLine("╚═════════════════════════════╝");
    sb.AppendLine();

    if (displayMode == 0)
    {
        PrintProjectorStatus(sb);
        sb.AppendLine();
        sb.AppendLine("STORAGE CAPACITIES:");
        PrintGroupStat(sb, "FUEL", destFuel);
        PrintGroupStat(sb, "ORES", destOre);
        PrintGroupStat(sb, "INGOTS", destIngot);
        PrintGroupStat(sb, "COMPS", destComp);
        PrintGroupStat(sb, "AMMO", destAmmo);
    }
    else
    {
        sb.AppendLine("INVENTORY MANIFEST:");

        Dictionary<string, double> counts = new Dictionary<string, double>();
        var blocks = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.IsSameConstructAs(Me) && b.HasInventory);

        foreach (var b in blocks)
        {
            for (int i = 0; i < b.InventoryCount; i++)
            {
                var inv = b.GetInventory(i);
                if (inv == null || inv.ItemCount == 0) continue;

                var items = new List<MyInventoryItem>();
                inv.GetItems(items);

                foreach (var item in items)
                {
                    string n = item.Type.SubtypeId;
                    double amt = (double)item.Amount;
                    double cur;
                    if (counts.TryGetValue(n, out cur)) counts[n] = cur + amt;
                    else counts[n] = amt;
                }
            }
        }

        foreach (var kvp in counts.OrderByDescending(x => x.Value).Take(14))
        {
            sb.AppendLine($"{kvp.Key.PadRight(20).Substring(0, 20)} : {kvp.Value:N0}");
        }
    }

    lcd.WriteText(sb.ToString());
}

void PrintProjectorStatus(StringBuilder sb)
{
    var projectors = new List<IMyProjector>();
    GridTerminalSystem.GetBlocksOfType(projectors, b => b.IsSameConstructAs(Me));

    bool activeFound = false;
    foreach (var proj in projectors)
    {
        if (proj.IsProjecting)
        {
            activeFound = true;

            int total = proj.TotalBlocks;
            int remaining = proj.RemainingBlocks;
            int built = total - remaining;
            double pct = total > 0 ? ((double)built / total) * 100 : 0;

            sb.AppendLine($"PRJ: {proj.CustomName}");

            int bars = (int)(pct / 5);
            if (bars < 0) bars = 0;
            if (bars > 20) bars = 20;

            string barStr = new string('|', bars).PadRight(20, '.');
            sb.AppendLine($" [{barStr}] {pct,3:F0}%");
        }
    }

    if (!activeFound) sb.AppendLine("PROJECTOR: [ IDLE ]");
}

void PrintGroupStat(StringBuilder sb, string label, List<IMyTerminalBlock> list)
{
    if (list == null || list.Count == 0)
    {
        sb.AppendLine($" {label,-8} [ NOT FOUND ]");
        return;
    }

    double currentVolume = 0;
    double maxVolume = 0;

    foreach (var block in list)
    {
        var inv = block.GetInventory(0);
        if (inv == null) continue;

        currentVolume += (double)inv.CurrentVolume;
        maxVolume += (double)inv.MaxVolume;
    }

    double pct = maxVolume > 0 ? (currentVolume / maxVolume) * 100 : 0;

    int bars = (int)(pct / 5);
    if (bars < 0) bars = 0;
    if (bars > 20) bars = 20;

    string barStr = new string('|', bars).PadRight(20, '.');
    sb.AppendLine($" {label,-8} [{barStr}] {pct,3:F0}%");
}

bool TryMove(IMyInventory sourceInv, MyInventoryItem item, List<IMyTerminalBlock> targets, string currentName, string targetTag)
{
    // Don’t move into same-category container
    if (currentName.Contains(targetTag)) return false;

    foreach (var target in targets)
    {
        var destInv = target.GetInventory(0);
        if (destInv == null || destInv.IsFull) continue;

        if (sourceInv.TransferItemTo(destInv, item))
            return true;
    }

    return false;
}

List<IMyTerminalBlock> GetContainersWithTag(string tag)
{
    var list = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType(list, b => b.IsSameConstructAs(Me) && b.HasInventory && b.CustomName.Contains(tag));
    return list;
}

bool IsSortable(IMyTerminalBlock b)
{
    if (b.CustomName.Contains(TAG_IGNORE)) return false;
    if (b.CustomName.Contains("Charger")) return false;

    if (b is IMyPowerProducer) return false;
    if (b is IMyRefinery) return false;
    if (b is IMyAssembler) return false;
    if (b is IMyLargeTurretBase) return false;
    if (b is IMyUserControllableGun) return false;
    if (b is IMyGasGenerator) return false;
    if (b is IMyGasTank) return false;
    
    // FIX: Ignore Connectors. If a connector is set to "Collect All", 
    // it fights the script sorting logic.
    if (b is IMyShipConnector) return false;

    return true;
}