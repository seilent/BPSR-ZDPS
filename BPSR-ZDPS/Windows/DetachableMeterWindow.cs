using BPSR_ZDPS.Meters;
using BPSR_ZDPS.Managers;
using BPSR_ZDPS.DataTypes;
using Hexa.NET.ImGui;
using System.Numerics;

namespace BPSR_ZDPS.Windows
{
    public static class DetachableMeterWindow
    {
        public const string LAYER = "DetachableMeterWindowLayer";
        public const string TITLE_ID = "###DetachableMeterWindow";
        public static bool IsOpened = false;
        public static bool IsTopMost = true; // Always on top

        private static MeterBase _detachedMeter = null;
        private static int RunOnceDelayed = 0;
        private static Vector2 DefaultWindowSize = new Vector2(400, 500);

        // Track if window was manually toggled (vs auto-hidden)
        private static bool _manuallyToggled = false;

        // Track if user is interacting with window (dragging/resizing)
        private static bool _isInteracting = false;

        // Track last applied opacity to avoid redundant GLFW calls
        private static int LastPinnedOpacity = -1;

        public static void ToggleDetachMeter(MeterBase meter, MainWindow mainWindow)
        {
            if (_detachedMeter == meter && IsOpened)
            {
                // Save current position/size before closing
                SaveCurrentWindowSettings();
                IsOpened = false;
                _manuallyToggled = false;
                _detachedMeter = null;
            }
            else
            {
                // Save previous meter's settings if switching
                if (_detachedMeter != null && IsOpened)
                {
                    SaveCurrentWindowSettings();
                }
                // Detach new meter
                _detachedMeter = meter;
                IsOpened = true;
                _manuallyToggled = true;
                RunOnceDelayed = 0;
            }
        }

        private static void SaveCurrentWindowSettings()
        {
            if (_detachedMeter == null) return;
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            Settings.Instance.WindowSettings.DetachableMeter.MeterWindowSettings[_detachedMeter.Name] = (pos, size);
            Settings.Save();
        }

        private static bool TryGetSavedSettings(string meterName, out Vector2 pos, out Vector2 size)
        {
            if (Settings.Instance.WindowSettings.DetachableMeter.MeterWindowSettings.TryGetValue(meterName, out var settings))
            {
                pos = settings.pos;
                size = settings.size;
                return true;
            }
            pos = Vector2.Zero;
            size = Vector2.Zero;
            return false;
        }

        private static bool HasActiveEncounter()
        {
            return EncounterManager.Current != null && EncounterManager.Current.HasStatsBeenRecorded();
        }

        private static void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                // Show encounter status as draggable title bar
                var current = EncounterManager.Current;
                if (current != null)
                {
                    // Status label
                    ImGui.Text("Status:");

                    ImGui.SameLine();
                    // Player placement in current meter
                    ImGui.Text($"[{AppState.PlayerMeterPlacement}]");

                    ImGui.SameLine();
                    // Duration
                    string duration = "00:00:00";
                    if (current.GetDuration().TotalSeconds > 0)
                    {
                        duration = current.GetDuration().ToString("hh\\:mm\\:ss");
                    }
                    if (AppState.IsBenchmarkMode && !AppState.HasBenchmarkBegun)
                    {
                        duration = "00:00:00";
                    }
                    ImGui.Text(duration);

                    // Scene/Zone name
                    if (!string.IsNullOrEmpty(current.SceneName))
                    {
                        ImGui.SameLine();
                        string subName = "";
                        if (!string.IsNullOrEmpty(current.SceneSubName))
                        {
                            subName = $" ({current.SceneSubName})";
                        }
                        ImGui.TextUnformatted($"- {current.SceneName}{subName}");
                    }

                    // Benchmark mode
                    if (AppState.IsBenchmarkMode)
                    {
                        ImGui.SameLine();
                        ImGui.TextUnformatted($"[BENCHMARK ({AppState.BenchmarkTime}s)]");
                    }

                    // Right-aligned damage/DPS values
                    ImGui.SameLine();
                    string currentValuePerSecond = $"{Utils.NumberToShorthand(AppState.PlayerTotalMeterValue)} ({Utils.NumberToShorthand(AppState.PlayerMeterValuePerSecond)})";
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + float.Max(0.0f, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(currentValuePerSecond).X));
                    ImGui.Text(currentValuePerSecond);
                }
                else
                {
                    // No active encounter
                    ImGui.Text("Status:");
                    ImGui.SameLine();
                    ImGui.Text("(No encounter)");
                }

