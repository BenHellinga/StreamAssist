using System;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Background;



public class InputComponentManager : IDisposable
{
    // private



    private readonly InputComponentModel _model;

    private readonly InputSaveManager _saveManager;
    private readonly DeviceInputManager _inputManager;
    private readonly KeybindManager _keybindManager;
    private readonly MicrophoneManager _microphoneManager; // microphone boxes: levels and talking



    // constructor



    public InputComponentManager(InputComponentModel model)
    {
        _model = model;

        _saveManager = new(_model);
        _inputManager = new();
        _model.ListModel.AttachDevices(DeviceInputManager.Devices);
        _inputManager.DevicesChanged += OnDevicesChanged;
        _keybindManager = new(_model.ListModel, _inputManager);
        _microphoneManager = new(_model.ListModel);

        _saveManager.Save();
    }



    // input events



    // a keyboard was plugged in or removed: slots find theirs again (or lose it until it's back)
    private void OnDevicesChanged() => _model.ListModel.AttachDevices(DeviceInputManager.Devices);



    // init



    public void InitHwnd(IntPtr hwnd) => _inputManager.Initialize(hwnd);



    // public api



    public DeviceInputManager GetInputManager() => _inputManager;



    // disposal



    public void Dispose()
    {
        _inputManager.DevicesChanged -= OnDevicesChanged;
        _keybindManager.Dispose();
        _microphoneManager.Dispose();
        _inputManager.Dispose();
        _saveManager.Dispose();
    }
}