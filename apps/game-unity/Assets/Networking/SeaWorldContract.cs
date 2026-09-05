#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace Sea.Client
{
    /// <summary>
    /// The public world schema exactly as the generated client bindings describe it: every
    /// subscribable table, event tables included, with its SQL column names. Subscription queries are checked against it
    /// so a renamed table or column fails a test instead of silently yielding an empty
    /// subscription at runtime. Nothing here is hand-listed; regenerating the bindings is the
    /// only way the contract changes.
    /// </summary>
    public static class SeaWorldContract
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex FromClause = new Regex(
            @"\bFROM\s+(?<table>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            MatchTimeout);

        private static readonly Regex ComparedIdentifier = new Regex(
            @"\b(?<column>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<=|>=|=|<|>)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            MatchTimeout);

        private static readonly Lazy<Dictionary<string, IReadOnlyCollection<string>>> tables =
            new Lazy<Dictionary<string, IReadOnlyCollection<string>>>(ReadTables);

        /// <summary>Public table name to the set of SQL column names the module publishes.</summary>
        public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Tables => tables.Value;

        /// <summary>
        /// Every way one subscription query disagrees with the contract: an unknown table, or a
        /// compared column the table does not publish. Empty when the query is sound.
        /// </summary>
        public static IReadOnlyList<string> Violations(string query)
        {
            var from = FromClause.Match(query);
            if (!from.Success)
            {
                return new[] { $"No FROM clause: {query}" };
            }

            var table = from.Groups["table"].Value;
            if (!Tables.TryGetValue(table, out var columns))
            {
                return new[] { $"Unknown table '{table}': {query}" };
            }

            var predicate = query.Substring(from.Index + from.Length);
            return ComparedIdentifier.Matches(predicate)
                .Cast<Match>()
                .Select(match => match.Groups["column"].Value)
                .Where(column => !columns.Contains(column, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Select(column => $"Unknown column '{table}.{column}': {query}")
                .ToList();
        }

        /// <summary>Throws with every violation listed when any query breaks the contract.</summary>
        public static void Require(IEnumerable<string> queries)
        {
            var violations = queries.SelectMany(Violations).ToList();
            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    "Subscription queries violate the world contract:\n" + string.Join("\n", violations));
            }
        }

        private static Dictionary<string, IReadOnlyCollection<string>> ReadTables()
        {
            // The connection constructor is offline; it only builds the table handles we reflect over.
            var db = new DbConnection().Db;
            var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            foreach (var field in typeof(RemoteTables).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var rowType = HandleRowType(field.FieldType);
                if (rowType == null)
                {
                    continue;
                }

                var handle = field.GetValue(db);
                var nameProperty = field.FieldType.GetProperty(nameof(RemoteTableHandleBase<EventContext, Ship>.RemoteTableName));
                if (handle == null || nameProperty?.GetValue(handle) is not string name)
                {
                    throw new InvalidOperationException($"{field.Name} is not a named remote table handle.");
                }

                result[name] = SqlColumns(rowType);
            }

            return result;
        }

        private static Type? HandleRowType(Type handleType)
        {
            for (var type = handleType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RemoteTableHandleBase<,>))
                {
                    return type.GetGenericArguments()[1];
                }
            }

            return null;
        }

        private static HashSet<string> SqlColumns(Type rowType)
        {
            return rowType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.GetCustomAttribute<DataMemberAttribute>()?.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
