/*
 * BASE HEARTBEAT v6.2 - CARBON STEEL FIX
 * * FEATURES:
 * 1. Corrected "Steel" Blueprint name.
 * 2. Paged Debug & Status Displays.
 * 3. Role Separation ([AutoSlave] for Steel).
 */

const string LCD_NAME = "Status LCD";
const string DEBUG_LCD_NAME = "Debug LCD"; 
const double THRESHOLD = 0.40; 
const int CHECK_INTERVAL = 60; 

// --- MASTER CATALOG ---
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
    {"Powerbank", "SuitPowerbank_2"}
    // Removed "Steel" alias because Inventory Name == Blueprint Name
};

// --- GLOBALS ---
Dictionary<string, List<IMyAssembler>> assemblers = new Dictionary<string, List<IMyAssembler>>();
List<IMyTerminalBlock> storage = new List<IMyTerminalBlock>();
Dictionary<string, double> inventory = new Dictionary<string, double>();
Dictionary<string, double> queuedCounts = new Dictionary<string, double>(); 

// Vitals Globals
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMyGasTank> gasTanks = new List<IMyGasTank>();
List<IMyPowerProducer> engines = new List<IMyPowerProducer>();

int cycle = 0;
int debugPage = 0;
int debugTimer = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    Init();
}

void Init()
{
    assemblers.Clear();
    List<IMyAssembler> all = new List<IMyAssembler>();
    GridTerminalSystem.GetBlocksOfType(all, a => a.IsSameConstructAs(Me));
    
    assemblers["[AutoAssembler]"] = all.Where(a => a.CustomName.Contains("[AutoAssembler]")).ToList();
    assemblers["[AutoSlave]"] = all.Where(a => a.CustomName.Contains("[AutoSlave]")).ToList();
    assemblers["[FuelAssembly]"] = all.Where(a => a.CustomName.Contains("[FuelAssembly]")).ToList();
    
    storage.Clear();
    GridTerminalSystem.GetBlocksOfType(storage, b => b.IsSameConstructAs(Me) && b.HasInventory);
    
    batteries.Clear();
    GridTerminalSystem.GetBlocksOfType(batteries, b => b.IsSameConstructAs(Me));
    
    gasTanks.Clear();
    GridTerminalSystem.GetBlocksOfType(gasTanks, b => b.IsSameConstructAs(Me));

    engines.Clear();
    GridTerminalSystem.GetBlocksOfType(engines, b => b.IsSameConstructAs(Me) && b.BlockDefinition.SubtypeId.Contains("Engine"));
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToUpper() == "RESET") {
        foreach (var list in assemblers.Values) foreach (var asm in list) asm.ClearQueue();
        queuedCounts.Clear(); Echo("!!! WIPED !!!"); return; 
    }

    cycle++;
    if (cycle % 300 == 0) Init(); 
    
    ScanInventory();
    ScanQueues();
    
    if (cycle % CHECK_INTERVAL == 0) ManageProduction();
    
    debugTimer++;
    if(debugTimer > 40) { 
        debugTimer = 0;
        debugPage++;
    }
    
    UpdateStatusDisplay();
    UpdateDebugDisplay();
}

void ScanInventory()
{
    inventory.Clear();
    foreach (var block in storage) {
        for (int i = 0; i < block.InventoryCount; i++) {
            var inv = block.GetInventory(i);
            if (inv == null) continue;
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);
            foreach (var item in items) {
                string id = item.Type.SubtypeId;
                double amt = (double)item.Amount;
                AddInv(id, amt);
                if (ALIASES.ContainsKey(id)) AddInv(ALIASES[id], amt);
                if (!id.EndsWith("Component")) AddInv(id + "Component", amt);
            }
        }
    }
}

void AddInv(string key, double val) {
    if (inventory.ContainsKey(key)) inventory[key] += val;
    else inventory[key] = val;
}

void ScanQueues() {
    queuedCounts.Clear();
    foreach (var asmList in assemblers.Values) {
        foreach (var asm in asmList) {
            if(!asm.IsWorking) continue;
            var queue = new List<MyProductionItem>();
            asm.GetQueue(queue);
            foreach (var item in queue) {
                string bp = item.BlueprintId.SubtypeName;
                double amt = (double)item.Amount;
                if (queuedCounts.ContainsKey(bp)) queuedCounts[bp] += amt;
                else queuedCounts[bp] = amt;
            }
        }
    }
}

void ManageProduction() {
    foreach (var req in CATALOG) {
        double current = inventory.ContainsKey(req.ID) ? inventory[req.ID] : 0;
        if (current >= req.Target) continue;
        double pending = queuedCounts.ContainsKey(req.ID) ? queuedCounts[req.ID] : 0;
        if (pending > 10) continue; 
        
        int needed = req.Target - (int)current;
        int batch = Math.Min(needed, 100); 
        
        if (Queue(req.ID, batch, req.Tag)) {
            if (queuedCounts.ContainsKey(req.ID)) queuedCounts[req.ID] += batch;
            else queuedCounts[req.ID] = batch;
        }
    }
}

