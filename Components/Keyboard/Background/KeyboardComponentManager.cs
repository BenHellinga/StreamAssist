using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardComponentManager : IDisposable
{
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
        _keybindManager = new(_model.ListModel, _inputManager);

        _saveManager.Save();
    }



    // init



    public void InitHwnd(IntPtr hwnd) => _inputManager.Initialize(hwnd);



    // getters



    public KeyboardInputManager GetInputManager() => _inputManager;



    // disposal



    public void Dispose()
    {
        _keybindManager.Dispose();
        _inputManager.Dispose();
        _saveManager.Dispose();
    }
}