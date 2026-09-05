using System.Globalization;

namespace Sea.Server;

/// <summary>
/// Dense code-indexed views over a <see cref="GameContent"/> catalog. Built once at module load so
/// hot paths resolve content by its byte code with an array index instead of a table lookup.
/// </summary>
public static class ContentIndex
{
    /// <summary>Every code enum is <c>: byte</c>, so one slot per possible value covers them all.</summary>
    public const int CodeSlots = 256;

    public static AmmunitionContent?[] AmmunitionByCode(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ByCode(content.Ammunition, entry => entry.Code, entry => entry.Id, "Ammunition");
    }

    public static NpcContent?[] NpcByArchetypeCode(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ByCode(content.Npcs, entry => entry.Code, entry => entry.Id, "Npc");
    }

    /// <summary>
    /// The tier table applied to every enemy in the catalog, laid out beside
    /// <see cref="NpcByArchetypeCode"/> so a decision reads an enemy's numbers by its own code
    /// rather than deriving them again every time it thinks.
    /// </summary>
    public static NpcStatLine[] NpcStatsByArchetypeCode(GameContent content, BaseShipProfile baseShip)
    {
        ArgumentNullException.ThrowIfNull(content);
        var slots = new NpcStatLine[CodeSlots];
        for (var index = 0; index < content.Npcs.Count; index++)
        {
            var npc = content.Npcs[index];
            slots[(byte)npc.Code] = NpcDerivation.Derive(
                npc.Tier,
                npc.MapId,
                baseShip,
                content.StatCaps);
        }

        return slots;
    }

    /// <summary>
    /// Builds a <see cref="Dictionary{TKey,TValue}"/> keyed by id. Like <see cref="ByCode{T,TCode}"/>
    /// below, the delegate only runs while the module loads, never on a reducer or tick path.
    /// </summary>
    public static IReadOnlyDictionary<string, T> ById<T>(IReadOnlyList<T> entries, Func<T, string> id, string family)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(id);
        var map = new Dictionary<string, T>(entries.Count, StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var key = id(entry);
            if (!map.TryAdd(key, entry))
            {
                throw new InvalidOperationException($"{family} id '{key}' is declared twice.");
            }
        }

        return map;
    }

    /// <summary>
    /// Builds one dense slot array. The delegates and the boxing conversion below only run while the
    /// module loads, never on a reducer or tick path.
    /// </summary>
    private static T?[] ByCode<T, TCode>(
        IReadOnlyList<T> entries,
        Func<T, TCode> code,
        Func<T, string> id,
        string family)
        where T : class
        where TCode : struct, Enum
    {
        var slots = new T?[CodeSlots];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var value = code(entry);
            var slot = Convert.ToByte(value, CultureInfo.InvariantCulture);
            if (slots[slot] is T taken)
            {
                throw new InvalidOperationException(
                    $"{family} code '{value}' is claimed by both '{id(taken)}' and '{id(entry)}'.");
            }

            slots[slot] = entry;
        }

        return slots;
    }
}
