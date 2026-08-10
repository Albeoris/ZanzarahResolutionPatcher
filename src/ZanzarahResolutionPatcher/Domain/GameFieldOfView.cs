namespace ZanzarahResolutionPatcher.Domain;

public readonly record struct GameFieldOfView(int Horizontal, int Vertical)
{
    public override string ToString() => $"fov {Horizontal},{Vertical}";
}
