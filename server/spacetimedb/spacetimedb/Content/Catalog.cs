using Sea.Server;

/// <summary>
/// The module's single source of content. The generated catalog is constant data compiled into the
/// wasm module, so building it once in a static initializer is deterministic and allocation-free
/// afterwards. Server code reads content from here; the content tables exist only as the client
/// projection.
/// </summary>
internal static class Catalog
{
    private const string StarterHullId = "hull_t1";
    private const string StarterCannonId = "cannon_t1";

    public static readonly GameContent Content = ContentCatalog.CreateDefault();

    public static readonly AmmunitionContent?[] AmmunitionByCode =
        ContentIndex.AmmunitionByCode(Content);

    public static readonly AbilityContent?[] AbilityByCode =
        ContentIndex.AbilityByCode(Content);

    public static readonly NpcContent?[] NpcByArchetypeCode =
        ContentIndex.NpcByArchetypeCode(Content);

    public static readonly IReadOnlyDictionary<string, HullContent> HullById =
        ContentIndex.ById(Content.Hulls, hull => hull.Id, "Hull");

    public static readonly IReadOnlyDictionary<string, CannonContent> CannonById =
        ContentIndex.ById(Content.Cannons, cannon => cannon.Id, "Cannon");

    // Resolved from the maps above; static field order is the dependency order.
    public static readonly HullContent StarterHull = HullById[StarterHullId];

    public static readonly CannonContent StarterCannon = CannonById[StarterCannonId];

    public static readonly AmmunitionContent BaselineAmmunition =
        AmmunitionByCode[(byte)AmmunitionCode.Round] ??
        throw new InvalidOperationException("Round Shot ammunition is missing.");
}
