using System;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.AppAction.Background;



public class ActionGroupChangeManager : ManagerBase
{
    // private



    private ActionGroupModel _group;



    // events



    public event Action? GroupChanged;



    // constructor



    public ActionGroupChangeManager(ActionGroupModel group)
    {
        _group = group;
        SubscribeToActionGroupModel(_group);
    }



    // model events



    private void OnActionAdded(ActionModel action, int _)
    {
        SubscribeToActionModel(action);
        GroupChanged?.Invoke();
    }



    private void OnActionRemoved(ActionModel action)
    {
        UnsubscribeFromActionModel(action);
        GroupChanged?.Invoke();
    }



    private void OnConfigurationChanged() => GroupChanged?.Invoke();



    private void OnIsDisabledChanged(bool _) => GroupChanged?.Invoke();



    // helpers



    private void SubscribeToActionGroupModel(ActionGroupModel group)
    {
        group.ActionAdded += OnActionAdded;
        group.ActionRemoved += OnActionRemoved;

        foreach (var action in group.Actions)
            SubscribeToActionModel(action);
    }



    private void UnsubscribeFromActionGroupModel(ActionGroupModel group)
    {
        group.ActionAdded -= OnActionAdded;
        group.ActionRemoved -= OnActionRemoved;

        foreach (var action in group.Actions)
            UnsubscribeFromActionModel(action);
    }



    private void SubscribeToActionModel(ActionModel action)
    {
        action.ConfigurationChanged += OnConfigurationChanged;
        action.IsDisabledChanged += OnIsDisabledChanged;
    }



    private void UnsubscribeFromActionModel(ActionModel action)
    {
        action.ConfigurationChanged -= OnConfigurationChanged;
        action.IsDisabledChanged -= OnIsDisabledChanged;
    }




    // disposal



    public override void Dispose() => UnsubscribeFromActionGroupModel(_group);
}