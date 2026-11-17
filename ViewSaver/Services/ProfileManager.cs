using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ViewSaver.Models;

namespace ViewSaver.Services
{
    public class ProfileManager
    {
        private readonly string _profilesPath;
        private readonly string _settingsPath;
        private readonly DisplayService _displayService;
        private List<DisplayProfile> _profiles;
        private AppSettings _settings;

        public event EventHandler? ProfilesChanged;
        public event EventHandler? SettingsChanged;

        public IReadOnlyList<DisplayProfile> Profiles => _profiles.AsReadOnly();
        public AppSettings Settings => _settings;

        public ProfileManager()
        {
            _displayService = new DisplayService();
            _profiles = new List<DisplayProfile>();
            _settings = new AppSettings();

            // Store data in AppData/Roaming/ViewSaver
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ViewSaver"
            );

            Directory.CreateDirectory(appDataPath);
            _profilesPath = Path.Combine(appDataPath, "profiles.json");
            _settingsPath = Path.Combine(appDataPath, "settings.json");

            LoadProfiles();
            LoadSettings();
        }

        public void LoadProfiles()
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    var json = File.ReadAllText(_profilesPath);
                    _profiles = JsonConvert.DeserializeObject<List<DisplayProfile>>(json) ?? new List<DisplayProfile>();
                }
            }
            catch (Exception ex)
            {
                // Keep a copy so a corrupt file isn't silently overwritten on the next save.
                Logger.Error("Failed to load profiles; backing up the unreadable file", ex);
                TryBackup(_profilesPath);
                _profiles = new List<DisplayProfile>();
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
                WriteAtomic(_profilesPath, json);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save profiles", ex);
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings; using defaults", ex);
                TryBackup(_settingsPath);
                _settings = new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                WriteAtomic(_settingsPath, json);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }

        public DisplayProfile CaptureNewProfile(string name)
        {
            var profile = _displayService.CaptureCurrentConfiguration(name);
            _profiles.Add(profile);
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return profile;
        }

        public void UpdateProfile(string profileId)
        {
            var existingProfile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (existingProfile == null) return;

            var newConfig = _displayService.CaptureCurrentConfiguration(existingProfile.Name);
            existingProfile.Monitors = newConfig.Monitors;
            existingProfile.RawConfiguration = newConfig.RawConfiguration;
            existingProfile.ModifiedAt = DateTime.Now;

            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RenameProfile(string profileId, string newName)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return;

            profile.Name = newName;
            profile.ModifiedAt = DateTime.Now;

            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteProfile(string profileId)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return;

            _profiles.Remove(profile);
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetProfileHotkey(string profileId, HotkeyBinding? hotkey)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return;

            if (hotkey != null && hotkey.IsValid)
            {
                foreach (var p in _profiles)
                {
                    if (p.Id != profileId && p.Hotkey != null && p.Hotkey.Equals(hotkey))
                    {
                        throw new InvalidOperationException($"Hotkey {hotkey} is already assigned to profile '{p.Name}'");
                    }
                }
            }

            profile.Hotkey = hotkey;
            profile.ModifiedAt = DateTime.Now;

            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public ApplyResult ApplyProfile(string profileId)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return ApplyResult.Fail("Profile not found.");

            var result = _displayService.ApplyProfile(profile);
            if (result.Success)
            {
                _settings.LastSelectedProfileId = profileId;
                SaveSettings();
            }
            return result;
        }

        private static void WriteAtomic(string path, string contents)
        {
            // Write to a temp file and swap it in so a crash mid-write can't truncate the real file.
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        }

        private static void TryBackup(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Copy(path, $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not back up {path}", ex);
            }
        }

        public DisplayProfile? GetProfile(string profileId)
        {
            return _profiles.FirstOrDefault(p => p.Id == profileId);
        }

        public DisplayProfile? GetProfileByHotkey(HotkeyBinding hotkey)
        {
            return _profiles.FirstOrDefault(p => p.Hotkey != null && p.Hotkey.Equals(hotkey));
        }

        public IEnumerable<DisplayProfile> GetProfilesWithHotkeys()
        {
            return _profiles.Where(p => p.Hotkey != null && p.Hotkey.IsValid);
        }

        public void MoveProfileUp(string profileId)
        {
            var index = _profiles.FindIndex(p => p.Id == profileId);
            if (index > 0)
            {
                var profile = _profiles[index];
                _profiles.RemoveAt(index);
                _profiles.Insert(index - 1, profile);
                SaveProfiles();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void MoveProfileDown(string profileId)
        {
            var index = _profiles.FindIndex(p => p.Id == profileId);
            if (index >= 0 && index < _profiles.Count - 1)
            {
                var profile = _profiles[index];
                _profiles.RemoveAt(index);
                _profiles.Insert(index + 1, profile);
                SaveProfiles();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #region Export / Import (single profiles) and Backup / Restore (all profiles)

        /// <summary>Exports one profile to its own file.</summary>
        public void ExportProfile(string profileId, string filePath)
        {
            var profile = GetProfile(profileId) ?? throw new InvalidOperationException("Profile not found.");
            WriteAtomic(filePath, JsonConvert.SerializeObject(profile, Formatting.Indented));
        }

        /// <summary>Writes every profile to a single backup file.</summary>
        public void BackupProfiles(string filePath)
        {
            WriteAtomic(filePath, JsonConvert.SerializeObject(_profiles, Formatting.Indented));
        }

        /// <summary>
        /// Adds the profile(s) in a file to the current list. Accepts a single exported profile or a backup.
        /// Imported profiles get new IDs, unique names, and drop hotkeys that are already in use.
        /// </summary>
        public int ImportProfiles(string filePath)
        {
            var imported = ReadProfilesFile(filePath);
            foreach (var profile in imported)
            {
                profile.Id = Guid.NewGuid().ToString();
                profile.Name = MakeUniqueName(profile.Name);
                if (profile.Hotkey != null && _profiles.Any(p => profile.Hotkey.Equals(p.Hotkey)))
                {
                    Logger.Info($"Cleared hotkey {profile.Hotkey} on imported '{profile.Name}' (already in use).");
                    profile.Hotkey = null;
                }
                _profiles.Add(profile);
            }

            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return imported.Count;
        }

        /// <summary>Replaces all profiles with the contents of a backup file.</summary>
        public int RestoreProfiles(string filePath)
        {
            var restored = ReadProfilesFile(filePath);
            _profiles = restored;
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return restored.Count;
        }

        private static List<DisplayProfile> ReadProfilesFile(string filePath)
        {
            var json = File.ReadAllText(filePath).TrimStart();
            List<DisplayProfile>? profiles = json.StartsWith('[')
                ? JsonConvert.DeserializeObject<List<DisplayProfile>>(json)
                : JsonConvert.DeserializeObject<DisplayProfile>(json) is { } single ? new List<DisplayProfile> { single } : null;

            profiles?.RemoveAll(p => p == null);
            if (profiles == null || profiles.Count == 0)
                throw new InvalidDataException("The file does not contain any ViewSaver profiles.");
            return profiles;
        }

        private string MakeUniqueName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Imported Profile";
            if (!_profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return name;

            for (int i = 2; ; i++)
            {
                var candidate = $"{name} ({i})";
                if (!_profiles.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
        }

        #endregion

        /// <summary>Saves hand-edited monitor positions to a profile.</summary>
        public void UpdateMonitorPositions(string profileId, IReadOnlyList<(int X, int Y)> positions)
        {
            var profile = GetProfile(profileId) ?? throw new InvalidOperationException("Profile not found.");
            SetMonitorPositions(profile, positions);
            profile.ModifiedAt = DateTime.Now;
            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets monitor positions on a profile, keeping the raw configuration used by SetDisplayConfig in sync.
        /// </summary>
        public static void SetMonitorPositions(DisplayProfile profile, IReadOnlyList<(int X, int Y)> positions)
        {
            if (positions.Count != profile.Monitors.Count)
                throw new ArgumentException("Position count does not match monitor count.", nameof(positions));

            var raw = profile.RawConfiguration;
            bool aligned = raw != null && raw.Paths.Count == profile.Monitors.Count;

            for (int i = 0; i < positions.Count; i++)
            {
                profile.Monitors[i].PositionX = positions[i].X;
                profile.Monitors[i].PositionY = positions[i].Y;

                if (aligned)
                {
                    int modeIdx = (int)raw!.Paths[i].SourceModeInfoIdx;
                    if (modeIdx >= 0 && modeIdx < raw.Modes.Count)
                    {
                        raw.Modes[modeIdx].PositionX = positions[i].X;
                        raw.Modes[modeIdx].PositionY = positions[i].Y;
                    }
                }
            }

            // Windows treats the display at (0,0) as primary.
            foreach (var m in profile.Monitors)
                m.IsPrimary = m.IsEnabled && m.PositionX == 0 && m.PositionY == 0;
        }

        public static DisplayProfile Clone(DisplayProfile profile) =>
            JsonConvert.DeserializeObject<DisplayProfile>(JsonConvert.SerializeObject(profile))!;

        public ApplyResult ValidateProfile(DisplayProfile profile) => _displayService.ValidateProfile(profile);
    }
}
