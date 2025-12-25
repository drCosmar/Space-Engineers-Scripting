/*
 * BASE HEARTBEAT v7.0 (Economy Refactor)
 * * Performance Changes:
 * - Runs on Update10 (Fast tick) but uses Time-Slicing.
 * - Inventory scans, queue checks, and production logic are 
 * offset to different ticks to prevent CPU spikes.
 * - LCDs update smoothly without re-calculating data every frame.
 *
 * SETUP:
 * - Place in PB #1.
 * - LCDs: "Status LCD", "Debug LCD"
 * - Assemblers named: [AutoAssembler], [AutoSlave], [FuelAssembly], [AutoAmmunition]
 */

const string LCD_NAME = "Status LCD";
const string DEBUG_LCD_NAME = "Debug LCD";

const double THRESHOLD = 0.40;       // "critical low" threshold vs target

// --- SCHEDULING CONSTANTS (Ticks @ Update10) ---
// 6 ticks = ~1 second
const int CACHE_INTERVAL = 1200;     // Re-fetch blocks every ~3 mins
const int SCAN_INTERVAL  = 30;       // Scan Inventory/Queues every ~5 seconds
const int PROD_INTERVAL  = 120;      // Calculate production every ~20 seconds
const int LCD_INTERVAL   = 6;        // Redraw LCD every ~1 second
const int DEBUG_PAGE_INTERVAL = 30;  // Switch debug page every ~5 seconds

// --- TRANSLATION LAYER ---
Dictionary<string, string> ALIASES = new Dictionary<string, string>
{
    {"Plastic", "OilToPlastic"},
    {"Rubber", "OilToRubber"},
    {"PotassiumPerchlorate", "IceToPerchlorate"},
    {"Nitre", "IceToPerchlorate"},
    {"AdvancedCircuit", "Circuit"},
    {"NeodymiumMagnet", "Magnet"},
    {"FlashPowder", "Flashpowder"},
    {"10GHzCPU", "OctocoreComponent"},
    {"SuitPowerbank", "SuitPowerbank_2"},
    {"Powerbank", "SuitPowerbank_2"},
};

