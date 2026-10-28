using Avalonia;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// a drop target for blocks, set IsCondition for slots that take conditions
// (drag events bubble up from the inner border, so the owning view handles them on this control)
public partial class DropZone : ViewBase
{
    public static readonly StyledProperty<bool> IsConditionProperty =
        AvaloniaProperty.Register<DropZone, bool>(nameof(IsCondition));



    public bool IsCondition
    {
        get => GetValue(IsConditionProperty);
        set => SetValue(IsConditionProperty, value);
    }



    public DropZone()
    {
        InitializeComponent();
    }



    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsConditionProperty) return;

        BlockZone.IsVisible = !IsCondition;
        ConditionZone.IsVisible = IsCondition;
    }
}
