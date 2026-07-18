using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.AppAction.Model;



public class ActionGroupModel
{
    // private



    private List<ActionModel> _actions;
    private Action? _tryUnbindSelf;



    // public


    
    [JsonIgnore] public IReadOnlyList<ActionModel> Actions => _actions; 



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



    public event Action<ActionModel, int>? ActionAdded;
    public event Action<ActionModel>? ActionRemoved;
    public event Action? FirstActionAdded;



    // constructor



    public ActionGroupModel()
    {
        _actions = [];
    }



    // action management


    
    public void AddAction(ActionModel action) => InsertAction(action, _actions.Count);



    public bool TryInsertAction(ActionModel action, int index)
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



    private void InsertAction(ActionModel action, int index)
    {
        _actions.Insert(index, action);

        ActionAdded?.Invoke(action, index);
        if (_actions.Count == 1)
            FirstActionAdded?.Invoke();
    }



    public bool TryRemoveAction(ActionModel action)
    {
        if (!_actions.Contains(action))
        {
            AppLog.WriteError("Could not remove action - action not found in list");
            return false;
        }

        RemoveAction(action);
        return true;
    }



    private void RemoveAction(ActionModel action)
    {
        _actions.Remove(action);

        if (_actions.Count == 0)
            _tryUnbindSelf?.Invoke();
        ActionRemoved?.Invoke(action);
    }



    public void SetUnbindSelfLambda(Action? tryUnbindSelf) => _tryUnbindSelf = tryUnbindSelf;



    // functional



    public void SafeExecuteAllAsync()
    {
        foreach (var action in _actions)
            _ = action.SafeExecuteAsync();
    }
}
