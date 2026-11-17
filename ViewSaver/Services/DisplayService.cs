using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ViewSaver.Models;
using static ViewSaver.Native.NativeMethods;

namespace ViewSaver.Services
{
    /// <summary>Outcome of applying a profile, with a user-facing reason on failure.</summary>
    public sealed record ApplyResult(bool Success, string Message)
    {
        public static ApplyResult Ok(string message = "") => new(true, message);
        public static ApplyResult Fail(string message) => new(false, message);
    }

    public class DisplayService
    {
        private const int MaxQueryAttempts = 5;

        #region Capture

        public DisplayProfile CaptureCurrentConfiguration(string profileName = "Current")
        {
            var profile = new DisplayProfile
            {
                Name = profileName,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };

            if (!TryQueryConfig(QDC_ONLY_ACTIVE_PATHS, out var paths, out var modes, out int error))
            {
                throw new InvalidOperationException($"QueryDisplayConfig failed: {DescribeError(error)}");
            }

            var adapterPathCache = new Dictionary<LUID, string>();
            profile.RawConfiguration = new DisplayConfigurationData();

            foreach (var path in paths)
            {
                var targetName = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);

                profile.RawConfiguration.Paths.Add(new PathInfoData
                {
                    SourceAdapterId = LuidToLong(path.sourceInfo.adapterId),
                    SourceId = path.sourceInfo.id,
                    SourceModeInfoIdx = path.sourceInfo.modeInfoIdx,
                    SourceStatusFlags = path.sourceInfo.statusFlags,
                    TargetAdapterId = LuidToLong(path.targetInfo.adapterId),
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
                    Flags = path.flags,
                    SourceAdapterPath = GetAdapterPath(path.sourceInfo.adapterId, adapterPathCache),
                    TargetAdapterPath = GetAdapterPath(path.targetInfo.adapterId, adapterPathCache),
                    TargetDevicePath = targetName?.monitorDevicePath
                });
            }

            foreach (var mode in modes)
            {
                var modeData = new ModeInfoData
                {
                    InfoType = (uint)mode.infoType,
                    Id = mode.id,
                    AdapterId = LuidToLong(mode.adapterId)
                };

                if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    var src = mode.modeInfo.sourceMode;
                    modeData.Width = src.width;
                    modeData.Height = src.height;
                    modeData.PixelFormat = (uint)src.pixelFormat;
                    modeData.PositionX = src.position.x;
                    modeData.PositionY = src.position.y;
                }
                else if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    var sig = mode.modeInfo.targetMode.targetVideoSignalInfo;
                    modeData.PixelRate = sig.pixelRate;
                    modeData.HSyncNumerator = sig.hSyncFreq.Numerator;
                    modeData.HSyncDenominator = sig.hSyncFreq.Denominator;
                    modeData.VSyncNumerator = sig.vSyncFreq.Numerator;
                    modeData.VSyncDenominator = sig.vSyncFreq.Denominator;
                    modeData.ActiveSizeWidth = sig.activeSize.cx;
                    modeData.ActiveSizeHeight = sig.activeSize.cy;
                    modeData.TotalSizeWidth = sig.totalSize.cx;
                    modeData.TotalSizeHeight = sig.totalSize.cy;
                    modeData.VideoStandard = sig.videoStandard;
                    modeData.TargetScanLineOrdering = (uint)sig.scanLineOrdering;
                }

