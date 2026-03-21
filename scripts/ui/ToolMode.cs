namespace EndfieldZero.UI;

/// <summary>
/// Defines the current interaction tool mode.
/// Determines how mouse clicks behave in the game world.
/// </summary>
public enum ToolMode
{
    /// <summary>Default: left-click selects units, right-click moves them.</summary>
    Select,

    /// <summary>Left-click/drag designates blocks for mining.</summary>
    Mine,

    /// <summary>Left-click/drag designates blocks for construction.</summary>
    Construct,

    /// <summary>Left-click/drag designates blocks for growing crops.</summary>
    Grow,

    /// <summary>Left-click/drag cancels existing job designations.</summary>
    Cancel,
}
