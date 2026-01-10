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
            catch (Exception)
            {
                _profiles = new List<DisplayProfile>();
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
                File.WriteAllText(_profilesPath, json);
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
            catch (Exception)
            {
                _settings = new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
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

        public bool ApplyProfile(string profileId)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return false;

            _settings.LastSelectedProfileId = profileId;
            SaveSettings();

            return _displayService.ApplyProfile(profile);
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

        public void ExportProfiles(string filePath)
        {
            var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public int ImportProfiles(string filePath, bool replaceExisting = false)
        {
            var json = File.ReadAllText(filePath);
            var importedProfiles = JsonConvert.DeserializeObject<List<DisplayProfile>>(json);

            if (importedProfiles == null) return 0;

            if (replaceExisting)
            {
                _profiles = importedProfiles;
            }
            else
            {
                // Generate new IDs to avoid conflicts
                foreach (var profile in importedProfiles)
                {
                    profile.Id = Guid.NewGuid().ToString();
                    _profiles.Add(profile);
                }
            }

            SaveProfiles();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return importedProfiles.Count;
        }
    }
}
