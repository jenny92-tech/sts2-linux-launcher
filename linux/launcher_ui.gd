# GDScript launcher UI — no .NET dependency. C# patches live in
# sts2_compat.dll and are loaded by the game process, not the launcher.
#
# Exit codes (consumed by launcher.sh):
#   42  Start Game
#    0  Quit to Menu

extends Control

const EXIT_START_GAME    := 42
const EXIT_QUIT_TO_MENU  := 0

const LANGUAGES : Array[String] = ["en_US", "zh_CN"]
const LANG_LABELS := {
	"en_US": "English",
	"zh_CN": "Chinese",
}

# Nintendo layout → A on right (BTN_EAST), Xbox → A on bottom (BTN_SOUTH).
# We rewrite input_remap.cfg so silkscreen-A always maps to JoyButton::A.
# Takes effect on next launcher restart (godot loads cfg once at startup).
const TOGGLES : Array[String] = ["off", "on"]
const TOGGLE_LABELS := {
	"off": "Off",
	"on":  "On",
}

# Quality picker → SLL_QUALITY env var, read by sts2_compat.dll.
# Decoupled from RAM: loading strategy (lazy vs full) is auto-decided
# by the patcher based on /proc/meminfo, not a user choice.
const QUALITIES : Array[String] = ["smooth", "balanced", "quality"]

const STATE_FILE := "user://linux_launcher_state.json"
const ENV_FILE   := "user://launch_config.env"

var _state := {
	"schema_version": 11,
	"launch_count":   0,
	"last_action":    "(none)",
	"last_action_at": "",
	"language":       "zh_CN",
	"quality":        "balanced",
	"swap_ab":        "on",
	"swap_xy":        "off",
	"ui_lang":        "zh",
}

const STRINGS := {
	"detected":         {"en": "Detected %dx%d",    "zh": "检测到 %d×%d"},
	"language":         {"en": "Language:",         "zh": "语言:"},
	"quality":          {"en": "Quality:",          "zh": "画质:"},
	"q_smooth":         {"en": "Smooth",            "zh": "流畅"},
	"q_balanced":       {"en": "Balanced",          "zh": "均衡"},
	"q_quality":        {"en": "Fidelity",          "zh": "画质"},
	"swap_ab":          {"en": "Swap A/B:",         "zh": "换 A/B:"},
	"swap_xy":          {"en": "Swap X/Y:",         "zh": "换 X/Y:"},
	"start_game":       {"en": "Start Game",        "zh": "开始游戏"},
	"quit_menu":        {"en": "Quit to Menu",      "zh": "返回主菜单"},
	"off":              {"en": "Off",               "zh": "关"},
	"on":               {"en": "On",                "zh": "开"},
	"english":          {"en": "English",           "zh": "英文"},
	"chinese":          {"en": "Chinese",           "zh": "中文"},
}

func _t(key: String) -> String:
	var pair: Dictionary = STRINGS.get(key, {})
	return pair.get(_state.ui_lang, pair.get("en", key))

var _lang_button:    Button
var _quality_button: Button
var _swapab_button:  Button
var _swapxy_button:  Button
var _first_focus:    Control
var _center:         CenterContainer
var _ui_lang_button: Button


func _ready() -> void:
	_load_state()
	_install_font()
	_write_input_remap_cfg()
	_add_joypad_action_bindings()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	var bg_path := "res://launcher_bg.png"
	var bg: Control
	if FileAccess.file_exists(bg_path):
		var f := FileAccess.open(bg_path, FileAccess.READ)
		var img := Image.new()
		if img.load_png_from_buffer(f.get_buffer(f.get_length())) == OK:
			var tex_rect := TextureRect.new()
			tex_rect.texture = ImageTexture.create_from_image(img)
			tex_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
			bg = tex_rect
	if bg == null:
		var color_rect := ColorRect.new()
		color_rect.color = Color(0.07, 0.07, 0.09)
		bg = color_rect
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)

	var overlay := ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0, 0, 0, 0.35)
	overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(overlay)

	var credit := Label.new()
	credit.text = "© jenny92"
	credit.add_theme_font_size_override("font_size", 30)
	credit.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	_apply_outline(credit, 3)
	credit.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	credit.offset_left = -240
	credit.offset_top = -52
	credit.offset_right = -18
	credit.offset_bottom = -14
	credit.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	credit.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(credit)

	_center = CenterContainer.new()
	_center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(_center)
	_center.add_child(_build_layout())

	_ui_lang_button = Button.new()
	_ui_lang_button.text = "中" if _state.ui_lang == "en" else "EN"
	_ui_lang_button.custom_minimum_size = Vector2(64, 44)
	_ui_lang_button.focus_mode = Control.FOCUS_ALL
	_ui_lang_button.add_theme_font_size_override("font_size", 22)
	_apply_outline(_ui_lang_button, 3)
	_apply_button_theme(_ui_lang_button)
	_ui_lang_button.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_ui_lang_button.offset_left = -84
	_ui_lang_button.offset_top = 16
	_ui_lang_button.offset_right = -20
	_ui_lang_button.offset_bottom = 60
	_ui_lang_button.pressed.connect(_on_ui_lang_toggle)
	add_child(_ui_lang_button)

	call_deferred("_grab_initial_focus")
	set_process_input(true)
	push_warning("[SLL-GD] LauncherUI mounted (launch_count=%d, last=%s)" %
		[_state.launch_count, _state.last_action])


