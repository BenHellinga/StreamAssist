using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.AppAction.Model;



public class ActionGroupModel
{
    // private storage fields
    private List<IAppAction> _actions;

    // public getter properties
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



    public event Action<IAppAction>? ActionAdded;
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
        ActionAdded?.Invoke(action);
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
