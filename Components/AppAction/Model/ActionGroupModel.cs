using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Core;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.AppAction.Model;



public class ActionGroupModel : ModelBase
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



    public void RemoveAction(ActionModel action)
    {
        if (!_actions.Remove(action)) return;

        if (_actions.Count == 0)
            _tryUnbindSelf?.Invoke();
        ActionRemoved?.Invoke(action);
        action.Dispose();
    }



    public void AttachUnbindSelf(Action? tryUnbindSelf) => _tryUnbindSelf = tryUnbindSelf;



    // functional



    public async Task SafeExecuteAllAsync()
    {
        foreach (var action in _actions)
            await action.SafeExecuteAsync();
    }



    // disposal



    public override void Dispose()
    {
        foreach (var action in _actions)
            action.Dispose();
    }
}
