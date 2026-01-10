using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ViewSaver.Models;
using ViewSaver.Native;
using static ViewSaver.Native.NativeMethods;

namespace ViewSaver.Services
{
    public class DisplayService
    {
        public DisplayProfile CaptureCurrentConfiguration(string profileName = "Current")
        {
            var profile = new DisplayProfile
            {
                Name = profileName,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };

            int result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (result != ERROR_SUCCESS)
            {
                throw new Exception($"GetDisplayConfigBufferSizes failed with error {result}");
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result != ERROR_SUCCESS)
            {
                throw new Exception($"QueryDisplayConfig failed with error {result}");
            }

            profile.RawConfiguration = new DisplayConfigurationData();

            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var pathData = new PathInfoData
                {
                    SourceAdapterId = ((long)path.sourceInfo.adapterId.HighPart << 32) | path.sourceInfo.adapterId.LowPart,
                    SourceId = path.sourceInfo.id,
                    SourceModeInfoIdx = path.sourceInfo.modeInfoIdx,
                    SourceStatusFlags = path.sourceInfo.statusFlags,
                    TargetAdapterId = ((long)path.targetInfo.adapterId.HighPart << 32) | path.targetInfo.adapterId.LowPart,
                    TargetId = path.targetInfo.id,
                    TargetModeInfoIdx = path.targetInfo.modeInfoIdx,
                    OutputTechnology = (uint)path.targetInfo.outputTechnology,
                    Rotation = (uint)path.targetInfo.rotation,
                    Scaling = (uint)path.targetInfo.scaling,
                    RefreshRateNumerator = path.targetInfo.refreshRate.Numerator,
                    RefreshRateDenominator = path.targetInfo.refreshRate.Denominator,
                    ScanLineOrdering = (uint)path.targetInfo.scanLineOrdering,
                    TargetAvailable = path.targetInfo.targetAvailable,
                    TargetStatusFlags = path.targetInfo.statusFlags,
                    Flags = path.flags
                };
                profile.RawConfiguration.Paths.Add(pathData);
            }

            for (int i = 0; i < modeCount; i++)
            {
                var mode = modes[i];
                var modeData = new ModeInfoData
                {
                    InfoType = (uint)mode.infoType,
                    Id = mode.id,
                    AdapterId = ((long)mode.adapterId.HighPart << 32) | mode.adapterId.LowPart
                };

                if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    modeData.Width = mode.modeInfo.sourceMode.width;
                    modeData.Height = mode.modeInfo.sourceMode.height;
                    modeData.PixelFormat = (uint)mode.modeInfo.sourceMode.pixelFormat;
                    modeData.PositionX = mode.modeInfo.sourceMode.position.x;
                    modeData.PositionY = mode.modeInfo.sourceMode.position.y;
                }
                else if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    modeData.PixelRate = mode.modeInfo.targetMode.targetVideoSignalInfo.pixelRate;
                    modeData.HSyncNumerator = mode.modeInfo.targetMode.targetVideoSignalInfo.hSyncFreq.Numerator;
                    modeData.HSyncDenominator = mode.modeInfo.targetMode.targetVideoSignalInfo.hSyncFreq.Denominator;
                    modeData.VSyncNumerator = mode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator;
                    modeData.VSyncDenominator = mode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator;
                    modeData.ActiveSizeWidth = mode.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx;
                    modeData.ActiveSizeHeight = mode.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy;
                    modeData.TotalSizeWidth = mode.modeInfo.targetMode.targetVideoSignalInfo.totalSize.cx;
                    modeData.TotalSizeHeight = mode.modeInfo.targetMode.targetVideoSignalInfo.totalSize.cy;
                    modeData.VideoStandard = mode.modeInfo.targetMode.targetVideoSignalInfo.videoStandard;
                    modeData.TargetScanLineOrdering = (uint)mode.modeInfo.targetMode.targetVideoSignalInfo.scanLineOrdering;
                }

