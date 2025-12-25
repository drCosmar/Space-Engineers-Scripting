/*
 * LOGISTICS CORE v1.0
 * * HANDLES: Sorting, Assembler Flushing, and Inventory Dashboard.
 * * SETUP:
 * - Load this in PB #2.
 * - LCD Name: "Maintenance LCD"
 * * BUTTON PANEL SETUP:
 * - Button 1: Run PB -> Argument: "FLUSH"  (Force cleans assemblers)
 * - Button 2: Run PB -> Argument: "CYCLE"  (Switches LCD View)
 */

// --- CONFIGURATION ---
const string TAG_ORE   = "[Ore]";
const string TAG_INGOT = "[Ingot]";
const string TAG_COMP  = "[Comp]";
const string TAG_AMMO  = "[Ammo]"; 
const string TAG_TOOL  = "[Tool]"; 
const string TAG_ICE   = "[Ice]";  
const string TAG_FUEL  = "[Fuel]";
const string TAG_IGNORE = "[Ignore]";

const string LCD_NAME  = "Maintenance LCD";
const int ITEMS_PER_TICK = 25; // Lowered to prevent lag, runs faster freq

// --- GLOBALS ---
int totalMoved = 0;
int displayMode = 0; // 0=Dashboard, 1=Manifest
List<string> displayModes = new List<string> { "DASHBOARD", "MANIFEST" };

public Program()
{
    // Run very frequently for smooth sorting
    Runtime.UpdateFrequency = UpdateFrequency.Update10; 
}

public void Main(string argument, UpdateType updateSource)
{
    // --- BUTTON COMMANDS ---
    switch(argument.ToUpper())
    {
        case "CYCLE":
            displayMode++;
            if (displayMode >= displayModes.Count) displayMode = 0;
            UpdateDisplay();
            return;
            
        case "FLUSH":
            VacuumAssemblers(true); // Force aggressive clean
            return;
    }

    // --- MAIN LOOP ---
    SortInventory();
    
    // Every 100 ticks (approx 1.5 sec), check assemblers gently
    if (DateTime.Now.Second % 2 == 0) VacuumAssemblers(false);

    UpdateDisplay();
}

void SortInventory()
{
    var destOre   = GetContainersWithTag(TAG_ORE);
    var destIngot = GetContainersWithTag(TAG_INGOT);
    var destComp  = GetContainersWithTag(TAG_COMP);
    var destAmmo  = GetContainersWithTag(TAG_AMMO);
    var destTool  = GetContainersWithTag(TAG_TOOL);
    var destIce   = GetContainersWithTag(TAG_ICE);
    var destFuel  = GetContainersWithTag(TAG_FUEL);

    var sources = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(sources, b => 
        b.IsSameConstructAs(Me) && b.HasInventory && IsSortable(b));

    int tickMoved = 0;

    foreach (var source in sources)
    {
        string name = source.CustomName;
        for (int i = 0; i < source.InventoryCount; i++)
        {
            var inv = source.GetInventory(i);
            if (inv.ItemCount == 0) continue;

            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);

            for (int j = items.Count - 1; j >= 0; j--)
            {
                var item = items[j];
                string type = item.Type.TypeId.ToString();
                string subtype = item.Type.SubtypeId.ToString();
                
                // PROTECTED ITEMS (Do not move active powerbanks)
                if (subtype.Contains("Powerbank_")) continue;

                bool moved = false;

                // --- SORTING ---
                if ((subtype.Contains("Petroleum") || subtype.Contains("Petrol") || 
                     subtype.Contains("Kerosene") || subtype.Contains("Oil") || 
                     subtype.Contains("Fuel") || subtype.Contains("Diesel")) && destFuel.Count > 0)
                     moved = TryMove(inv, item, destFuel, name, TAG_FUEL);
                     
                else if (type.EndsWith("Ore")) {
                    if (subtype == "Ice" && destIce.Count > 0) moved = TryMove(inv, item, destIce, name, TAG_ICE);
                    else if (destOre.Count > 0) moved = TryMove(inv, item, destOre, name, TAG_ORE);
                }
                else if (type.EndsWith("Ingot")) {
                    if (destIngot.Count > 0) moved = TryMove(inv, item, destIngot, name, TAG_INGOT);
                }
                else if (type.EndsWith("Component")) {
                    if (destComp.Count > 0) moved = TryMove(inv, item, destComp, name, TAG_COMP);
                }
                else if (type.EndsWith("AmmoMagazine")) {
                    if (destAmmo.Count > 0) moved = TryMove(inv, item, destAmmo, name, TAG_AMMO);
                }
                else if (type.EndsWith("PhysicalGunObject") || type.EndsWith("GasContainerObject") || type.EndsWith("OxygenContainerObject")) {
                    if (destTool.Count > 0) moved = TryMove(inv, item, destTool, name, TAG_TOOL);
                }

                if (moved) { tickMoved++; totalMoved++; }
                if (tickMoved >= ITEMS_PER_TICK) return; 
            }
        }
    }
}

