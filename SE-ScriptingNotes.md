The API documentation is the gold standard for coding in Space Engineers.

https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/index.html


The WIKI contains general information regarding how to code in SE, but the API is the gospel. If something said in the wiki is not present in the API, it's outdated information and should be ignored for the most part.
https://spaceengineers.fandom.com/wiki/Scripting#Requirements

---

## Key Scripting Interfaces

Based on our first script, here are some important interfaces and how to use them.

### IMyProjector
Used to control blueprint projections.
-   `ProjectionOffset` (Vector3I): Gets or sets the projector's position offset.
-   `ProjectionRotation` (Vector3I): Gets or sets the projector's rotation. The values are integers representing 90-degree increments.
-   `UpdateOffsetAndRotation()`: IMPORTANT! You must call this method after changing `ProjectionOffset` or `ProjectionRotation` to apply the changes.
-   `IsProjecting` (bool): Read-only property to check if the projector is currently projecting a blueprint.

Example:
```csharp
// Move projection forward by one block
myProjector.ProjectionOffset += new Vector3I(0, 0, 1);
myProjector.UpdateOffsetAndRotation();
```

### IMyTextSurface
This is the interface for any block that has a screen or text display area.
-   `WriteText(string text, bool append = false)`: Writes text to the surface.
-   `ContentType` (enum): Set to `ContentType.TEXT_AND_IMAGE` to enable text rendering.
-   `BackgroundColor` (Color): Sets the background color of the display.
-   `FontColor` (Color): Sets the color of the text.

### IMyTextSurfaceProvider
Some blocks have multiple text surfaces (e.g., a cockpit with several LCDs). This interface provides access to them.
-   `SurfaceCount` (int): The number of available text surfaces.
-   `GetSurface(int index)`: Returns an `IMyTextSurface` object for the given index.

**Assumption:** Blocks like `IMyButtonPanel` can be treated as an `IMyTextSurfaceProvider`, where each button corresponds to a text surface that can be accessed by an index. This allows us to change the text and background color of individual buttons to provide feedback to the player.

Example of finding a text surface (from BaseHeartbeat.cs):
```csharp
IMyTextSurface FindLCD(string name) {
    var b = GridTerminalSystem.GetBlockWithName(name);
    if (b is IMyTextSurface) return (IMyTextSurface)b;
    if (b is IMyTextSurfaceProvider) return ((IMyTextSurfaceProvider)b).GetSurface(0);
    return null;
}
```