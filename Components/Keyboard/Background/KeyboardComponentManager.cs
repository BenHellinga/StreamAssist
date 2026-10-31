using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardComponentManager : IDisposable
{
    // private



    private readonly KeyboardComponentModel _model;

    private readonly KeyboardSaveManager _saveManager;
    private readonly KeyboardInputManager _inputManager;
    private readonly KeybindManager _keybindManager;



    // constructor



    public KeyboardComponentManager(KeyboardComponentModel model)
    {
        _model = model;

        _saveManager = new(_model);
        _inputManager = new();
        _model.ListModel.AttachDevices(KeyboardInputManager.Devices);
        _inputManager.DevicesChanged += OnDevicesChanged;
        _keybindManager = new(_model.ListModel, _inputManager);

        _saveManager.Save();
    }



    // input events



    // a keyboard was plugged in or removed: slots find theirs again (or lose it until it's back)
    private void OnDevicesChanged() => _model.ListModel.AttachDevices(KeyboardInputManager.Devices);



    // init



    public void InitHwnd(IntPtr hwnd) => _inputManager.Initialize(hwnd);



    // public api



    public KeyboardInputManager GetInputManager() => _inputManager;



    // disposal



    public void Dispose()
    {
        _inputManager.DevicesChanged -= OnDevicesChanged;
        _keybindManager.Dispose();
        _inputManager.Dispose();
        _saveManager.Dispose();
    }
}