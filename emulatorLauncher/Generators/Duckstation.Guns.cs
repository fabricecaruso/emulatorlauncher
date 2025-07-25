﻿using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using System.Configuration;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class DuckstationGenerator : Generator
    {
        private bool _sindenSoft = false;

        private void CreateGunConfiguration(IniFile ini)
        {
            bool gun = SystemConfig.getOptBoolean("use_guns") || SystemConfig["duck_controller1"] == "GunCon";
            
            if (!gun)
                return;

            Controller ctrl = null;

            if (Program.Controllers.Count >= 1)
                ctrl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            else
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            // Start Sinden Soft if required
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                _forceSDL = true;

            string techPadNumber = null;
            string tech = "";
            bool gamepad = false;
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");

            if (!ctrl.IsKeyboard && ctrl.IsXInputDevice && !_forceSDL)
            {
                techPadNumber = "XInput-" + ctrl.XInput.DeviceIndex + "/";
                tech = "XInput";
                gamepad = true;
            }
            else if (!ctrl.IsKeyboard)
            {
                techPadNumber = "SDL-" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "/";
                tech = "SDL";
                gamepad = true;
            }
            else
                techPadNumber = "Keyboard/";

            string padNumber = "Pad1";

            if (SystemConfig.isOptSet("duck_gunp2") && SystemConfig.getOptBoolean("duck_gunp2"))
                padNumber = "Pad2";

            ini.WriteValue("ControllerPorts", "MultitapMode", "Disabled");

            // Guncon configuration
            // Only one mouse is supported so far in duckstation, for player 1
            ini.WriteValue(padNumber, "Type", "GunCon");
            ini.WriteValue(padNumber, "Pointer", "Pointer-0");
            ini.WriteValue(padNumber, "Trigger", guninvert ? "Pointer-0/RightButton" : "Pointer-0/LeftButton");

            // Define mapping for A and B buttons (default is mouse right click and middle click)
            if (SystemConfig.isOptSet("duck_gun_ab") && !string.IsNullOrEmpty(SystemConfig["duck_gun_ab"]) && SystemConfig["duck_gun_ab"] == "controller_1")
            {
                if (gamepad)
                {
                    ini.WriteValue(padNumber, "A", techPadNumber + GetInputKeyName(ctrl, InputKey.a, tech));
                    ini.WriteValue(padNumber, "B", techPadNumber + GetInputKeyName(ctrl, InputKey.b, tech));
                    ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
                }
                else
                {
                    ini.WriteValue(padNumber, "A", "Keyboard/PageUp");
                    ini.WriteValue(padNumber, "B", "Keyboard/PageDown");
                    ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
                }
            }
            else if (SystemConfig["duck_gun_ab"] == "key_1")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/PageUp");
                ini.WriteValue(padNumber, "B", "Keyboard/PageDown");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_2")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/K");
                ini.WriteValue(padNumber, "B", "Keyboard/L");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_3")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/Left");
                ini.WriteValue(padNumber, "B", "Keyboard/Right");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_4")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/Left");
                ini.WriteValue(padNumber, "B", "Keyboard/Return");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_5")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/VolumeUp");
                ini.WriteValue(padNumber, "B", "Keyboard/VolumeDown");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_6")
            {
                ini.WriteValue(padNumber, "A", "Keyboard/1");
                ini.WriteValue(padNumber, "B", "Keyboard/5");
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
            }
            else if(SystemConfig.isOptSet("gun_reload_button") && SystemConfig.getOptBoolean("gun_reload_button"))
            {
                ini.WriteValue(padNumber, "ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
                ini.WriteValue(padNumber, "A", "Pointer-0/MiddleButton");
                ini.WriteValue(padNumber, "B", "");
            }
            else
            {
                ini.WriteValue(padNumber, "A", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
                ini.WriteValue(padNumber, "B", "Pointer-0/MiddleButton");
            }

            string crosshairSize = "0.500000";
            if (SystemConfig.isOptSet("duck_crosshair") && !string.IsNullOrEmpty(SystemConfig["duck_crosshair"]))
                crosshairSize = SystemConfig["duck_crosshair"].Substring(0, SystemConfig["duck_crosshair"].Length - 4);
            ini.WriteValue(padNumber, "CrosshairScale", crosshairSize);
            
            ini.WriteValue(padNumber, "XScale", "0.930000");    // Adjust Xscale for mouse calibration

            // Crosshair
            ini.Remove(padNumber, "CrosshairImagePath");
            string crosshairPath = Path.Combine(Path.Combine(AppConfig.GetFullPath("duckstation"), "cross"));
            if (!Directory.Exists(crosshairPath)) try { Directory.CreateDirectory(crosshairPath); }
                catch { }
            string crosshairFile = Path.Combine(crosshairPath, "crosshair.png");

            if (!File.Exists(crosshairFile))
                SimpleLogger.Instance.Info("[GUNS] No crosshair file found in " + crosshairFile);

            if (SystemConfig.isOptSet("duck_custom_crosshair") && SystemConfig.getOptBoolean("duck_custom_crosshair") && File.Exists(crosshairFile))
            {
                ini.WriteValue(padNumber, "CrosshairImagePath", crosshairFile);
                ini.WriteValue("Main", "HideCursorInFullscreen", "true");
            }
        }
    }
}