                ImGui.EndMenuBar();
            }
        }

        public static void Draw(MainWindow mainWindow)
        {
            // Auto-hide when no encounter/activity, but only if manually toggled first
            if (_manuallyToggled && !HasActiveEncounter())
            {
                if (IsOpened)
                {
                    SaveCurrentWindowSettings();
                }
                IsOpened = false;
                return;
            }

            // Auto-show when encounter becomes active again
            if (_manuallyToggled && HasActiveEncounter() && !IsOpened && _detachedMeter != null)
            {
                IsOpened = true;
                RunOnceDelayed = 0;
            }

            if (!IsOpened || _detachedMeter == null) return;

            // Don't restore position/size if user is currently interacting (prevents jumping)
            bool hasInteracting = ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0) || _isInteracting;
            if (!hasInteracting)
            {
                // Restore saved position/size for this meter
                if (TryGetSavedSettings(_detachedMeter.Name, out var savedPos, out var savedSize))
                {
                    if (savedPos != Vector2.Zero)
                        ImGui.SetNextWindowPos(savedPos, ImGuiCond.FirstUseEver);
                    if (savedSize != Vector2.Zero)
                        ImGui.SetNextWindowSize(savedSize, ImGuiCond.FirstUseEver);
                }
                else
                {
                    ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
                }
            }

            // Window flags - no title bar, no scrollbar, always on top, with menu bar for dragging
            ImGuiWindowFlags exWindowFlags = ImGuiWindowFlags.None;
            if (AppState.MousePassthrough)
            {
                exWindowFlags |= ImGuiWindowFlags.NoInputs;
            }

            ImGuiWindowFlags windowFlags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.MenuBar;  // Menu bar allows dragging

            if (ImGui.Begin($"Detached Meter{TITLE_ID}", ref IsOpened, windowFlags))
            {
                // Draw menu bar (for dragging)
                DrawMenuBar();

                // Run-once initialization for topmost
                if (RunOnceDelayed == 0)
                {
                    RunOnceDelayed++;
                }
                else if (RunOnceDelayed == 1)
                {
                    RunOnceDelayed++;
                    Utils.SetWindowTopmost();
                    Utils.SetWindowOpacity(Settings.Instance.WindowSettings.DetachableMeter.Opacity * 0.01f);
                    LastPinnedOpacity = Settings.Instance.WindowSettings.DetachableMeter.Opacity;
                }
                else
                {
                    // Apply opacity setting (after window is initialized)
                    if (LastPinnedOpacity != Settings.Instance.WindowSettings.DetachableMeter.Opacity)
                    {
                        Utils.SetWindowOpacity(Settings.Instance.WindowSettings.DetachableMeter.Opacity * 0.01f);
                        LastPinnedOpacity = Settings.Instance.WindowSettings.DetachableMeter.Opacity;
                    }
                }

                // Also hide child window scrollbars
                ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 0f);

                // Draw the meter content directly
                _detachedMeter.Draw(mainWindow);

                ImGui.PopStyleVar(); // ScrollbarSize

                // Save settings when interacting with window
                if (ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0))
                {
                    _isInteracting = true;
                    SaveCurrentWindowSettings();
                }
                else if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0))
                {
                    _isInteracting = false;
                }
            }

            ImGui.End();

            if (!IsOpened && !_manuallyToggled)
            {
                _detachedMeter = null;
            }
        }
    }
}
