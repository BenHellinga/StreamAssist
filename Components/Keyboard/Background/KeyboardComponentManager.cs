using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardComponentManager : IDisposable
{
    private KeyboardComponentModel _model;

    private KeyboardSaveManager _saveManager;
    public KeyboardInputManager InputManager { get; }
    private KeyboardListManager _listManager;



    // constructor



    public KeyboardComponentManager(KeyboardComponentModel model)
    {
        _model = model;

        _saveManager = new(_model);
        InputManager = new();
        _model.KeyboardList.AttachDevices(KeyboardInputManager.Devices);
        _listManager = new(_model.KeyboardList, InputManager);

        _saveManager.Save();
    }


    // disposal



    public void Dispose()
    {
        _saveManager.Dispose();
        InputManager.Dispose();
        _listManager.Dispose();
    }
}