                profile.RawConfiguration.Modes.Add(modeData);
            }

            profile.Monitors.AddRange(GetMonitorConfigurations(paths, modes, adapterPathCache));
            Logger.Info($"Captured profile '{profileName}' with {paths.Length} path(s), {modes.Length} mode(s).");
            return profile;
        }

        private List<MonitorConfiguration> GetMonitorConfigurations(
            DISPLAYCONFIG_PATH_INFO[] paths,
            DISPLAYCONFIG_MODE_INFO[] modes,
            Dictionary<LUID, string> adapterPathCache)
        {
            var monitors = new List<MonitorConfiguration>();
            var displayDevices = GetDisplayDevices();

            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                var config = new MonitorConfiguration
                {
                    SourceId = path.sourceInfo.id,
                    TargetId = path.targetInfo.id,
                    IsEnabled = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0,
                    DisplayOrder = i,
                    FriendlyName = $"Display {i + 1}"
                };

                var targetName = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);
                if (targetName is { } tn)
                {
                    if (!string.IsNullOrEmpty(tn.monitorFriendlyDeviceName))
                        config.FriendlyName = tn.monitorFriendlyDeviceName;
                    config.DevicePath = tn.monitorDevicePath ?? string.Empty;
                }

                var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                sourceName.header.adapterId = path.sourceInfo.adapterId;
                sourceName.header.id = path.sourceInfo.id;
                if (DisplayConfigGetDeviceInfo(ref sourceName) == ERROR_SUCCESS)
                {
                    config.DeviceName = sourceName.viewGdiDeviceName ?? string.Empty;
                }

                config.AdapterPath = GetAdapterPath(path.sourceInfo.adapterId, adapterPathCache) ?? string.Empty;

                if (path.sourceInfo.modeInfoIdx < modes.Length)
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

                if (path.targetInfo.modeInfoIdx < modes.Length &&
                    modes[path.targetInfo.modeInfoIdx].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    var vSync = modes[path.targetInfo.modeInfoIdx].modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq;
                    if (vSync.Denominator > 0)
                        config.RefreshRate = (int)Math.Round((double)vSync.Numerator / vSync.Denominator);
                }
                else if (path.targetInfo.refreshRate.Denominator > 0)
                {
                    config.RefreshRate = (int)Math.Round((double)path.targetInfo.refreshRate.Numerator / path.targetInfo.refreshRate.Denominator);
                }

                config.Orientation = Math.Max(0, (int)path.targetInfo.rotation - 1); // IDENTITY is 1, we want 0-based

                // The primary display is always the one whose desktop origin is (0,0).
                config.IsPrimary = config.IsEnabled && config.PositionX == 0 && config.PositionY == 0;

                var device = displayDevices.FirstOrDefault(d => d.DeviceName == config.DeviceName);
                if (device.DeviceName != null)
                {
                    config.IsPrimary = (device.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                }

                if (!string.IsNullOrEmpty(config.DeviceName))
                {
                    var devMode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
                    if (EnumDisplaySettings(config.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        config.BitsPerPixel = devMode.dmBitsPerPel;
                        if (config.RefreshRate == 0)
                            config.RefreshRate = devMode.dmDisplayFrequency;
                    }
                }

                config.ScalingPercent = 100;
                monitors.Add(config);
            }

            return monitors;
        }

        private static List<DISPLAY_DEVICE> GetDisplayDevices()
        {
            var devices = new List<DISPLAY_DEVICE>();
            for (uint index = 0; ; index++)
            {
                var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(null, index, ref device, 0))
                    break;

                if ((device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                    devices.Add(device);
            }
            return devices;
        }

        #endregion

        #region Apply

        public ApplyResult ApplyProfile(DisplayProfile profile)
        {
            if (profile.RawConfiguration == null || profile.RawConfiguration.Paths.Count == 0)
            {
                return Fail(profile, "The profile contains no display data. Re-save it with \"Update\".");
            }

            try
            {
                if (!TryQueryConfig(QDC_ALL_PATHS, out var currentPaths, out _, out int error))
                {
                    return Fail(profile, $"Could not read the current display configuration ({DescribeError(error)}).");
                }

                var snapshot = CurrentTopology.Build(currentPaths);
                Logger.Info($"Applying '{profile.Name}'. Current adapters: {string.Join("; ", snapshot.AdapterPaths.Select(kv => $"{LuidToLong(kv.Value)}={kv.Key}"))}");

                if (!TryBuildConfig(profile, snapshot, out var paths, out var modes, out string buildError))
                {
                    return Fail(profile, buildError);
                }

                // Attempt 1: the exact saved configuration (resolution, refresh, position, orientation).
                const uint fullFlags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE;
                int result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, fullFlags);
                if (result == ERROR_SUCCESS)
                {
                    Logger.Info($"Applied '{profile.Name}' using the full saved configuration.");
                    return ApplyResult.Ok();
                }
                Logger.Warning($"Full apply of '{profile.Name}' failed: {DescribeError(result)}. Retrying with topology only.");

                // Attempt 2: enable the right monitors and let Windows pick modes, then restore layout.
                var topologyPaths = paths.Select(p =>
                {
                    p.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    p.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    return p;
                }).ToArray();

                result = SetDisplayConfig((uint)topologyPaths.Length, topologyPaths, 0, null,
                    SDC_APPLY | SDC_TOPOLOGY_SUPPLIED | SDC_ALLOW_PATH_ORDER_CHANGES);
                if (result != ERROR_SUCCESS)
                {
                    return Fail(profile, $"Windows rejected the configuration ({DescribeError(result)}).");
                }

                if (TryRestoreLayout(modes, paths, out string layoutError))
                {
                    Logger.Info($"Applied '{profile.Name}' via topology + layout restore.");
                    return ApplyResult.Ok();
                }

                Logger.Warning($"'{profile.Name}': monitors enabled but layout restore failed: {layoutError}");
                return ApplyResult.Ok("Monitors were switched, but the exact resolution/position could not be restored. " +
                                      "Adjust in Windows Display Settings and use \"Update\" to re-save the profile.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error applying '{profile.Name}'", ex);
                return ApplyResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>Asks Windows whether the profile could be applied right now, without changing anything.</summary>
        public ApplyResult ValidateProfile(DisplayProfile profile)
        {
            if (profile.RawConfiguration == null || profile.RawConfiguration.Paths.Count == 0)
                return ApplyResult.Fail("The profile contains no display data.");

            try
            {
                if (!TryQueryConfig(QDC_ALL_PATHS, out var currentPaths, out _, out int error))
                    return ApplyResult.Fail($"Could not read the current display configuration ({DescribeError(error)}).");

                if (!TryBuildConfig(profile, CurrentTopology.Build(currentPaths), out var paths, out var modes, out string buildError))
                    return ApplyResult.Fail(buildError);

                int result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
                    SDC_VALIDATE | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                return result == ERROR_SUCCESS
                    ? ApplyResult.Ok()
                    : ApplyResult.Fail($"Windows would reject this layout ({DescribeError(result)}).");
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error validating '{profile.Name}'", ex);
                return ApplyResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        private static ApplyResult Fail(DisplayProfile profile, string message)
        {
            Logger.Error($"Failed to apply '{profile.Name}': {message}");
            return ApplyResult.Fail(message);
        }

        /// <summary>
        /// Translates the saved paths/modes into ones valid for the current session. Adapter LUIDs are
        /// regenerated every boot, so they are matched by adapter device path (stable), and monitors by
        /// monitor device path, falling back to the old raw IDs only when nothing better is available.
        /// </summary>
        private static bool TryBuildConfig(
            DisplayProfile profile,
            CurrentTopology current,
            out DISPLAYCONFIG_PATH_INFO[] paths,
            out DISPLAYCONFIG_MODE_INFO[] modes,
            out string error)
        {
            var raw = profile.RawConfiguration!;
            paths = new DISPLAYCONFIG_PATH_INFO[raw.Paths.Count];
            modes = new DISPLAYCONFIG_MODE_INFO[raw.Modes.Count];
            var targetRemap = new Dictionary<(long adapter, uint target), uint>();
            error = string.Empty;

            // Older profiles don't store paths on PathInfoData; Monitors[i] was captured in the same order.
            bool monitorsAligned = profile.Monitors.Count == raw.Paths.Count;
            var adapterMap = new Dictionary<long, LUID>();

            for (int i = 0; i < raw.Paths.Count; i++)
            {
                var p = raw.Paths[i];
                var monitor = monitorsAligned ? profile.Monitors[i] : null;
                string? sourceAdapterPath = p.SourceAdapterPath ?? monitor?.AdapterPath;
                string? targetAdapterPath = p.TargetAdapterPath ?? (p.TargetAdapterId == p.SourceAdapterId ? sourceAdapterPath : null);
                string? devicePath = p.TargetDevicePath ?? monitor?.DevicePath;
                string label = monitor?.FriendlyName is { Length: > 0 } n ? n : $"Display {i + 1}";

                // Monitor device path -> its current adapter + target id (handles port changes too).
                if (!string.IsNullOrEmpty(devicePath) && current.Targets.TryGetValue(devicePath, out var target))
                {
                    adapterMap.TryAdd(p.TargetAdapterId, target.Adapter);
                    targetRemap[(p.TargetAdapterId, p.TargetId)] = target.TargetId;
                }
                else if (!string.IsNullOrEmpty(devicePath) && (p.Flags & DISPLAYCONFIG_PATH_ACTIVE) != 0)
                {
                    error = $"Monitor \"{label}\" is not connected or is not detected by Windows.";
                    return false;
                }

                if (!string.IsNullOrEmpty(sourceAdapterPath) && current.AdapterPaths.TryGetValue(sourceAdapterPath, out var srcLuid))
                    adapterMap.TryAdd(p.SourceAdapterId, srcLuid);
                if (!string.IsNullOrEmpty(targetAdapterPath) && current.AdapterPaths.TryGetValue(targetAdapterPath, out var tgtLuid))
                    adapterMap.TryAdd(p.TargetAdapterId, tgtLuid);
            }

            // Last-resort mapping for profiles with no stored paths.
            foreach (long saved in raw.Paths.SelectMany(p => new[] { p.SourceAdapterId, p.TargetAdapterId }).Distinct())
            {
                if (adapterMap.ContainsKey(saved)) continue;

                var savedLuid = LongToLuid(saved);
                if (current.AdapterPaths.ContainsValue(savedLuid))
                {
                    adapterMap[saved] = savedLuid; // same boot session, LUID still valid
                }
                else if (current.AdaptersWithMonitors.Count == 1)
                {
                    adapterMap[saved] = current.AdaptersWithMonitors.First();
                }
                else
                {
                    error = "The graphics adapter this profile was saved on could not be identified. " +
                            "Apply the layout manually once, then use \"Update\" to re-save the profile.";
                    return false;
                }
            }

            LUID Map(long id) => adapterMap[id];
            uint MapTarget(long adapter, uint id) => targetRemap.TryGetValue((adapter, id), out var t) ? t : id;

            for (int i = 0; i < raw.Paths.Count; i++)
            {
                var d = raw.Paths[i];
                var path = new DISPLAYCONFIG_PATH_INFO();
                path.sourceInfo.adapterId = Map(d.SourceAdapterId);
                path.sourceInfo.id = d.SourceId;
                path.sourceInfo.modeInfoIdx = d.SourceModeInfoIdx;
                path.sourceInfo.statusFlags = 0;

                path.targetInfo.adapterId = Map(d.TargetAdapterId);
                path.targetInfo.id = MapTarget(d.TargetAdapterId, d.TargetId);
                path.targetInfo.modeInfoIdx = d.TargetModeInfoIdx;
                path.targetInfo.outputTechnology = (DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY)d.OutputTechnology;
                path.targetInfo.rotation = (DISPLAYCONFIG_ROTATION)d.Rotation;
                path.targetInfo.scaling = (DISPLAYCONFIG_SCALING)d.Scaling;
                path.targetInfo.refreshRate.Numerator = d.RefreshRateNumerator;
                path.targetInfo.refreshRate.Denominator = d.RefreshRateDenominator;
                path.targetInfo.scanLineOrdering = (DISPLAYCONFIG_SCANLINE_ORDERING)d.ScanLineOrdering;
                path.targetInfo.targetAvailable = true;
                path.targetInfo.statusFlags = 0;
                path.flags = d.Flags;
                paths[i] = path;
            }

            for (int i = 0; i < raw.Modes.Count; i++)
            {
                var d = raw.Modes[i];
                var mode = new DISPLAYCONFIG_MODE_INFO
                {
                    infoType = (DISPLAYCONFIG_MODE_INFO_TYPE)d.InfoType,
                    adapterId = adapterMap.TryGetValue(d.AdapterId, out var luid) ? luid : LongToLuid(d.AdapterId),
                    id = d.Id
                };

                if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    mode.modeInfo.sourceMode.width = d.Width;
                    mode.modeInfo.sourceMode.height = d.Height;
                    mode.modeInfo.sourceMode.pixelFormat = (DISPLAYCONFIG_PIXELFORMAT)d.PixelFormat;
                    mode.modeInfo.sourceMode.position.x = d.PositionX;
                    mode.modeInfo.sourceMode.position.y = d.PositionY;
                }
                else if (mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
                {
                    mode.id = MapTarget(d.AdapterId, d.Id);
                    ref var sig = ref mode.modeInfo.targetMode.targetVideoSignalInfo;
                    sig.pixelRate = d.PixelRate;
                    sig.hSyncFreq.Numerator = d.HSyncNumerator;
                    sig.hSyncFreq.Denominator = d.HSyncDenominator;
                    sig.vSyncFreq.Numerator = d.VSyncNumerator;
                    sig.vSyncFreq.Denominator = d.VSyncDenominator;
                    sig.activeSize.cx = d.ActiveSizeWidth;
                    sig.activeSize.cy = d.ActiveSizeHeight;
                    sig.totalSize.cx = d.TotalSizeWidth;
                    sig.totalSize.cy = d.TotalSizeHeight;
                    sig.videoStandard = d.VideoStandard;
                    sig.scanLineOrdering = (DISPLAYCONFIG_SCANLINE_ORDERING)d.TargetScanLineOrdering;
                }

                modes[i] = mode;
            }

            return true;
        }

        /// <summary>
        /// After a topology-only switch, re-apply the saved source resolution/position onto
        /// whatever modes Windows chose, matching each active path by its target.
        /// </summary>
        private static bool TryRestoreLayout(
            DISPLAYCONFIG_MODE_INFO[] savedModes,
            DISPLAYCONFIG_PATH_INFO[] savedPaths,
            out string error)
        {
            error = string.Empty;
            if (!TryQueryConfig(QDC_ONLY_ACTIVE_PATHS, out var paths, out var modes, out int qerr))
            {
                error = DescribeError(qerr);
                return false;
            }

            bool changed = false;
            foreach (var active in paths)
            {
                int savedIdx = Array.FindIndex(savedPaths, p =>
                    p.targetInfo.adapterId.Equals(active.targetInfo.adapterId) && p.targetInfo.id == active.targetInfo.id);
                if (savedIdx < 0) continue;

                var saved = savedPaths[savedIdx];
                if (saved.sourceInfo.modeInfoIdx >= savedModes.Length || active.sourceInfo.modeInfoIdx >= modes.Length)
                    continue;

                var savedSrc = savedModes[saved.sourceInfo.modeInfoIdx].modeInfo.sourceMode;
                ref var src = ref modes[active.sourceInfo.modeInfoIdx].modeInfo.sourceMode;
                src.width = savedSrc.width;
                src.height = savedSrc.height;
                src.position = savedSrc.position;
                changed = true;
            }

            if (!changed) return true;

            int result = SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes,
                SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
            if (result != ERROR_SUCCESS)
            {
                error = DescribeError(result);
                return false;
            }
            return true;
        }

        private sealed class CurrentTopology
        {
            public Dictionary<string, LUID> AdapterPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, (LUID Adapter, uint TargetId)> Targets { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<LUID> AdaptersWithMonitors { get; } = new();

            public static CurrentTopology Build(DISPLAYCONFIG_PATH_INFO[] paths)
            {
                var topo = new CurrentTopology();
                var cache = new Dictionary<LUID, string>();

                foreach (var path in paths)
                {
                    foreach (var luid in new[] { path.sourceInfo.adapterId, path.targetInfo.adapterId })
                    {
                        var adapterPath = GetAdapterPath(luid, cache);
                        if (!string.IsNullOrEmpty(adapterPath))
                            topo.AdapterPaths.TryAdd(adapterPath, luid);
                    }

                    if (!path.targetInfo.targetAvailable) continue;

                    var name = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);
                    if (name?.monitorDevicePath is { Length: > 0 } devicePath)
                    {
                        topo.Targets.TryAdd(devicePath, (path.targetInfo.adapterId, path.targetInfo.id));
                        topo.AdaptersWithMonitors.Add(path.targetInfo.adapterId);
                    }
                }

                return topo;
            }
        }

        #endregion

        #region Helpers

        public List<MonitorConfiguration> GetAllDisplays()
        {
            var monitors = new List<MonitorConfiguration>();
            if (!TryQueryConfig(QDC_ALL_PATHS, out var paths, out _, out _))
                return monitors;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (!path.targetInfo.targetAvailable) continue;

                var targetName = GetTargetName(path.targetInfo.adapterId, path.targetInfo.id);
                string uniqueId = targetName?.monitorDevicePath is { Length: > 0 } dp
                    ? dp
                    : $"{LuidToLong(path.targetInfo.adapterId)}-{path.targetInfo.id}";

                if (!seen.Add(uniqueId)) continue;

                monitors.Add(new MonitorConfiguration
                {
                    TargetId = path.targetInfo.id,
                    IsEnabled = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0,
                    FriendlyName = targetName?.monitorFriendlyDeviceName is { Length: > 0 } fn ? fn : $"Display {monitors.Count + 1}",
                    DevicePath = targetName?.monitorDevicePath ?? string.Empty
                });
            }

            return monitors;
        }

        /// <summary>Queries the display config, retrying if it changes between the size and query calls.</summary>
        private static bool TryQueryConfig(uint flags, out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes, out int error)
        {
            paths = Array.Empty<DISPLAYCONFIG_PATH_INFO>();
            modes = Array.Empty<DISPLAYCONFIG_MODE_INFO>();
            error = ERROR_SUCCESS;

            for (int attempt = 0; attempt < MaxQueryAttempts; attempt++)
            {
                error = GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
                if (error != ERROR_SUCCESS) return false;

                var p = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var m = new DISPLAYCONFIG_MODE_INFO[modeCount];
                error = QueryDisplayConfig(flags, ref pathCount, p, ref modeCount, m, IntPtr.Zero);

                if (error == ERROR_INSUFFICIENT_BUFFER) continue;
                if (error != ERROR_SUCCESS) return false;

                paths = p.Take((int)pathCount).ToArray();
                modes = m.Take((int)modeCount).ToArray();
                return true;
            }

            return false;
        }

        private static DISPLAYCONFIG_TARGET_DEVICE_NAME? GetTargetName(LUID adapter, uint targetId)
        {
            var name = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            name.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            name.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
            name.header.adapterId = adapter;
            name.header.id = targetId;
            return DisplayConfigGetDeviceInfo(ref name) == ERROR_SUCCESS ? name : null;
        }

        private static string? GetAdapterPath(LUID adapter, Dictionary<LUID, string> cache)
        {
            if (cache.TryGetValue(adapter, out var cached)) return cached;

            var name = new DISPLAYCONFIG_ADAPTER_NAME();
            name.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
            name.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_ADAPTER_NAME>();
            name.header.adapterId = adapter;

            if (DisplayConfigGetDeviceInfo(ref name) != ERROR_SUCCESS) return null;
            cache[adapter] = name.adapterDevicePath ?? string.Empty;
            return cache[adapter];
        }

        private static long LuidToLong(LUID luid) => ((long)luid.HighPart << 32) | luid.LowPart;

        private static LUID LongToLuid(long value) => new()
        {
            LowPart = (uint)(value & 0xFFFFFFFF),
            HighPart = (int)(value >> 32)
        };

        public static string DescribeError(int code) => code switch
        {
            ERROR_SUCCESS => "success",
            ERROR_ACCESS_DENIED => "access denied — Windows blocks display changes from a locked, remote or secure-desktop session (error 5)",
            ERROR_GEN_FAILURE => "general driver failure (error 31)",
            ERROR_NOT_SUPPORTED => "not supported by the graphics driver (error 50)",
            ERROR_INVALID_PARAMETER => "invalid configuration — a monitor, adapter or mode no longer matches (error 87)",
            ERROR_INSUFFICIENT_BUFFER => "display configuration changed during the query (error 122)",
            ERROR_BAD_CONFIGURATION => "no matching configuration in the Windows display database (error 1610)",
            _ => $"error {code}"
        };

        #endregion
    }
}
