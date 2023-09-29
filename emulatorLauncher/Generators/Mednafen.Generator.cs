﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using System.IO;
using emulatorLauncher.Tools;
using System.Drawing;

namespace emulatorLauncher
{
    partial class MednafenGenerator : Generator
    {
        public MednafenGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mednafen");

            string exe = Path.Combine(path, "mednafen.exe");
            if (!File.Exists(exe))
                return null;

            var cfg = MednafenConfigFile.FromFile(Path.Combine(path, "mednafen.cfg"));

            var mednafenCore = GetMednafenCoreName(core);

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            // Configure cfg file
            SetupConfig(path, cfg, mednafenCore, system);

            // Command line arguments
            List<string> commandArray = new List<string>();
            
            commandArray.Add("-fps.scale 0");
            commandArray.Add("-sound.volume 120");
            commandArray.Add("-video.fs 1");
			commandArray.Add("-video.disable_composition 1");
            commandArray.Add("-video.driver opengl");
            
            if (mednafenCore == "pce" && AppConfig.isOptSet("bios"))
                commandArray.Add("-pce.cdbios \"" + Path.Combine(AppConfig.GetFullPath("bios"), "syscard3.pce") + "\"");
         
            commandArray.Add("-" + mednafenCore + ".shader none");
            commandArray.Add("-" + mednafenCore + ".xres 0");
            commandArray.Add("-" + mednafenCore + ".yres 0");
            commandArray.Add("-" + mednafenCore + ".shader.goat.fprog 0");
            commandArray.Add("-" + mednafenCore + ".shader.goat.slen 0");
            commandArray.Add("-" + mednafenCore + ".shader.goat.tp 0.25");
            commandArray.Add("-" + mednafenCore + ".shader.goat.hdiv 1");
            commandArray.Add("-" + mednafenCore + ".shader.goat.vdiv 1");

            if (Features.IsSupported("smooth") && SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                commandArray.Add("-" + mednafenCore + ".videoip 1");
            else
                commandArray.Add("-" + mednafenCore + ".videoip 0");

            // Aspect ratio correction
            if (Features.IsSupported("mednafen_ratio_correction") && SystemConfig.isOptSet("mednafen_ratio_correction") && !SystemConfig.getOptBoolean("mednafen_ratio_correction"))
                commandArray.Add("-" + mednafenCore + ".correct_aspect 0");
            else
                commandArray.Add("-" + mednafenCore + ".correct_aspect 1");

            // Force mono
            if (Features.IsSupported("forcemono") && SystemConfig.isOptSet("forcemono") && SystemConfig.getOptBoolean("forcemono"))
                commandArray.Add("-" + mednafenCore + ".forcemono 1");
            else
                commandArray.Add("-" + mednafenCore + ".forcemono 0");

            // Shader & Bezel
            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution);
            var platform = ReshadeManager.GetPlatformFromFile(exe);

            if (ReshadeManager.Setup(ReshadeBezelType.opengl, platform, system, rom, path, resolution, bezels != null) && bezels != null)
                commandArray.Add("-" + mednafenCore + ".stretch full");
            else if (SystemConfig.isOptSet("mednafen_ratio") && !string.IsNullOrEmpty(SystemConfig["mednafen_ratio"]))
                commandArray.Add("-" + mednafenCore + ".stretch " + SystemConfig["mednafen_ratio"]);
            else
                commandArray.Add("-" + mednafenCore + ".stretch aspect");

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }

        private void SetPath(MednafenConfigFile cfg, string settingName, string pathName)
        {
            if (!Directory.Exists(pathName)) try { Directory.CreateDirectory(pathName); }
                catch { }
            if (!string.IsNullOrEmpty(pathName))
                cfg[settingName] = pathName;
        }

        private void BindMednafenFeature(MednafenConfigFile cfg, string featureName, string settingName, string defaultValue)
        {
            if (SystemConfig.isOptSet(featureName) && !string.IsNullOrEmpty(SystemConfig[featureName]))
                cfg[settingName] = SystemConfig[featureName];
            else
                cfg[settingName] = defaultValue;
        }

