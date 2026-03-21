namespace EndfieldZero.UI;

/// <summary>
/// Available tool modes for player interaction.
///
///   Select    (Q) — Select and command pawns
///   Mine      (M) — Designate blocks for mining
///   Construct (B) — Place building blueprints
///   Grow      (G) — Designate grow areas
///   Zone      (Z) — Create/manage zones
///   Cancel    (X) — Cancel designations/blueprints/zones
/// </summary>
public enum ToolMode
{
    Select,
    Mine,
    Construct,
    Grow,
    Zone,
    Cancel,
}
