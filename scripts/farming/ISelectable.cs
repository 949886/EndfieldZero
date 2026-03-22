namespace EndfieldZero.Farming;

/// <summary>
/// Interface for entities that can be selected by the player.
/// Implemented by Pawn, CropInstance, BuildingInstance.
/// </summary>
public interface ISelectable
{
    bool IsSelected { get; set; }

    /// <summary>Short title shown in the info panel header.</summary>
    string SelectionTitle { get; }

    /// <summary>Multi-line detail text for the info panel body.</summary>
    string SelectionInfo { get; }

    /// <summary>World position for camera/UI targeting.</summary>
    Godot.Vector3 GlobalPosition { get; }
}