// --- MASTER CATALOG ---
// Format: new ItemReq("InventoryName", "BlueprintName", Target, "Tag"),
List<ItemReq> CATALOG = new List<ItemReq>
{
    // === [AutoAssembler] BULK ===
    new ItemReq("SteelPlate",              "SteelPlate",              10000, "[AutoAssembler]"),
    new ItemReq("InteriorPlate",           "InteriorPlate",            6000, "[AutoAssembler]"),
    new ItemReq("SmallTube",               "SmallTube",                5000, "[AutoAssembler]"),
    new ItemReq("LargeTube",               "LargeTube",                2000, "[AutoAssembler]"),
    new ItemReq("MetalGrid",               "MetalGrid",                4000, "[AutoAssembler]"),
    new ItemReq("Girder",                  "GirderComponent",          2000, "[AutoAssembler]"), // Fixed
    new ItemReq("SteelGirder",             "SteelGirder",              1500, "[AutoAssembler]"),
    new ItemReq("BulletproofGlass",        "BulletproofGlass",         2000, "[AutoAssembler]"),
    new ItemReq("Construction",            "ConstructionComponent",    8000, "[AutoAssembler]"), // Fixed

    // === [AutoSlave] TECH & ALLOYS ===
    new ItemReq("Steel",                   "Steel",                   5000, "[AutoSlave]"),
    new ItemReq("Motor",                   "MotorComponent",           3000, "[AutoSlave]"), // Fixed
    new ItemReq("Computer",                "ComputerComponent",        2000, "[AutoSlave]"), // Fixed
    new ItemReq("RadioCommunication",      "RadioCommunicationComponent",100,"[AutoSlave]"), // Fixed
    new ItemReq("Detector",                "DetectorComponent",        1000, "[AutoSlave]"), // Fixed
    new ItemReq("Display",                 "Display",                  1500, "[AutoSlave]"),
    new ItemReq("Explosives",              "ExplosivesComponent",       100, "[AutoSlave]"), // Fixed
    new ItemReq("Thrust",                  "ThrustComponent",          1000, "[AutoSlave]"), // Fixed
    new ItemReq("Reactor",                 "ReactorComponent",          500, "[AutoSlave]"), // Fixed
    new ItemReq("GravityGenerator",        "GravityGeneratorComponent", 500, "[AutoSlave]"), // Fixed
    new ItemReq("Medical",                 "MedicalComponent",           50, "[AutoSlave]"), // Fixed
    new ItemReq("SolarCell",               "SolarCell",                1500, "[AutoSlave]"),
    new ItemReq("PowerCell",               "PowerCell",                 250, "[AutoSlave]"),

    // MODDED / TIER 2 (These usually match, but verify via debug LCD if issues persist)
    new ItemReq("TitaniumPlate",           "TitaniumPlate",           5000, "[AutoSlave]"),
    new ItemReq("CompositePlate",          "CompositePlate",          3000, "[AutoSlave]"),
    new ItemReq("TitaniumTube",            "TitaniumTube",            2000, "[AutoSlave]"),
    new ItemReq("OctocoreComponent",       "OctocoreComponent",        500, "[AutoSlave]"),
    new ItemReq("AdvancedMotor",           "AdvancedMotor",           2000, "[AutoSlave]"),
    new ItemReq("Circuit",                 "Circuit",                 1000, "[AutoSlave]"),
    new ItemReq("CPU",                     "CPU",                      500, "[AutoSlave]"),
    new ItemReq("Magnet",                  "Magnet",                   500, "[AutoSlave]"),
    new ItemReq("Superconductor",          "Superconductor",           450, "[AutoSlave]"),
    new ItemReq("UltraConductor",          "UltraConductor",           150, "[AutoSlave]"),
    new ItemReq("Heatsink",                "Heatsink",                 300, "[AutoSlave]"),
    new ItemReq("LED",                     "LED",                      200, "[AutoSlave]"),
    new ItemReq("Kevlar",                  "Kevlar",                   200, "[AutoSlave]"),
    new ItemReq("Nylon",                   "Nylon",                    300, "[AutoSlave]"),
    new ItemReq("Polycarbonate",           "Polycarbonate",            500, "[AutoSlave]"),
    new ItemReq("Tyre",                    "Tyre",                     500, "[AutoSlave]"),
    new ItemReq("SiTyre",                  "SiTyre",                    50, "[AutoSlave]"),
    new ItemReq("CopperWire",              "CopperWire",              1000, "[AutoSlave]"),
    new ItemReq("GoldWire",                "GoldWire",                1000, "[AutoSlave]"),
    new ItemReq("SteelWire",               "SteelWire",                400, "[AutoSlave]"),
    new ItemReq("Optic",                   "Optic",                    200, "[AutoSlave]"),

    // CONVERSIONS
    new ItemReq("OilToPlastic",            "OilToPlastic",            2000, "[AutoSlave]"),
    new ItemReq("OilToRubber",             "OilToRubber",             2000, "[AutoSlave]"),
    new ItemReq("IceToPerchlorate",        "IceToPerchlorate",        3000, "[AutoSlave]"),

    // === [FuelAssembly] CHEMIST ===
    new ItemReq("Kerosene",                "Kerosene",                2000, "[FuelAssembly]"),
    new ItemReq("Gunpowder",               "Gunpowder",              10000, "[FuelAssembly]"),
    new ItemReq("Flashpowder",             "Flashpowder",            10000, "[FuelAssembly]"),
};

// --- GLOBALS ---
Dictionary<string, List<IMyAssembler>> assemblers = new Dictionary<string, List<IMyAssembler>>();
List<IMyTerminalBlock> storage = new List<IMyTerminalBlock>();
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMyGasTank> gasTanks = new List<IMyGasTank>();

Dictionary<string, double> inventory = new Dictionary<string, double>();     // InvId => amount
Dictionary<string, double> queuedCounts = new Dictionary<string, double>();  // BpId => amount

long tick = 0;
int debugPage = 0;
int debugTimer = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10; // Fast tick, but we work sparingly
    RefreshCache();
}

public void Main(string argument, UpdateType updateSource)
{
    tick++;

    // 1. Argument Handling (Immediate)
    if (!string.IsNullOrWhiteSpace(argument))
    {
        switch (argument.Trim().ToUpper())
        {
            case "RESET":
                DoReset();
                return;
            case "REINIT":
                RefreshCache();
                Echo("Reinitialized block cache.");
                return;
        }
    }

    // 2. Scheduled Tasks (Time Sliced)
    
    // Task A: Refresh Blocks (Rarely)
    if (tick % CACHE_INTERVAL == 0) RefreshCache();

    // Task B: Scan Inventory (Every ~5s, Offset 0)
    if (tick % SCAN_INTERVAL == 0) ScanInventory();

    // Task C: Scan Queues (Every ~5s, Offset 10)
    if ((tick + 10) % SCAN_INTERVAL == 0) ScanQueues();

    // Task D: Manage Production (Every ~20s, Offset 20)
    if ((tick + 20) % PROD_INTERVAL == 0) ManageProduction();

    // Task E: Update LCDs (Every ~1s)
    if (tick % LCD_INTERVAL == 0)
    {
        // Debug page scrolling logic
        debugTimer++;
        if (debugTimer > DEBUG_PAGE_INTERVAL) // every ~5s
        {
            debugTimer = 0;
            debugPage++;
        }
        UpdateStatusDisplay();
        UpdateDebugDisplay();
    }
}

