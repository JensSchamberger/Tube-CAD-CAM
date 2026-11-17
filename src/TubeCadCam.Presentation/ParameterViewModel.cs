namespace TubeCadCam.Presentation;

public sealed class ParameterViewModel(string name, double value, Action changed) : ViewModelBase
{
    private double _value = value;

    public string Name { get; } = name;

    public string Label => ParameterLabels.Label(Name);

    /// <summary>Sets the value without raising the change callback (used when loading whole documents).</summary>
    public void SetSilently(double value)
    {
        _value = value;
        OnPropertyChanged(nameof(Value));
    }

    public double Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
            {
                changed();
            }
        }
    }
}
