using SpacetimeDB;

namespace Sea.Server;

[SpacetimeDB.Type]
public partial struct NavigationBlockerState
{
    public float X;
    public float Y;
    public float Radius;
}