void DoReset()
{
    foreach (var list in assemblers.Values)
        foreach (var asm in list)
            asm.ClearQueue();
    queuedCounts.Clear();
    Echo("!!! WIPED !!!");
}

void RefreshCache()
{
    assemblers.Clear();
    var all = new List<IMyAssembler>();
    GridTerminalSystem.GetBlocksOfType(all, a => a.IsSameConstructAs(Me));

    assemblers["[AutoAssembler]"]  = all.Where(a => a.CustomName.Contains("[AutoAssembler]")).ToList();
    assemblers["[AutoSlave]"]      = all.Where(a => a.CustomName.Contains("[AutoSlave]")).ToList();
    assemblers["[FuelAssembly]"]   = all.Where(a => a.CustomName.Contains("[FuelAssembly]")).ToList();
    assemblers["[AutoAmmunition]"] = all.Where(a => a.CustomName.Contains("[AutoAmmunition]")).ToList();

    storage.Clear();
    GridTerminalSystem.GetBlocksOfType(storage, b => b.IsSameConstructAs(Me) && b.HasInventory);

    batteries.Clear();
    GridTerminalSystem.GetBlocksOfType(batteries, b => b.IsSameConstructAs(Me));

    gasTanks.Clear();
    GridTerminalSystem.GetBlocksOfType(gasTanks, b => b.IsSameConstructAs(Me));
}

void ScanInventory()
{
    inventory.Clear();

    foreach (var block in storage)
    {
        for (int i = 0; i < block.InventoryCount; i++)
        {
            var inv = block.GetInventory(i);
            if (inv == null || inv.ItemCount == 0) continue;

            var items = new List<MyInventoryItem>();
            inv.GetItems(items);

            foreach (var item in items)
            {
                string invId = item.Type.SubtypeId;
                double amt = (double)item.Amount;

                AddInv(invId, amt);

                // Inventory aliasing only (explicit)
                string alias;
                if (ALIASES.TryGetValue(invId, out alias))
                    AddInv(alias, amt);
            }
        }
    }
}

void AddInv(string key, double val)
{
    double cur;
    if (inventory.TryGetValue(key, out cur)) inventory[key] = cur + val;
    else inventory[key] = val;
}

void ScanQueues()
{
    queuedCounts.Clear();

    foreach (var asmList in assemblers.Values)
    {
        foreach (var asm in asmList)
        {
            if (!asm.IsWorking) continue;

            var queue = new List<MyProductionItem>();
            asm.GetQueue(queue);

            foreach (var item in queue)
            {
                string bp = item.BlueprintId.SubtypeName;
                double amt = (double)item.Amount;

                double cur;
                if (queuedCounts.TryGetValue(bp, out cur)) queuedCounts[bp] = cur + amt;
                else queuedCounts[bp] = amt;
            }
        }
    }
}

void ManageProduction()
{
    foreach (var req in CATALOG)
    {
        double current = inventory.ContainsKey(req.InvId) ? inventory[req.InvId] : 0;
        if (current >= req.Target) continue;

        double pending = queuedCounts.ContainsKey(req.BpId) ? queuedCounts[req.BpId] : 0;
        if (pending > 10) continue; // already queued enough

        int needed = req.Target - (int)current;
        if (needed <= 0) continue;

        int batch = Math.Min(needed, 100);

        if (QueueBlueprint(req.BpId, batch, req.Tag))
        {
            queuedCounts[req.BpId] = pending + batch;
        }
    }
}

bool QueueBlueprint(string bpSubtype, int amount, string tag)
{
    List<IMyAssembler> group;
    if (!assemblers.TryGetValue(tag, out group) || group.Count == 0) return false;

    IMyAssembler best = null;
    int minQ = int.MaxValue;

    foreach (var asm in group)
    {
        if (!asm.IsFunctional || !asm.Enabled) continue;

        var q = new List<MyProductionItem>();
        asm.GetQueue(q);

        if (q.Count < minQ)
        {
            minQ = q.Count;
            best = asm;
        }
    }

    if (best == null) return false;

    try
    {
        var bp = MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/{bpSubtype}");
        best.AddQueueItem(bp, (MyFixedPoint)amount);
        return true;
    }
    catch
    {
        return false;
    }
}