bool Queue(string name, int amount, string tag) {
    if (!assemblers.ContainsKey(tag) || assemblers[tag].Count == 0) return false;
    IMyAssembler best = null; int minQ = int.MaxValue;
    foreach (var asm in assemblers[tag]) {
        if (!asm.IsWorking || !asm.IsFunctional) continue;
        var q = new List<MyProductionItem>(); asm.GetQueue(q);
        if (q.Count < minQ) { minQ = q.Count; best = asm; }
    }
    if (best == null) return false;
    try {
        var bp = MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/{name}");
        best.AddQueueItem(bp, (MyFixedPoint)amount);
        return true;
    } catch { return false; }
}

void UpdateStatusDisplay() {
    var lcd = FindLCD(LCD_NAME);
    if (lcd == null) return;
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    
    double totalBatt = 0, maxBatt = 0;
    foreach(var b in batteries) { totalBatt += b.CurrentStoredPower; maxBatt += b.MaxStoredPower; }
    double battPct = maxBatt > 0 ? (totalBatt / maxBatt) * 100 : 0;
    
    double h2 = 0, o2 = 0;
    foreach(var t in gasTanks) {
        if(t.BlockDefinition.SubtypeId.Contains("Hydrogen")) h2 += t.FilledRatio;
        else o2 += t.FilledRatio;
    }
    if(gasTanks.Count > 0) { h2 /= gasTanks.Count(t => t.BlockDefinition.SubtypeId.Contains("Hydrogen")); h2*=100; }
    
    double fuel = inventory.ContainsKey("Kerosene") ? inventory["Kerosene"] : 0;
    double canvas = inventory.ContainsKey("Canvas") ? inventory["Canvas"] : 0;
    double ice = inventory.ContainsKey("Ice") ? inventory["Ice"] : 0;

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("╔═ BASE VITALS ═══════════════╗");
    sb.AppendLine($"║ PWR: {DrawBar(battPct)} {battPct:F0}%");
    sb.AppendLine($"║ H2:  {DrawBar(h2)} {h2:F0}%");
    sb.AppendLine("╚═════════════════════════════╝");
    sb.AppendLine($"FUEL STORES:");
    sb.AppendLine($" Kerosene: {fuel:N0} L");
    sb.AppendLine($" Ice:      {ice:N0} kg");
    sb.AppendLine($" Canvas:   {canvas:N0}");
    sb.AppendLine();
    sb.AppendLine("CRITICAL LOW STOCK:");
    var low = CATALOG
        .Where(c => (inventory.ContainsKey(c.ID) ? inventory[c.ID] : 0) < c.Target * THRESHOLD)
        .OrderBy(c => (inventory.ContainsKey(c.ID) ? inventory[c.ID] : 0) / (double)c.Target)
        .Take(6);
    foreach (var item in low) {
        double curr = inventory.ContainsKey(item.ID) ? inventory[item.ID] : 0;
        string n = item.ID.Length > 12 ? item.ID.Substring(0, 12) : item.ID;
        sb.AppendLine($"! {n,-12} {curr,4}/{item.Target,-4}");
    }
    lcd.WriteText(sb.ToString());
}

void UpdateDebugDisplay() {
    var lcd = FindLCD(DEBUG_LCD_NAME);
    if (lcd == null) return;
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    
    var allItems = inventory.OrderBy(x => x.Key).ToList();
    int pageSize = 18;
    int totalPages = (int)Math.Ceiling((double)allItems.Count / pageSize);
    if (debugPage >= totalPages) debugPage = 0;
    
    var pageItems = allItems.Skip(debugPage * pageSize).Take(pageSize);

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"╔═ DEBUG INV ═══════ PAGE {debugPage+1}/{totalPages} ╗");
    foreach(var kvp in pageItems) {
        string name = kvp.Key.Length > 18 ? kvp.Key.Substring(0,18) : kvp.Key;
        sb.AppendLine($" {name,-18} : {kvp.Value:N0}");
    }
    lcd.WriteText(sb.ToString());
}

string DrawBar(double pct) {
    int bars = (int)(pct / 10);
    return $"[{new string('|', bars).PadRight(10, '.')}]";
}

IMyTextSurface FindLCD(string name) {
    var b = GridTerminalSystem.GetBlockWithName(name);
    if (b is IMyTextSurface) return (IMyTextSurface)b;
    if (b is IMyTextSurfaceProvider) return ((IMyTextSurfaceProvider)b).GetSurface(0);
    return null;
}

public class ItemReq {
    public string ID; public int Target; public string Tag;
    public ItemReq(string id, int t, string tag) { ID = id; Target = t; Tag = tag; }
}
