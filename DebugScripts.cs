/*
 * THE AUDITOR - MASTER INVENTORY LIST
 * * PURPOSE: Scans grid and lists EVERY item by category.
 * * FEATURES: Auto-scrolls long lists.
 */

const string LCD_NAME = "Inventory LCD";
const int SCROLL_SPEED = 2; // Lines to scroll per update

// --- GLOBALS ---
Dictionary<string, double> comps = new Dictionary<string, double>();
Dictionary<string, double> ingots = new Dictionary<string, double>();
Dictionary<string, double> ores = new Dictionary<string, double>();
Dictionary<string, double> ammo = new Dictionary<string, double>();
Dictionary<string, double> tools = new Dictionary<string, double>();

int scrollLine = 0;
int maxLines = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // Updates every ~1.6s
}

public void Main(string argument, UpdateType updateSource)
{
    ScanInventory();
    UpdateDisplay();
}

void ScanInventory()
{
    // Clear previous counts
    comps.Clear(); ingots.Clear(); ores.Clear(); ammo.Clear(); tools.Clear();

    var blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType(blocks, b => b.IsSameConstructAs(Me) && b.HasInventory);

    foreach (var block in blocks)
    {
        for (int i = 0; i < block.InventoryCount; i++)
        {
            var inv = block.GetInventory(i);
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);

            foreach (var item in items)
            {
                string name = item.Type.SubtypeId;
                string type = item.Type.TypeId.ToString();
                double amount = (double)item.Amount;

                if (type.EndsWith("Component")) AddTo(comps, name, amount);
                else if (type.EndsWith("Ingot")) AddTo(ingots, name, amount);
                else if (type.EndsWith("Ore")) AddTo(ores, name, amount);
                else if (type.EndsWith("AmmoMagazine")) AddTo(ammo, name, amount);
                else AddTo(tools, name, amount);
            }
        }
    }
}

void AddTo(Dictionary<string, double> dict, string key, double val)
{
    if (dict.ContainsKey(key)) dict[key] += val;
    else dict[key] = val;
}

void UpdateDisplay()
{
    var lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextSurface;
    if (lcd == null) return;
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.Font = "Monospace";
    lcd.FontSize = 0.5f;

    // Build the Full List
    StringBuilder content = new StringBuilder();
    
    content.AppendLine("=== ORES ===");
    foreach(var i in ores.OrderBy(x => x.Key)) content.AppendLine($" {i.Key,-18} : {i.Value:N0}");
    content.AppendLine();

    content.AppendLine("=== INGOTS ===");
    foreach(var i in ingots.OrderBy(x => x.Key)) content.AppendLine($" {i.Key,-18} : {i.Value:N0}");
    content.AppendLine();

    content.AppendLine("=== COMPONENTS ===");
    foreach(var i in comps.OrderBy(x => x.Key)) content.AppendLine($" {i.Key,-18} : {i.Value:N0}");
    content.AppendLine();

    content.AppendLine("=== AMMO & TOOLS ===");
    foreach(var i in ammo.OrderBy(x => x.Key)) content.AppendLine($" {i.Key,-18} : {i.Value:N0}");
    foreach(var i in tools.OrderBy(x => x.Key)) content.AppendLine($" {i.Key,-18} : {i.Value:N0}");

    // Handle Scrolling
    string[] lines = content.ToString().Split('\n');
    maxLines = lines.Length;
    
    // Calculate visible lines based on font size (approx 34 lines for 0.5 font)
    int visibleLines = 34; 
    
    if (maxLines > visibleLines)
    {
        scrollLine += SCROLL_SPEED;
        if (scrollLine > maxLines - visibleLines) scrollLine = 0; // Reset to top
    }
    else
    {
        scrollLine = 0;
    }

    // Extract Visible Section
    StringBuilder view = new StringBuilder();
    view.AppendLine("╔═ MASTER INVENTORY ══════════╗");
    for (int i = scrollLine; i < Math.Min(scrollLine + visibleLines, maxLines); i++)
    {
        view.AppendLine(lines[i].TrimEnd());
    }
    
    // Add Footer if scrolling
    if (maxLines > visibleLines) 
        view.AppendLine($"... Scrolling ({scrollLine}/{maxLines - visibleLines}) ...");

    lcd.WriteText(view.ToString());
}


/*
 * ASSEMBLER DIAGNOSTIC TOOL
 * * PURPOSE: force-lists ALL assemblers to debug visibility issues.
 * * OUTPUT: Custom Data
 */

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    var assemblers = new List<IMyAssembler>();
    // Get ALL assemblers on this construct, even if broken/off
    GridTerminalSystem.GetBlocksOfType(assemblers, b => b.IsSameConstructAs(Me));

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"=== DIAGNOSTIC REPORT ===");
    sb.AppendLine($"Time: {DateTime.Now.ToShortTimeString()}");
    sb.AppendLine($"Assemblers Found: {assemblers.Count}");
    sb.AppendLine("=========================");
    sb.AppendLine();

    if (assemblers.Count == 0)
    {
        sb.AppendLine("CRITICAL WARNING:");
        sb.AppendLine("No assemblers found!");
        sb.AppendLine("1. Check if PB is on the same grid.");
        sb.AppendLine("2. Check ownership settings.");
    }

    foreach (var asm in assemblers)
    {
        sb.AppendLine($"[{asm.CustomName}]");
        
        // Status Checks
        if (!asm.IsFunctional) sb.Append(" (DAMAGED)");
        if (!asm.Enabled) sb.Append(" (OFF)");
        if (!asm.IsWorking && asm.Enabled && asm.IsFunctional) sb.Append(" (LOW POWER/IDLE)");
        sb.AppendLine();

        // Queue Check
        var queue = new List<MyProductionItem>();
        asm.GetQueue(queue);

        if (queue.Count == 0)
        {
            sb.AppendLine(" - Status: QUEUE EMPTY");
            
            // Check Input Inventory (Why isn't it producing?)
            var input = asm.GetInventory(0);
            if (input.ItemCount > 0)
            {
                sb.AppendLine(" - Input Inv: Has Materials");
            }
            else
            {
                sb.AppendLine(" - Input Inv: Empty");
            }
        }
        else
        {
            foreach (var item in queue)
            {
                sb.AppendLine($" - {item.BlueprintId.SubtypeName}: {item.Amount:N0}");
            }
        }
        sb.AppendLine("- - - - - - - - -");
    }

    // Output
    Echo($"Found: {assemblers.Count} assemblers.");
    Echo("Check Custom Data for details.");
    Me.CustomData = sb.ToString();
}