void UpdateStatusDisplay()
{
    var lcd = FindLCD(LCD_NAME);
    if (lcd == null) return;

    lcd.ContentType = ContentType.TEXT_AND_IMAGE;

    // Batteries
    double totalBatt = 0, maxBatt = 0;
    foreach (var b in batteries)
    {
        totalBatt += b.CurrentStoredPower;
        maxBatt += b.MaxStoredPower;
    }
    double battPct = (maxBatt > 0) ? (totalBatt / maxBatt) * 100 : 0;

    // Tanks
    double h2Sum = 0, o2Sum = 0;
    int h2Count = 0, o2Count = 0;

    foreach (var t in gasTanks)
    {
        bool isH2 = t.BlockDefinition.SubtypeId.Contains("Hydrogen");
        if (isH2) { h2Sum += t.FilledRatio; h2Count++; }
        else { o2Sum += t.FilledRatio; o2Count++; }
    }

    double h2Pct = (h2Count > 0) ? (h2Sum / h2Count) * 100 : 0;
    double o2Pct = (o2Count > 0) ? (o2Sum / o2Count) * 100 : 0;

    double fuel = inventory.ContainsKey("Kerosene") ? inventory["Kerosene"] : 0;
    double canvas = inventory.ContainsKey("Canvas") ? inventory["Canvas"] : 0;
    double ice = inventory.ContainsKey("Ice") ? inventory["Ice"] : 0;

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("╔═ BASE VITALS ═══════════════╗");
    sb.AppendLine($"║ PWR: {DrawBar(battPct)} {battPct:F0}%");
    sb.AppendLine($"║ H2:  {DrawBar(h2Pct)} {h2Pct:F0}%");
    sb.AppendLine($"║ O2:  {DrawBar(o2Pct)} {o2Pct:F0}%");
    sb.AppendLine("╚═════════════════════════════╝");

    sb.AppendLine("FUEL STORES:");
    sb.AppendLine($" Kerosene: {fuel:N0} L");
    sb.AppendLine($" Ice:      {ice:N0} kg");
    sb.AppendLine($" Canvas:   {canvas:N0}");
    sb.AppendLine();

    sb.AppendLine("CRITICAL LOW STOCK:");
    var low = CATALOG
        .Select(c => new
        {
            Req = c,
            Curr = inventory.ContainsKey(c.InvId) ? inventory[c.InvId] : 0
        })
        .Where(x => x.Curr < x.Req.Target * THRESHOLD)
        .OrderBy(x => (x.Req.Target > 0) ? (x.Curr / (double)x.Req.Target) : 1.0)
        .Take(6);

    foreach (var x in low)
    {
        string id = x.Req.InvId;
        string n = id.Length > 12 ? id.Substring(0, 12) : id;
        sb.AppendLine($"! {n,-12} {x.Curr,4:0}/{x.Req.Target,-4} [{TrimTag(x.Req.Tag)}]");
    }

    lcd.WriteText(sb.ToString());
}

void UpdateDebugDisplay()
{
    var lcd = FindLCD(DEBUG_LCD_NAME);
    if (lcd == null) return;

    lcd.ContentType = ContentType.TEXT_AND_IMAGE;

    var allItems = inventory.OrderBy(x => x.Key).ToList();
    int pageSize = 18;
    int totalPages = Math.Max(1, (int)Math.Ceiling((double)allItems.Count / pageSize));
    if (debugPage >= totalPages) debugPage = 0;

    var pageItems = allItems.Skip(debugPage * pageSize).Take(pageSize);

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"╔═ DEBUG INV ═══════ PAGE {debugPage + 1}/{totalPages} ╗");
    foreach (var kvp in pageItems)
    {
        string name = kvp.Key.Length > 18 ? kvp.Key.Substring(0, 18) : kvp.Key;
        sb.AppendLine($" {name,-18} : {kvp.Value:N0}");
    }

    sb.AppendLine();
    sb.AppendLine("QUEUED (Top 5):");
    foreach (var q in queuedCounts.OrderByDescending(x => x.Value).Take(5))
    {
        string name = q.Key.Length > 18 ? q.Key.Substring(0, 18) : q.Key;
        sb.AppendLine($" {name,-18} : {q.Value:N0}");
    }

    lcd.WriteText(sb.ToString());
}

string DrawBar(double pct)
{
    int bars = (int)(pct / 10);
    if (bars < 0) bars = 0;
    if (bars > 10) bars = 10;
    return $"[{new string('|', bars).PadRight(10, '.')}]";
}

string TrimTag(string tag)
{
    if (string.IsNullOrEmpty(tag)) return "";
    return tag.Replace("[", "").Replace("]", "");
}

IMyTextSurface FindLCD(string name)
{
    var b = GridTerminalSystem.GetBlockWithName(name);
    if (b == null) return null;

    var s = b as IMyTextSurface;
    if (s != null) return s;

    var p = b as IMyTextSurfaceProvider;
    if (p != null) return p.GetSurface(0);

    return null;
}

public class ItemReq
{
    public string InvId;
    public string BpId;
    public int Target;
    public string Tag;

    public ItemReq(string invId, string bpId, int target, string tag)
    {
        InvId = invId;
        BpId = bpId;
        Target = target;
        Tag = tag;
    }
}