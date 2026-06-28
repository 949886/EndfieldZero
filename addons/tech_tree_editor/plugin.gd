@tool
extends EditorPlugin

var _editor_control: Control
var _bottom_button: Button


func _enter_tree() -> void:
    _create_editor()
    add_tool_menu_item("Open Technology Tree Editor", _open_editor)


func _exit_tree() -> void:
    remove_tool_menu_item("Open Technology Tree Editor")

    if _editor_control != null:
        remove_control_from_bottom_panel(_editor_control)
        _editor_control.queue_free()
        _editor_control = null
        _bottom_button = null


func _create_editor() -> void:
    if _editor_control != null:
        return

    var editor_script := load("res://addons/tech_tree_editor/TechnologyTreeEditorDock.cs")
    if editor_script == null:
        push_error("Technology Tree Editor: failed to load TechnologyTreeEditorDock.cs")
        return

    _editor_control = editor_script.new()
    if _editor_control == null:
        push_error("Technology Tree Editor: failed to instantiate editor dock")
        return

    _editor_control.name = "Technology Tree"
    _bottom_button = add_control_to_bottom_panel(_editor_control, "Technology Tree")
    call_deferred("_open_editor")


func _open_editor() -> void:
    if _editor_control == null:
        _create_editor()

    if _editor_control != null:
        make_bottom_panel_item_visible(_editor_control)