void VacuumAssemblers(bool force)
{
    // Finds assemblers hoarding ingots and pushes them back to [Ingot] storage
    var destIngot = GetContainersWithTag(TAG_INGOT);
    if (destIngot.Count == 0) return;

    var assemblers = new List<IMyAssembler>();
    GridTerminalSystem.GetBlocksOfType(assemblers, b => b.IsSameConstructAs(Me));

    foreach(var asm in assemblers)
    {
        // Don't clean Fuel Assemblies (Chemists)
        if (asm.CustomName.Contains("[FuelAssembly]")) continue;

        var input = asm.GetInventory(0);
        double fill = (double)input.CurrentVolume / (double)input.MaxVolume;

        // CRITERIA: If >80% full (or forced), start cleaning until 50%
        if (force || fill > 0.8)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            input.GetItems(items);
            foreach(var item in items)
            {
                // Only pull Ingots/Ore
                if (item.Type.TypeId.ToString().EndsWith("Ingot") || item.Type.TypeId.ToString().EndsWith("Ore"))
                {
                    // Move to Storage
                    foreach(var target in destIngot)
                    {
                        if (input.TransferItemTo(target.GetInventory(0), item)) break;
                    }
                }
            }
        }
    }
}

void UpdateDisplay()
{
    var lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextSurface;
    if (lcd == null) return;
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.Font = "Monospace";
    lcd.FontSize = 0.5f; 

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"╔═ LOGISTICS CORE ════ [{displayModes[displayMode][0]}] ╗");
    sb.AppendLine($"║ Moved: {totalMoved,-8}           ║");
    sb.AppendLine("╚═════════════════════════════╝");
    sb.AppendLine();

    if (displayMode == 0) // DASHBOARD
    {
        PrintProjectorStatus(sb);
        sb.AppendLine();
        sb.AppendLine("STORAGE CAPACITIES:");
        PrintGroupStat(sb, "FUEL", TAG_FUEL);
        PrintGroupStat(sb, "ORES", TAG_ORE);
        PrintGroupStat(sb, "INGOTS", TAG_INGOT);
        PrintGroupStat(sb, "COMPS", TAG_COMP);
        PrintGroupStat(sb, "AMMO", TAG_AMMO);
    }
    else if (displayMode == 1) // MANIFEST
    {
        sb.AppendLine("INVENTORY MANIFEST:");
        // Quick scan for display
        Dictionary<string, double> counts = new Dictionary<string, double>();
        var storage = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(storage, b => b.IsSameConstructAs(Me) && b.HasInventory);
        foreach(var b in storage) {
            for(int i=0; i<b.InventoryCount; i++) {
                var items = new List<MyInventoryItem>();
                b.GetInventory(i).GetItems(items);
                foreach(var item in items) {
                    string n = item.Type.SubtypeId;
                    if(counts.ContainsKey(n)) counts[n] += (double)item.Amount; else counts[n] = (double)item.Amount;
                }
            }
        }
        foreach(var kvp in counts.OrderByDescending(x=>x.Value).Take(14)) {
            sb.AppendLine($"{kvp.Key.PadRight(20).Substring(0,20)} : {kvp.Value:N0}");
        }
    }

    lcd.WriteText(sb.ToString());
}

