using System.Text;

namespace DbDocs;

public sealed record DatabaseSpec(
    string Title,
    DateTimeOffset GeneratedAt,
    string ConnectionSummary,
    IReadOnlyList<TableSpec> Tables);

public sealed record TableSpec(
    string Schema,
    string Name,
    string? Description,
    IReadOnlyList<ColumnSpec> Columns,
    IReadOnlyList<IndexSpec> Indexes,
    IReadOnlyList<ForeignKeySpec> ForeignKeys)
{
    public string FullName => $"{Schema}.{Name}";
    public string SafeFileName => $"{Schema}.{Name}".Replace('/', '_').Replace('\\', '_');
}

public sealed record ColumnSpec(
    int Ordinal,
    string Name,
    string SqlType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? DefaultDefinition,
    bool IsIdentity,
    string? ComputedDefinition,
    string? Description);

public sealed record IndexSpec(
    string Name,
    string TypeDesc,
    bool IsUnique,
    bool IsPrimaryKey,
    IReadOnlyList<IndexColumnSpec> KeyColumns,
    IReadOnlyList<string> IncludeColumns);

public sealed record IndexColumnSpec(string Name, bool Desc);

public sealed record ForeignKeySpec(
    string Name,
    string FromSchema,
    string FromTable,
    string FromColumn,
    string ToSchema,
    string ToTable,
    string ToColumn,
    string OnDelete,
    string OnUpdate);
