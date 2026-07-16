using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Utilities;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.AppAction.Model;



public class ActionGroupModel
{
    // private



    private List<IAppAction> _actions;



    // public


    
    [JsonIgnore] public IReadOnlyList<IAppAction> Actions => _actions; 



    // json



    [JsonInclude] [JsonPropertyName("actions")]
    private List<PendingActionData> JsonActions
    {
        get => _actions.Select(ActionResolver.GetKey).ToList();
        set
        {
            _actions = [];
            foreach (var data in value)
            {
                var action = ActionResolver.Resolve(data);
                if (action is not null) _actions.Add(action);
            }
        }
    }



    // events



    public event Action<IAppAction, int>? ActionAdded;
    public event Action<IAppAction>? ActionRemoved;



    // constructor



    public ActionGroupModel()
    {
        _actions = [];
    }



    // action management


    
    public void AddAction(IAppAction action)
    {
        _actions.Add(action);
        ActionAdded?.Invoke(action, _actions.Count - 1);
    }



    public bool TryInsertAction(IAppAction action, int index)
    {
        if (_actions.Count < index)
        {
            AppLog.WriteError("Could not insert action - index too high");
            return false;
        }

        if (index < 0)
        {
            AppLog.WriteError("Could not insert action - index too low");
            return false;
        }

        InsertAction(action, index);
        return true;
    }



    private void InsertAction(IAppAction action, int index)
    {
        _actions.Insert(index, action);
        ActionAdded?.Invoke(action, index);
    }



    public bool TryRemoveAction(IAppAction action)
    {
        if (!_actions.Contains(action))
        {
            AppLog.WriteError("Could not remove action - action not found in list");
            return false;
        }

        RemoveAction(action);
        return true;
    }



    private void RemoveAction(IAppAction action)
    {
        _actions.Remove(action);
        ActionRemoved?.Invoke(action);
    }



    // functional



    public void SafeExecuteAllAsync()
    {
        foreach (var action in _actions)
            _ = action.SafeExecuteAsync();
    }
}