        private void BindMednafenBoolFeature(MednafenConfigFile cfg, string featureName, string settingName, string trueValue, string falseValue)
        {
            if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                cfg[settingName] = trueValue;
            else
                cfg[settingName] = falseValue;
        }

        private void SetupConfig(string path, MednafenConfigFile cfg, string mednafenCore, string system)
        {          
            // Path
            var biosPath = AppConfig.GetFullPath("bios");
            SetPath(cfg, "filesys.path_firmware", biosPath);

            var cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "mednafen");
            SetPath(cfg, "filesys.path_cheat", cheatsPath);

            var savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "mednafen");
            SetPath(cfg, "filesys.path_sav", savesPath);

            var backupPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "mednafen", "backup");
            SetPath(cfg, "filesys.path_savbackup", backupPath);

            var saveStatePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "mednafen", "sstates");
            SetPath(cfg, "filesys.path_state", saveStatePath);

            var screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "mednafen");
            SetPath(cfg, "filesys.path_snap", screenshotsPath);

            // General Settings
            cfg[mednafenCore + ".enable"] = "1";
            BindMednafenFeature(cfg, "mednafen_scaler", mednafenCore + ".special", "none");
            BindMednafenFeature(cfg, "mednafen_apu", "sound.driver", "default");
            BindMednafenFeature(cfg, "mednafen_interlace", "video.deinterlacer", "weave");
            BindMednafenFeature(cfg, "MonitorIndex", "video.fs.display", "-1");
            BindMednafenBoolFeature(cfg, "mednafen_vsync", "video.glvsync", "0", "1");
            BindMednafenBoolFeature(cfg, "autosave", "autosave", "1", "0");
            BindMednafenBoolFeature(cfg, "mednafen_cheats", "cheats", "1", "0");
            BindMednafenBoolFeature(cfg, "mednafen_fps_enable", "fps.autoenable", "1", "0");

            // Core Specific settings
            ConfigureMednafenApple2(cfg, mednafenCore, system);
            ConfigureMednafenLynx(cfg, mednafenCore, system);
            ConfigureMednafenMasterSystem(cfg, mednafenCore, system);
            ConfigureMednafenMegadrive(cfg, mednafenCore, system);
            ConfigureMednafenNES(cfg, mednafenCore, system);
            ConfigureMednafenPCE(cfg, mednafenCore, system);
            ConfigureMednafenPSX(cfg, mednafenCore, system);
            ConfigureMednafenSaturn(cfg, mednafenCore, system);
            ConfigureMednafenSnes(cfg, mednafenCore, system);
            ConfigureMednafenWswan(cfg, mednafenCore, system);

            // controllers
            CreateControllerConfiguration(cfg, mednafenCore);

            cfg.Save();
        }

        private void ConfigureMednafenApple2(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "apple2")
                return;

            cfg["apple2.input.port1.gamepad.resistance_select.defpos"] = "2";
            cfg["apple2.input.port1.joystick.resistance_select.defpos"] = "2";
            cfg["apple2.input.port1.joystick.axis_scale"] = "1.00";
        }

        private void ConfigureMednafenLynx(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "lynx")
                    return;

            BindMednafenBoolFeature(cfg, "mednafen_lynx_lowpass", mednafenCore + ".lowpass", "1", "0");
            BindMednafenBoolFeature(cfg, "mednafen_lynx_rotate", mednafenCore + ".rotateinput", "1", "0");
        }

        private void ConfigureMednafenMasterSystem(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "sms")
                return;

            BindMednafenBoolFeature(cfg, "mednafen_sms_fm", mednafenCore + ".fm", "1", "0");
        }

        private void ConfigureMednafenMegadrive(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "md")
                return;

            BindMednafenFeature(cfg, "mednafen_md_region", mednafenCore + ".region", "game");

            cfg["md.reported_region"] = "same";

            if (SystemConfig.isOptSet("mednafen_md_multitap") && SystemConfig["mednafen_md_multitap"] == "both")
                cfg["md.input.multitap"] = "tpd";
            else if (SystemConfig.isOptSet("mednafen_md_multitap") && SystemConfig["mednafen_md_multitap"] == "1")
                cfg["md.input.multitap"] = "tp1";
            else if (SystemConfig.isOptSet("mednafen_md_multitap") && SystemConfig["mednafen_md_multitap"] == "2")
                cfg["md.input.multitap"] = "tp2";
            else
                cfg["md.input.multitap"] = "none";
        }

        private void ConfigureMednafenNES(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "nes")
                return;

            BindMednafenFeature(cfg, "mednafen_nes_soundq", mednafenCore + ".soundq", "0");
            BindMednafenFeature(cfg, "mednafen_nes_videopreset", mednafenCore + ".ntsc.preset", "none");
            BindMednafenBoolFeature(cfg, "mednafen_nes_106bs", mednafenCore + ".n106bs", "1", "0");
            BindMednafenBoolFeature(cfg, "mednafen_nes_pal50", mednafenCore + ".pal", "1", "0");
            BindMednafenBoolFeature(cfg, "mednafen_nes_multitap", mednafenCore + ".input.fcexp", "4player", "none");
        }

        private void ConfigureMednafenPCE(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "pce")
                return;

            BindMednafenBoolFeature(cfg, "mednafen_pce_multitap", mednafenCore + ".input.multitap", "1", "0");
        }

        private void ConfigureMednafenPSX(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "psx")
                return;

            cfg["psx.input.analog_mode_ct"] = "1";

            if (SystemConfig.isOptSet("mednafen_psx_region") && !string.IsNullOrEmpty(SystemConfig["mednafen_psx_region"]))
            {
                cfg["psx.region_autodetect"] = "0";
                cfg["psx.region_default"] = SystemConfig["mednafen_psx_region"];
            }
            else
            {
                cfg["psx.region_autodetect"] = "1";
                cfg["psx.region_default"] = "eu";
            }

            if (SystemConfig.isOptSet("mednafen_psx_multitap") && SystemConfig["mednafen_psx_multitap"] == "1")
            {
                cfg["psx.input.pport1.multitap"] = "1";
                cfg["psx.input.pport2.multitap"] = "0";
            }
            else if (SystemConfig.isOptSet("mednafen_psx_multitap") && SystemConfig["mednafen_psx_multitap"] == "2")
            {
                cfg["psx.input.pport1.multitap"] = "0";
                cfg["psx.input.pport2.multitap"] = "1";
            }
            else if (SystemConfig.isOptSet("mednafen_psx_multitap") && SystemConfig["mednafen_psx_multitap"] == "both")
            {
                cfg["psx.input.pport1.multitap"] = "1";
                cfg["psx.input.pport2.multitap"] = "1";
            }
            else
            {
                cfg["psx.input.pport1.multitap"] = "0";
                cfg["psx.input.pport2.multitap"] = "0";
            }

            BindMednafenFeature(cfg, "mednafen_psx_analogcombo", mednafenCore + ".input.analog_mode_ct.compare", "0x0C01");

            // Memory cards (up to 4)
            // Cleanup first
            for (int i = 1; i < 9; i++)
                cfg["psx.input.port" + i + ".memcard"] = "0";

            if (SystemConfig.isOptSet("mednafen_psx_memcards") && SystemConfig["mednafen_psx_memcards"] != "0")
            {
                int memcnb = SystemConfig["mednafen_psx_memcards"].ToInteger();
                for (int i = 1; i <= memcnb; i++)
                    cfg["psx.input.port" + i + ".memcard"] = "1";
            }

            for (int i = 1; i < 9; i++)
                cfg["psx.input.port" + i + ".dualshock.axis_scale"] = "1.00";
        }

        private void ConfigureMednafenSaturn(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "ss")
                return;

            BindMednafenFeature(cfg, "mednafen_saturn_ramcart", mednafenCore + ".cart", "auto"); // Expansion cartridge
            cfg[mednafenCore + ".smpc.autortc"] = "1"; // Automatically set clock
            BindMednafenFeature(cfg, "mednafen_saturn_language", mednafenCore + ".smpc.autortc.lang", "english"); // BIOS language

            if (SystemConfig.isOptSet("mednafen_saturn_region") && !string.IsNullOrEmpty(SystemConfig["mednafen_saturn_region"]))
            {
                cfg["ss.region_autodetect"] = "0";
                cfg["ss.region_default"] = SystemConfig["mednafen_saturn_region"];
            }
            else
                cfg["ss.region_autodetect"] = "1";

            if (SystemConfig.isOptSet("mednafen_ss_multitap") && SystemConfig["mednafen_ss_multitap"] == "both")
            {
                cfg["ss.input.sport1.multitap"] = "1";
                cfg["ss.input.sport2.multitap"] = "1";
            }
            else if (SystemConfig.isOptSet("mednafen_ss_multitap") && SystemConfig["mednafen_ss_multitap"] == "1")
            {
                cfg["ss.input.sport1.multitap"] = "1";
                cfg["ss.input.sport2.multitap"] = "0";
            }
            else if (SystemConfig.isOptSet("mednafen_ss_multitap") && SystemConfig["mednafen_ss_multitap"] == "2")
            {
                cfg["ss.input.sport1.multitap"] = "0";
                cfg["ss.input.sport2.multitap"] = "1";
            }
            else
            {
                cfg["ss.input.sport1.multitap"] = "0";
                cfg["ss.input.sport2.multitap"] = "0";
            }
        }

        private void ConfigureMednafenSnes(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "snes")
                return;

            BindMednafenBoolFeature(cfg, "mednafen_snes_h_blend", mednafenCore + ".h_blend", "0", "1"); // H-Blend
            BindMednafenFeature(cfg, "mednafen_snes_resamp_quality", mednafenCore + ".apu.resamp_quality", "5"); // Sound accuracy

            // Multitap
            if (SystemConfig.isOptSet("mednafen_snes_multitap") && SystemConfig["mednafen_snes_multitap"] == "both")
            {
                cfg["snes.input.port1.multitap"] = "1";
                cfg["snes.input.port2.multitap"] = "1";
            }
            else if (SystemConfig.isOptSet("mednafen_snes_multitap") && SystemConfig["mednafen_snes_multitap"] == "1")
            {
                cfg["snes.input.port1.multitap"] = "1";
                cfg["snes.input.port2.multitap"] = "0";
            }
            else if (SystemConfig.isOptSet("mednafen_snes_multitap") && SystemConfig["mednafen_snes_multitap"] == "2")
            {
                cfg["snes.input.port1.multitap"] = "0";
                cfg["snes.input.port2.multitap"] = "1";
            }
            else
            {
                cfg["snes.input.port1.multitap"] = "0";
                cfg["snes.input.port2.multitap"] = "0";
            }
        }

        private void ConfigureMednafenWswan(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (mednafenCore != "wswan")
                return;

            BindMednafenFeature(cfg, "mednafen_wswan_lang", mednafenCore + ".language", "english");
        }

        private string GetMednafenCoreName(string core)
        {
            switch (core)
            {
                case "mastersystem":
                    return "sms";
                case "megadrive":
                    return "md";
                case "pcengine":
                case "pcenginecd":
                case "supergrafx":
                    return "pce";
                case "saturn":
                    return "ss";
            }

            return core;
        }
    }
    
    class MednafenConfigFile
    {
        private string _fileName;
        private List<string> _lines;

        public static MednafenConfigFile FromFile(string file)
        {
            var ret = new MednafenConfigFile();
            ret._fileName = file;

            try
            {
                if (File.Exists(file))
                    ret._lines = File.ReadAllLines(file).ToList();
            }
            catch { }

            if (ret._lines == null)
                ret._lines = new List<string>();

            return ret;
        }

        public string this[string key]
        {
            get
            {
                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                if (idx >= 0)
                {
                    int split = _lines[idx].IndexOf(" ");
                    if (split >= 0)
                        return _lines[idx].Substring(split + 1).Trim();
                }

                return string.Empty;
            }
            set
            {
                if (this[key] == value)
                    return;

                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                if (idx >= 0)
                {
                    _lines.RemoveAt(idx);

                    if (!string.IsNullOrEmpty(value))
                        _lines.Insert(idx, key + " " + value);
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _lines.Add(key + " " + value);
                    _lines.Add("");
                }

                IsDirty = true;
            }
        }

        public bool IsDirty { get; private set; }

        public void Save()
        {
            if (!IsDirty)
                return;

            File.WriteAllLines(_fileName, _lines);
            IsDirty = false;
        }
    }
}