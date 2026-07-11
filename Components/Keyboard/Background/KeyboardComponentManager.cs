using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardComponentManager : IDisposable
{
    private KeyboardComponentModel _model;

    private KeyboardSaveManager _saveManager;
    private KeyboardInputManager _inputManager;
    private KeyboardListManager _listManager;



    // constructor



    public KeyboardComponentManager(KeyboardComponentModel model)
    {
        _model = model;

        _saveManager = new(_model);
        _inputManager = new();
        _model.KeyboardList.AttachDevices(KeyboardInputManager.Devices);
        _listManager = new(_model.KeyboardList, _inputManager);

        _saveManager.Save();
    }



    // init



    public void InitHwnd(IntPtr hwnd) => _inputManager.Initialize(hwnd);



    // getters



    public KeyboardInputManager GetInputManager() => _inputManager;



    // disposal



    public void Dispose()
    {
        _saveManager.Dispose();
        _inputManager.Dispose();
        _listManager.Dispose();
    }
}