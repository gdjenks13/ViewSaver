using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ViewSaver.Models;
using ViewSaver.Native;

namespace ViewSaver.Services
{
    public class HotkeyManager : IDisposable
    {
        private readonly IntPtr _windowHandle;
        private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys;
        private int _nextHotkeyId;
        private bool _disposed;

        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _registeredHotkeys = new Dictionary<int, HotkeyBinding>();
            _nextHotkeyId = 1;
        }

        public int RegisterHotkey(HotkeyBinding hotkey)
        {
            if (!hotkey.IsValid)
                throw new ArgumentException("Invalid hotkey binding");

            foreach (var kvp in _registeredHotkeys)
            {
                if (kvp.Value.Equals(hotkey))
                {
                    return kvp.Key; // Return existing ID
                }
            }

            int id = _nextHotkeyId++;
            uint modifiers = hotkey.GetModifiers();
            uint vk = (uint)hotkey.Key;

            if (!NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, vk))
            {
                throw new InvalidOperationException($"Failed to register hotkey {hotkey}. It may be in use by another application.");
            }

            _registeredHotkeys[id] = hotkey;
            return id;
        }

        public void UnregisterHotkey(int id)
        {
            if (_registeredHotkeys.ContainsKey(id))
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
                _registeredHotkeys.Remove(id);
            }
        }

        public void UnregisterHotkey(HotkeyBinding hotkey)
        {
            int? idToRemove = null;
            foreach (var kvp in _registeredHotkeys)
            {
                if (kvp.Value.Equals(hotkey))
                {
                    idToRemove = kvp.Key;
                    break;
                }
            }

            if (idToRemove.HasValue)
            {
                UnregisterHotkey(idToRemove.Value);
            }
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var id in _registeredHotkeys.Keys)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
            _registeredHotkeys.Clear();
        }

        public void ProcessHotkey(Message m)
        {
            if (m.Msg != NativeMethods.WM_HOTKEY) return;

            int id = m.WParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out var hotkey))
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkey));
            }
        }

        public HotkeyBinding? GetHotkey(int id)
        {
            return _registeredHotkeys.TryGetValue(id, out var hotkey) ? hotkey : null;
        }

        public bool IsRegistered(HotkeyBinding hotkey)
        {
            foreach (var kvp in _registeredHotkeys)
            {
                if (kvp.Value.Equals(hotkey))
                    return true;
            }
            return false;
        }

        public IEnumerable<HotkeyBinding> GetRegisteredHotkeys()
        {
            return _registeredHotkeys.Values;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterAllHotkeys();
                _disposed = true;
            }
        }
    }

    public class HotkeyPressedEventArgs : EventArgs
    {
        public HotkeyBinding Hotkey { get; }

        public HotkeyPressedEventArgs(HotkeyBinding hotkey)
        {
            Hotkey = hotkey;
        }
    }
}