func _input(event: InputEvent) -> void:
	if event is InputEventJoypadButton:
		push_warning("[SLL-GD] JoypadButton device=%d button=%d pressed=%s" %
			[event.device, event.button_index, event.pressed])
	elif event is InputEventJoypadMotion:
		if absf(event.axis_value) > 0.5:
			push_warning("[SLL-GD] JoypadMotion device=%d axis=%d value=%.2f" %
				[event.device, event.axis, event.axis_value])
	elif event is InputEventKey:
		push_warning("[SLL-GD] Key keycode=%d physical=%d pressed=%s" %
			[event.keycode, event.physical_keycode, event.pressed])
	elif event is InputEventAction:
		push_warning("[SLL-GD] Action %s pressed=%s" % [event.action, event.pressed])


func _build_layout() -> VBoxContainer:
	var box := VBoxContainer.new()
	box.custom_minimum_size = Vector2(720, 540)
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 10)

	box.add_child(_make_title("STS2 Linux Launcher"))
	box.add_child(_make_subtitle(_t("detected") % [
		DisplayServer.window_get_size().x, DisplayServer.window_get_size().y]))
	box.add_child(_make_separator())

	var lang_labels := {"en_US": _t("english"), "zh_CN": _t("chinese")}
	var toggle_labels := {"off": _t("off"), "on": _t("on")}
	var quality_labels := {
		"smooth":   _t("q_smooth"),
		"balanced": _t("q_balanced"),
		"quality":  _t("q_quality"),
	}

	_lang_button = _make_row(_t("language"), LANGUAGES, _state.language,
		lang_labels,
		func(v: String) -> void: _state.language = v)
	_quality_button = _make_row(_t("quality"), QUALITIES, _state.quality,
		quality_labels,
		func(v: String) -> void: _state.quality = v)
	_swapab_button = _make_row(_t("swap_ab"), TOGGLES, _state.swap_ab,
		toggle_labels,
		func(v: String) -> void:
			_state.swap_ab = v
			_write_input_remap_cfg())
	_swapxy_button = _make_row(_t("swap_xy"), TOGGLES, _state.swap_xy,
		toggle_labels,
		func(v: String) -> void:
			_state.swap_xy = v
			_write_input_remap_cfg())
	box.add_child(_lang_button.get_parent())
	box.add_child(_quality_button.get_parent())
	box.add_child(_swapab_button.get_parent())
	box.add_child(_swapxy_button.get_parent())

	var start_btn := _make_button(_t("start_game"),
		func() -> void: _on_action("StartGame",  EXIT_START_GAME))
	box.add_child(start_btn)
	box.add_child(_make_button(_t("quit_menu"),
		func() -> void: _on_action("QuitToMenu", EXIT_QUIT_TO_MENU)))

	_first_focus = start_btn
	return box


func _apply_outline(node: Control, size: int = 4) -> void:
	node.add_theme_color_override("font_outline_color", Color.BLACK)
	node.add_theme_constant_override("outline_size", size)
	node.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.55))
	node.add_theme_constant_override("shadow_offset_x", 2)
	node.add_theme_constant_override("shadow_offset_y", 2)