                profile.RawConfiguration.Modes.Add(modeData);
            }

            var monitorConfigs = GetMonitorConfigurations(paths, modes, (int)pathCount, (int)modeCount);
            profile.Monitors.AddRange(monitorConfigs);

            return profile;
        }

        private List<MonitorConfiguration> GetMonitorConfigurations(
            DISPLAYCONFIG_PATH_INFO[] paths,
            DISPLAYCONFIG_MODE_INFO[] modes,
            int pathCount,
            int modeCount)
        {
            var monitors = new List<MonitorConfiguration>();

            var displayDevices = GetDisplayDevices();

            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var config = new MonitorConfiguration
                {
                    SourceId = path.sourceInfo.id,
                    TargetId = path.targetInfo.id,
                    IsEnabled = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0,
                    DisplayOrder = i
                };

                var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                targetName.header.adapterId = path.targetInfo.adapterId;
                targetName.header.id = path.targetInfo.id;

                if (DisplayConfigGetDeviceInfo(ref targetName) == ERROR_SUCCESS)
                {
                    config.FriendlyName = string.IsNullOrEmpty(targetName.monitorFriendlyDeviceName)
                        ? $"Display {i + 1}"
                        : targetName.monitorFriendlyDeviceName;
                    config.DevicePath = targetName.monitorDevicePath ?? string.Empty;
                }
                else
                {
                    config.FriendlyName = $"Display {i + 1}";
                }

                var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                sourceName.header.adapterId = path.sourceInfo.adapterId;
                sourceName.header.id = path.sourceInfo.id;

                if (DisplayConfigGetDeviceInfo(ref sourceName) == ERROR_SUCCESS)
                {
                    config.DeviceName = sourceName.viewGdiDeviceName ?? $"\\\\.\\DISPLAY{i + 1}";
                }

                var adapterName = new DISPLAYCONFIG_ADAPTER_NAME();
                adapterName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
                adapterName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_ADAPTER_NAME>();
                adapterName.header.adapterId = path.sourceInfo.adapterId;

                if (DisplayConfigGetDeviceInfo(ref adapterName) == ERROR_SUCCESS)
                {
                    config.AdapterPath = adapterName.adapterDevicePath ?? string.Empty;
                }

                if (path.sourceInfo.modeInfoIdx < modeCount)
                {
                    var sourceMode = modes[path.sourceInfo.modeInfoIdx];
                    if (sourceMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                    {
                        config.Width = (int)sourceMode.modeInfo.sourceMode.width;
                        config.Height = (int)sourceMode.modeInfo.sourceMode.height;
                        config.PositionX = sourceMode.modeInfo.sourceMode.position.x;
                        config.PositionY = sourceMode.modeInfo.sourceMode.position.y;
                    }
                }

                if (path.targetInfo.modeInfoIdx < modeCount)
                {
                    var targetMode = modes[path.targetInfo.modeInfoIdx];
                    if (targetMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                    {
                        var vSync = targetMode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq;
                        if (vSync.Denominator > 0)
                        {
                            config.RefreshRate = (int)Math.Round((double)vSync.Numerator / vSync.Denominator);
                        }
                    }
                }
                else
                {
                    if (path.targetInfo.refreshRate.Denominator > 0)
                    {
                        config.RefreshRate = (int)Math.Round((double)path.targetInfo.refreshRate.Numerator / path.targetInfo.refreshRate.Denominator);
                    }
                }

                config.Orientation = (int)path.targetInfo.rotation - 1; // IDENTITY is 1, we want 0-based

                var device = displayDevices.FirstOrDefault(d => d.DeviceName == config.DeviceName);
                if (device.DeviceName != null)
                {
                    config.IsPrimary = (device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                    config.IsEnabled = (device.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                }

                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                if (EnumDisplaySettings(config.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    config.BitsPerPixel = devMode.dmBitsPerPel;
                    if (config.RefreshRate == 0)
                    {
                        config.RefreshRate = devMode.dmDisplayFrequency;
                    }
                }

                config.ScalingPercent = 100;

                monitors.Add(config);
            }

            return monitors;
        }

        private List<DISPLAY_DEVICE> GetDisplayDevices()
        {
            var devices = new List<DISPLAY_DEVICE>();
            uint deviceIndex = 0;

            while (true)
            {
                var device = new DISPLAY_DEVICE();
                device.cb = Marshal.SizeOf<DISPLAY_DEVICE>();

                if (!EnumDisplayDevices(null, deviceIndex, ref device, 0))
                    break;

                if ((device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    devices.Add(device);
                }

                deviceIndex++;
            }

            return devices;
        }

        public bool ApplyProfile(DisplayProfile profile)
        {
            if (profile.RawConfiguration == null || 
                profile.RawConfiguration.Paths.Count == 0)
            {
                return false;
            }

            try
            {
                int result = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out uint currentPathCount, out uint currentModeCount);
                if (result != ERROR_SUCCESS) return false;

                var currentPaths = new DISPLAYCONFIG_PATH_INFO[currentPathCount];
                var currentModes = new DISPLAYCONFIG_MODE_INFO[currentModeCount];

                result = QueryDisplayConfig(QDC_ALL_PATHS, ref currentPathCount, currentPaths, ref currentModeCount, currentModes, IntPtr.Zero);
                if (result != ERROR_SUCCESS) return false;

                var adapterMapping = BuildAdapterMapping(profile.RawConfiguration, currentPaths, (int)currentPathCount);

                var paths = new DISPLAYCONFIG_PATH_INFO[profile.RawConfiguration.Paths.Count];
                var modes = new DISPLAYCONFIG_MODE_INFO[profile.RawConfiguration.Modes.Count];

                for (int i = 0; i < profile.RawConfiguration.Paths.Count; i++)
                {
                    var pathData = profile.RawConfiguration.Paths[i];
                    var path = new DISPLAYCONFIG_PATH_INFO();

                    var sourceAdapterId = GetMappedAdapterId(pathData.SourceAdapterId, adapterMapping);
                    var targetAdapterId = GetMappedAdapterId(pathData.TargetAdapterId, adapterMapping);

                    path.sourceInfo.adapterId = sourceAdapterId;
                    path.sourceInfo.id = pathData.SourceId;
                    path.sourceInfo.modeInfoIdx = pathData.SourceModeInfoIdx;
                    path.sourceInfo.statusFlags = pathData.SourceStatusFlags;

                    path.targetInfo.adapterId = targetAdapterId;
                    path.targetInfo.id = pathData.TargetId;
                    path.targetInfo.modeInfoIdx = pathData.TargetModeInfoIdx;
                    path.targetInfo.outputTechnology = (DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY)pathData.OutputTechnology;
                    path.targetInfo.rotation = (DISPLAYCONFIG_ROTATION)pathData.Rotation;
                    path.targetInfo.scaling = (DISPLAYCONFIG_SCALING)pathData.Scaling;
                    path.targetInfo.refreshRate.Numerator = pathData.RefreshRateNumerator;
                    path.targetInfo.refreshRate.Denominator = pathData.RefreshRateDenominator;
                    path.targetInfo.scanLineOrdering = (DISPLAYCONFIG_SCANLINE_ORDERING)pathData.ScanLineOrdering;
                    path.targetInfo.targetAvailable = pathData.TargetAvailable;
                    path.targetInfo.statusFlags = pathData.TargetStatusFlags;

                    path.flags = pathData.Flags;
                    paths[i] = path;
                }

                for (int i = 0; i < profile.RawConfiguration.Modes.Count; i++)
                {
                    var modeData = profile.RawConfiguration.Modes[i];
                    var mode = new DISPLAYCONFIG_MODE_INFO();

                    var adapterId = GetMappedAdapterId(modeData.AdapterId, adapterMapping);

                    mode.infoType = (DISPLAYCONFIG_MODE_INFO_TYPE)modeData.InfoType;
                    mode.id = modeData.Id;
                    mode.adapterId = adapterId;

                    if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                    {
                        mode.modeInfo.sourceMode.width = modeData.Width;
                        mode.modeInfo.sourceMode.height = modeData.Height;
                        mode.modeInfo.sourceMode.pixelFormat = (DISPLAYCONFIG_PIXELFORMAT)modeData.PixelFormat;
                        mode.modeInfo.sourceMode.position.x = modeData.PositionX;
                        mode.modeInfo.sourceMode.position.y = modeData.PositionY;
                    }
                    else if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                    {
                        mode.modeInfo.targetMode.targetVideoSignalInfo.pixelRate = modeData.PixelRate;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.hSyncFreq.Numerator = modeData.HSyncNumerator;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.hSyncFreq.Denominator = modeData.HSyncDenominator;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator = modeData.VSyncNumerator;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator = modeData.VSyncDenominator;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx = modeData.ActiveSizeWidth;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy = modeData.ActiveSizeHeight;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.totalSize.cx = modeData.TotalSizeWidth;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.totalSize.cy = modeData.TotalSizeHeight;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.videoStandard = modeData.VideoStandard;
                        mode.modeInfo.targetMode.targetVideoSignalInfo.scanLineOrdering = (DISPLAYCONFIG_SCANLINE_ORDERING)modeData.TargetScanLineOrdering;
                    }

                    modes[i] = mode;
                }

                uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE;
                result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags);

                return result == ERROR_SUCCESS;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Dictionary<long, LUID> BuildAdapterMapping(DisplayConfigurationData saved, DISPLAYCONFIG_PATH_INFO[] currentPaths, int pathCount)
        {
            var mapping = new Dictionary<long, LUID>();

            var currentAdapters = new HashSet<LUID>();
            for (int i = 0; i < pathCount; i++)
            {
                currentAdapters.Add(currentPaths[i].sourceInfo.adapterId);
            }

            var savedAdapters = new HashSet<long>();
            foreach (var path in saved.Paths)
            {
                savedAdapters.Add(path.SourceAdapterId);
                savedAdapters.Add(path.TargetAdapterId);
            }

            var savedList = savedAdapters.OrderBy(a => a).ToList();
            var currentList = currentAdapters.OrderBy(a => (long)a.HighPart << 32 | a.LowPart).ToList();

            for (int i = 0; i < Math.Min(savedList.Count, currentList.Count); i++)
            {
                mapping[savedList[i]] = currentList[i];
            }

            return mapping;
        }

        private LUID GetMappedAdapterId(long savedAdapterId, Dictionary<long, LUID> mapping)
        {
            if (mapping.TryGetValue(savedAdapterId, out var luid))
            {
                return luid;
            }

            return new LUID
            {
                LowPart = (uint)(savedAdapterId & 0xFFFFFFFF),
                HighPart = (int)(savedAdapterId >> 32)
            };
        }

        public List<MonitorConfiguration> GetAllDisplays()
        {
            var monitors = new List<MonitorConfiguration>();

            int result = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out uint pathCount, out uint modeCount);
            if (result != ERROR_SUCCESS)
            {
                return monitors;
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            result = QueryDisplayConfig(QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (result != ERROR_SUCCESS)
            {
                return monitors;
            }

            var seen = new HashSet<string>();
            for (int i = 0; i < pathCount; i++)
            {
                var path = paths[i];

                var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                targetName.header.adapterId = path.targetInfo.adapterId;
                targetName.header.id = path.targetInfo.id;

                string uniqueId = $"{path.targetInfo.adapterId.LowPart}-{path.targetInfo.id}";

                if (DisplayConfigGetDeviceInfo(ref targetName) == ERROR_SUCCESS)
                {
                    uniqueId = targetName.monitorDevicePath ?? uniqueId;
                }

                if (seen.Contains(uniqueId)) continue;
                seen.Add(uniqueId);

                var config = new MonitorConfiguration
                {
                    TargetId = path.targetInfo.id,
                    IsEnabled = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0,
                    FriendlyName = string.IsNullOrEmpty(targetName.monitorFriendlyDeviceName)
                        ? $"Display {monitors.Count + 1}"
                        : targetName.monitorFriendlyDeviceName,
                    DevicePath = targetName.monitorDevicePath ?? string.Empty
                };

                monitors.Add(config);
            }

            return monitors;
        }
    }
}
