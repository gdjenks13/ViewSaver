using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ViewSaver.Models
{
    public class DisplayProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Profile";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        public HotkeyBinding? Hotkey { get; set; }
        public List<MonitorConfiguration> Monitors { get; set; } = new();
        public DisplayConfigurationData? RawConfiguration { get; set; }
    }

    public class MonitorConfiguration
    {
        public string DeviceName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string DevicePath { get; set; } = string.Empty;
        public string AdapterPath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsPrimary { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public int BitsPerPixel { get; set; }
        public int Orientation { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int ScalingPercent { get; set; } = 100;
        public uint SourceId { get; set; }
        public uint TargetId { get; set; }
        public int DisplayOrder { get; set; }

        public override string ToString()
        {
            string status = IsEnabled ? (IsPrimary ? "Primary" : "Extended") : "Disabled";
            return $"{FriendlyName} ({DeviceName}) - {Width}x{Height} @ {RefreshRate}Hz - {status}";
        }
    }

    public class DisplayConfigurationData
    {
        public List<PathInfoData> Paths { get; set; } = new();
        public List<ModeInfoData> Modes { get; set; } = new();
    }

    public class PathInfoData
    {
        public long SourceAdapterId { get; set; }
        public uint SourceId { get; set; }
        public uint SourceModeInfoIdx { get; set; }
        public uint SourceStatusFlags { get; set; }

        public long TargetAdapterId { get; set; }
        public uint TargetId { get; set; }
        public uint TargetModeInfoIdx { get; set; }
        public uint OutputTechnology { get; set; }
        public uint Rotation { get; set; }
        public uint Scaling { get; set; }
        public uint RefreshRateNumerator { get; set; }
        public uint RefreshRateDenominator { get; set; }
        public uint ScanLineOrdering { get; set; }
        public bool TargetAvailable { get; set; }
        public uint TargetStatusFlags { get; set; }

        public uint Flags { get; set; }

        // Stable identifiers used to re-map adapter LUIDs / target IDs, which change
        // across reboots, driver updates and when other GPUs (e.g. an iGPU) appear.
        public string? SourceAdapterPath { get; set; }
        public string? TargetAdapterPath { get; set; }
        public string? TargetDevicePath { get; set; }
    }

    public class ModeInfoData
    {
        public uint InfoType { get; set; }
        public uint Id { get; set; }
        public long AdapterId { get; set; }

        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint PixelFormat { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        public ulong PixelRate { get; set; }
        public uint HSyncNumerator { get; set; }
        public uint HSyncDenominator { get; set; }
        public uint VSyncNumerator { get; set; }
        public uint VSyncDenominator { get; set; }
        public uint ActiveSizeWidth { get; set; }
        public uint ActiveSizeHeight { get; set; }
        public uint TotalSizeWidth { get; set; }
        public uint TotalSizeHeight { get; set; }
        public uint VideoStandard { get; set; }
        public uint TargetScanLineOrdering { get; set; }
    }

    public class HotkeyBinding
    {
        public Keys Key { get; set; } = Keys.None;
        public bool Control { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }

        public bool IsValid => Key != Keys.None && (Control || Alt || Shift || Win);

        public uint GetModifiers()
        {
            uint modifiers = 0;
            if (Control) modifiers |= Native.NativeMethods.MOD_CONTROL;
            if (Alt) modifiers |= Native.NativeMethods.MOD_ALT;
            if (Shift) modifiers |= Native.NativeMethods.MOD_SHIFT;
            if (Win) modifiers |= Native.NativeMethods.MOD_WIN;
            return modifiers | Native.NativeMethods.MOD_NOREPEAT;
        }

        public override string ToString()
        {
            if (!IsValid) return "None";

            var parts = new List<string>();
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }

        public override bool Equals(object? obj)
        {
            if (obj is HotkeyBinding other)
            {
                return Key == other.Key &&
                       Control == other.Control &&
                       Alt == other.Alt &&
                       Shift == other.Shift &&
                       Win == other.Win;
            }
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(Key, Control, Alt, Shift, Win);
    }

    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public bool MinimizeOnClose { get; set; } = true;
        public string LastSelectedProfileId { get; set; } = string.Empty;
    }
}