func _apply_button_theme(btn: Button) -> void:
	btn.add_theme_color_override("font_color",          Color.WHITE)
	btn.add_theme_color_override("font_hover_color",    Color(1.0, 0.95, 1.0))
	btn.add_theme_color_override("font_pressed_color",  Color(0.95, 0.90, 1.0))
	btn.add_theme_color_override("font_focus_color",    Color.WHITE)
	btn.add_theme_color_override("font_disabled_color", Color(0.6, 0.6, 0.6))

	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(0.12, 0.08, 0.20, 0.72)
	normal.border_color = Color(1, 1, 1, 0.22)
	normal.set_border_width_all(1)
	normal.set_corner_radius_all(8)
	btn.add_theme_stylebox_override("normal", normal)

	var hover := StyleBoxFlat.new()
	hover.bg_color = Color(0.18, 0.12, 0.28, 0.85)
	hover.set_corner_radius_all(8)
	btn.add_theme_stylebox_override("hover", hover)

	var pressed := StyleBoxFlat.new()
	pressed.bg_color = Color(0.30, 0.18, 0.42, 0.92)
	pressed.set_corner_radius_all(8)
	btn.add_theme_stylebox_override("pressed", pressed)

	var focus := StyleBoxFlat.new()
	focus.bg_color = Color(0.55, 0.32, 0.85, 0.55)
	focus.border_color = Color(1.0, 0.85, 1.0, 1.0)
	focus.set_border_width_all(3)
	focus.set_corner_radius_all(8)
	btn.add_theme_stylebox_override("focus", focus)


func _make_row(label_text: String, values: Array[String], current: String,
		labels: Dictionary, on_change: Callable) -> Button:
	var row := HBoxContainer.new()
	row.custom_minimum_size = Vector2(640, 50)
	row.add_theme_constant_override("separation", 16)

	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(220, 0)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 22)
	label.add_theme_color_override("font_color", Color.WHITE)
	_apply_outline(label)
	row.add_child(label)

	var btn := Button.new()
	btn.custom_minimum_size = Vector2(280, 48)
	btn.focus_mode = Control.FOCUS_ALL
	btn.add_theme_font_size_override("font_size", 22)
	_apply_outline(btn)
	_apply_button_theme(btn)
	btn.text = "< %s >" % labels.get(current, current)
	btn.set_meta("current", current)

	var cycle := func(delta: int) -> void:
		var idx: int = values.find(btn.get_meta("current"))
		if idx < 0:
			idx = 0
		idx = posmod(idx + delta, values.size())
		var next: String = values[idx]
		btn.set_meta("current", next)
		btn.text = "< %s >" % labels.get(next, next)
		on_change.call(next)

	btn.pressed.connect(cycle.bind(1))

	btn.gui_input.connect(func(event: InputEvent) -> void:
		if event.is_action_pressed("ui_right"):
			cycle.call(1)
			btn.accept_event()
		elif event.is_action_pressed("ui_left"):
			cycle.call(-1)
			btn.accept_event()
	)
	row.add_child(btn)
	return btn


func _make_title(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 36)
	label.add_theme_color_override("font_color", Color.WHITE)
	_apply_outline(label, 5)
	return label


func _make_subtitle(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 18)
	label.add_theme_color_override("font_color", Color.WHITE)
	_apply_outline(label, 3)
	return label


func _make_button(text: String, on_press: Callable) -> Button:
	var btn := Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(280, 50)
	btn.focus_mode = Control.FOCUS_ALL
	btn.add_theme_font_size_override("font_size", 22)
	_apply_outline(btn)
	_apply_button_theme(btn)
	btn.pressed.connect(func() -> void:
		push_warning("[SLL-GD] Button '%s' pressed.fire" % text)
		on_press.call())
	return btn


func _make_separator() -> HSeparator:
	var sep := HSeparator.new()
	sep.custom_minimum_size = Vector2(0, 4)
	return sep


func _install_font() -> void:
	var path := "res://launcher_font_zh.ttf"
	if not FileAccess.file_exists(path):
		return
	var f := FileAccess.open(path, FileAccess.READ)
	var font := FontFile.new()
	font.data = f.get_buffer(f.get_length())
	var t := Theme.new()
	t.default_font = font
	self.theme = t


func _on_ui_lang_toggle() -> void:
	_state.ui_lang = "zh" if _state.ui_lang == "en" else "en"
	_save_state()
	_ui_lang_button.text = "中" if _state.ui_lang == "en" else "EN"
	for child in _center.get_children():
		child.queue_free()
	_center.add_child(_build_layout())
	call_deferred("_grab_lang_toggle_focus")


func _grab_lang_toggle_focus() -> void:
	if is_instance_valid(_ui_lang_button):
		_ui_lang_button.grab_focus()


func _make_panel() -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.07, 0.07, 0.09)
	return sb