void PrintProjectorStatus(StringBuilder sb)
{
    var projectors = new List<IMyProjector>();
    GridTerminalSystem.GetBlocksOfType(projectors, b => b.IsSameConstructAs(Me));
    bool activeFound = false;
    foreach(var proj in projectors)
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
            string barStr = new string('|', bars).PadRight(20, '.');
            sb.AppendLine($" [{barStr}] {pct,3:F0}%");
        }
    }
    if (!activeFound) sb.AppendLine("PROJECTOR: [ IDLE ]");
}

void PrintGroupStat(StringBuilder sb, string label, string tag)
{
    var list = GetContainersWithTag(tag);
    if (list.Count == 0) { sb.AppendLine($" {label,-8} [ NOT FOUND ]"); return; }
    double currentVolume = 0;
    double maxVolume = 0;
    foreach(var block in list)
    {
        var inv = block.GetInventory(0);
        currentVolume += (double)inv.CurrentVolume;
        maxVolume += (double)inv.MaxVolume;
    }
    double pct = maxVolume > 0 ? (currentVolume / maxVolume) * 100 : 0;
    int bars = (int)(pct / 5); 
    string barStr = new string('|', bars).PadRight(20, '.');
    sb.AppendLine($" {label,-8} [{barStr}] {pct,3:F0}%");
}

bool TryMove(IMyInventory sourceInv, MyInventoryItem item, List<IMyTerminalBlock> targets, string currentName, string targetTag)
{
    if (currentName.Contains(targetTag)) return false;
    foreach(var target in targets)
    {
        var destInv = target.GetInventory(0);
        if (!destInv.IsFull) { if(sourceInv.TransferItemTo(destInv, item)) return true; }
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
    return true; 
}


/*
 * PRODUCTION QUEUE SCANNER
 * * PURPOSE: Lists every item currently queued in every Assembler.
 * * USE: Run once to see what is actually happening inside your machines.
 */

const string LCD_NAME = "Debug LCD"; // Optional: Name of an LCD to show results

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Once; // Runs once immediately
}

public void Main(string argument, UpdateType updateSource)
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("=== ASSEMBLER QUEUE REPORT ===");
    sb.AppendLine($"Time: {DateTime.Now.ToShortTimeString()}");
    
    // 1. Get Assemblers
    var assemblers = new List<IMyAssembler>();
    GridTerminalSystem.GetBlocksOfType(assemblers, b => b.IsSameConstructAs(Me));

    if (assemblers.Count == 0)
    {
        sb.AppendLine("CRITICAL: No Assemblers Found!");
    }
    else
    {
        int totalQueued = 0;
        
        foreach (var asm in assemblers)
        {
            // 2. Get the Queue for this specific machine
            var queue = new List<MyProductionItem>();
            asm.GetQueue(queue);

            // Only print if there is something there
            if (queue.Count > 0)
            {
                sb.AppendLine($"\n[{asm.CustomName}]");
                foreach (var item in queue)
                {
                    // This is the EXACT name the game uses (Blueprint SubtypeID)
                    string bpName = item.BlueprintId.SubtypeName; 
                    double amount = (double)item.Amount;
                    
                    sb.AppendLine($" - {bpName}: {amount:N0}");
                    totalQueued++;
                }
            }
        }

        if (totalQueued == 0)
        {
            sb.AppendLine("\n[ ALL QUEUES EMPTY ]");
        }
    }

    // 3. Output Results
    string output = sb.ToString();
    
    // Print to the Programming Block Terminal (bottom right)
    Echo(output);

    // Print to LCD if it exists
    var lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextSurface;
    if (lcd != null)
    {
        lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        lcd.Font = "Monospace";
        lcd.FontSize = 0.6f;
        lcd.WriteText(output);
    }
}