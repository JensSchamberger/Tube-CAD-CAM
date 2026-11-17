namespace TubeCadCam.Presentation;

public sealed class ParameterViewModel(string name, double value, Action changed) : ViewModelBase
{
    private double _value = value;

    public string Name { get; } = name;

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