func _add_joypad_action_bindings() -> void:
	var bindings := {
		"ui_accept": JOY_BUTTON_A,
		"ui_cancel": JOY_BUTTON_B,
		"ui_up":     JOY_BUTTON_DPAD_UP,
		"ui_down":   JOY_BUTTON_DPAD_DOWN,
		"ui_left":   JOY_BUTTON_DPAD_LEFT,
		"ui_right":  JOY_BUTTON_DPAD_RIGHT,
	}
	for action in bindings:
		var btn_idx: int = bindings[action]
		var already := false
		for ev in InputMap.action_get_events(action):
			if ev is InputEventJoypadButton and ev.button_index == btn_idx:
				already = true
				break
		if already:
			continue
		var event := InputEventJoypadButton.new()
		event.button_index = btn_idx
		event.device = -1   # -1 = any device (matches all joypads)
		InputMap.action_add_event(action, event)
	push_warning("[SLL-GD] joypad action bindings installed")


func _grab_initial_focus() -> void:
	if _first_focus:
		_first_focus.grab_focus()
	var ui_accept_events = InputMap.action_get_events("ui_accept")
	push_warning("[SLL-GD] ui_accept has %d binding(s):" % ui_accept_events.size())
	for ev in ui_accept_events:
		push_warning("[SLL-GD]   - %s" % ev.as_text())


func _on_action(action: String, exit_code: int) -> void:
	prints("[SLL-GD] Action '%s' → exit %d" % [action, exit_code])
	_state.launch_count += 1
	_state.last_action = action
	_state.last_action_at = Time.get_datetime_string_from_system(true)
	_save_state()
	if exit_code == EXIT_START_GAME:
		_write_env()
	get_tree().quit(exit_code)


func _load_state() -> void:
	if not FileAccess.file_exists(STATE_FILE):
		prints("[SLL-GD] no state file at %s, fresh start" % STATE_FILE)
		return
	var f := FileAccess.open(STATE_FILE, FileAccess.READ)
	if f == null:
		printerr("[SLL-GD] failed to open state for read: %s" % FileAccess.get_open_error())
		return
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	if parsed is Dictionary:
		for k in parsed:
			if _state.has(k):
				_state[k] = parsed[k]
		prints("[SLL-GD] loaded state (launch_count=%d, last=%s)" %
			[_state.launch_count, _state.last_action])


func _save_state() -> void:
	var f := FileAccess.open(STATE_FILE, FileAccess.WRITE)
	if f == null:
		printerr("[SLL-GD] failed to open state for write: %s" % FileAccess.get_open_error())
		return
	f.store_string(JSON.stringify(_state, "  "))
	prints("[SLL-GD] saved state → %s" % STATE_FILE)


func _write_env() -> void:
	var f := FileAccess.open(ENV_FILE, FileAccess.WRITE)
	if f == null:
		printerr("[SLL-GD] failed to open env file: %s" % FileAccess.get_open_error())
		return
	f.store_string("# Generated by STS2 Linux Launcher; sourced by launcher.sh on exit 42.\n")
	f.store_string("SLL_PCK_VARIANT=8x8\n")
	f.store_string("SLL_LANGUAGE=%s\n"     % _state.language)
	f.store_string("SLL_QUALITY=%s\n"      % _state.quality)
	f.store_string("SLL_SWAP_AB=%s\n"     % _state.swap_ab)
	f.store_string("SLL_SWAP_XY=%s\n"     % _state.swap_xy)
	f.store_string("SLL_LAUNCH_COUNT=%d\n" % _state.launch_count)
	prints("[SLL-GD] wrote env → %s" % ENV_FILE)


func _write_input_remap_cfg() -> void:
	var godot_dir := OS.get_executable_path().get_base_dir()
	var path := godot_dir.path_join("input_remap.cfg")
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		printerr("[SLL-GD] failed to open %s: %s" %
			[path, FileAccess.get_open_error()])
		return

	var ab: bool = _state.swap_ab == "on"
	var xy: bool = _state.swap_xy == "on"
	f.store_string("# Generated by STS2 Linux Launcher.\n")
	f.store_string("# swap_ab=%s swap_xy=%s\n\n" % [_state.swap_ab, _state.swap_xy])
	f.store_string("[buttons]\n")
	f.store_string("a = %s\n" % ("BUTTON_B" if ab else "BUTTON_A"))
	f.store_string("b = %s\n" % ("BUTTON_A" if ab else "BUTTON_B"))
	f.store_string("x = %s\n" % ("BUTTON_Y" if xy else "BUTTON_X"))
	f.store_string("y = %s\n" % ("BUTTON_X" if xy else "BUTTON_Y"))

	prints("[SLL-GD] wrote %s — swap_ab=%s swap_xy=%s (takes effect on next launcher restart)" %
		[path, _state.swap_ab, _state.swap_xy])
