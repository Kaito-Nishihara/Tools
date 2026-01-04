using Dapper;
using Microsoft.Data.SqlClient;

namespace DbDocs;

public sealed class DbIntrospector
{
    private readonly string _connectionString;

    public DbIntrospector(string connectionString)
        => _connectionString = connectionString;

    public async Task<DatabaseSpec> ReadAsync(string title, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var connSummary = await GetConnectionSummaryAsync(conn, ct);

        var tables = (await conn.QueryAsync<TableRow>(new CommandDefinition(SqlTables, cancellationToken: ct))).ToList();
        if (tables.Count == 0)
            throw new InvalidOperationException("No tables found. Check connection string / permissions.");

        var cols = (await conn.QueryAsync<ColumnRow>(new CommandDefinition(SqlColumns, cancellationToken: ct))).ToList();
        var idx = (await conn.QueryAsync<IndexRow>(new CommandDefinition(SqlIndexes, cancellationToken: ct))).ToList();
        var fks = (await conn.QueryAsync<FkRow>(new CommandDefinition(SqlFks, cancellationToken: ct))).ToList();

        // schema.table -> grouped rows
        var tableMap = new Dictionary<string, TableBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tables)
        {
            var key = $"{t.SchemaName}.{t.TableName}";
            tableMap[key] = new TableBucket(t);
        }

        foreach (var c in cols)
        {
            var key = $"{c.SchemaName}.{c.TableName}";
            if (tableMap.TryGetValue(key, out var b))
                b.Columns.Add(c);
        }

        foreach (var i in idx)
        {
            var key = $"{i.SchemaName}.{i.TableName}";
            if (tableMap.TryGetValue(key, out var b))
                b.Indexes.Add(i);
        }

        foreach (var fk in fks)
        {
            var key = $"{fk.ParentSchema}.{fk.ParentTable}";
            if (tableMap.TryGetValue(key, out var b))
                b.ForeignKeys.Add(fk);
        }

        var tableSpecs = new List<TableSpec>(tableMap.Count);

        foreach (var kv in tableMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var bucket = kv.Value;
            var t = bucket.Table;

            var columns = bucket.Columns
                .OrderBy(x => x.ColumnId)
                .Select(r => new ColumnSpec(
                    Ordinal: r.ColumnId,
                    Name: r.ColumnName,
                    SqlType: FormatSqlType(r),
                    IsNullable: r.IsNullable,
                    IsPrimaryKey: r.IsPk,
                    DefaultDefinition: NullIfEmpty(r.DefaultDefinition),
                    IsIdentity: r.SeedValue.HasValue,
                    ComputedDefinition: NullIfEmpty(r.ComputedDefinition),
                    Description: NullIfEmpty(r.Description)
                ))
                .ToList();

            var indexes = bucket.Indexes
                .GroupBy(x => x.IndexName, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var any = g.First();

                    var keyCols = g.Where(x => !x.IsIncludedColumn)
                        .OrderBy(x => x.KeyOrdinal)
                        .Select(x => new IndexColumnSpec(x.ColumnName, x.IsDescendingKey))
                        .ToList();

                    var includes = g.Where(x => x.IsIncludedColumn)
                        .Select(x => x.ColumnName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new IndexSpec(
                        Name: any.IndexName,
                        TypeDesc: any.TypeDesc,
                        IsUnique: any.IsUnique,
                        IsPrimaryKey: any.IsPrimaryKey,
                        KeyColumns: keyCols,
                        IncludeColumns: includes
                    );
                })
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var foreignKeys = bucket.ForeignKeys
                .OrderBy(x => x.FkName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ConstraintColumnId)
                .Select(x => new ForeignKeySpec(
                    Name: x.FkName,
                    FromSchema: x.ParentSchema,
                    FromTable: x.ParentTable,
                    FromColumn: x.ParentColumn,
                    ToSchema: x.RefSchema,
                    ToTable: x.RefTable,
                    ToColumn: x.RefColumn,
                    OnDelete: x.OnDelete,
                    OnUpdate: x.OnUpdate
                ))
                .ToList();

            tableSpecs.Add(new TableSpec(
                Schema: t.SchemaName,
                Name: t.TableName,
                Description: NullIfEmpty(t.Description),
                Columns: columns,
                Indexes: indexes,
                ForeignKeys: foreignKeys
            ));
        }

        return new DatabaseSpec(
            Title: title,
            GeneratedAt: DateTimeOffset.Now,
            ConnectionSummary: connSummary,
            Tables: tableSpecs
        );
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static string FormatSqlType(ColumnRow r)
    {
        var t = r.TypeName;

        if (t is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary")
        {
            var len = r.MaxLength;

            // nvarchar/nchar はバイト長が返るので /2
            if (t.StartsWith("n", StringComparison.OrdinalIgnoreCase) && len > 0)
                len /= 2;

            if (len == -1) return $"{t}(max)";
            return $"{t}({len})";
        }

        if (t is "decimal" or "numeric")
            return $"{t}({r.Precision},{r.Scale})";

        if (t is "datetime2" or "time")
            return $"{t}({r.Scale})";

        return t;
    }

    private static async Task<string> GetConnectionSummaryAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = @"
SELECT
  DB_NAME() AS DbName,
  @@SERVERNAME AS ServerName,
  CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS Version;";
        var row = await conn.QuerySingleAsync<ConnSummaryRow>(new CommandDefinition(sql, cancellationToken: ct));
        return $"{row.ServerName} / {row.DbName} (SQL Server {row.Version})";
    }

    // -----------------------
    // SQL (型を C# に寄せる)
    // -----------------------

    private const string SqlTables = @"
SELECT
  s.name AS SchemaName,
  t.name AS TableName,
  CAST(ep.value AS nvarchar(max)) AS Description
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.extended_properties ep
  ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
ORDER BY s.name, t.name;";

    private const string SqlColumns = @"
;WITH pk_cols AS (
  SELECT ic.object_id, ic.column_id
  FROM sys.indexes i
  JOIN sys.index_columns ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  WHERE i.is_primary_key = 1
)
SELECT
  s.name AS SchemaName,
  t.name AS TableName,
  c.column_id AS ColumnId,
  c.name AS ColumnName,
  ty.name AS TypeName,
  CAST(c.max_length AS int) AS MaxLength,
  c.precision AS Precision,
  c.scale AS Scale,
  CAST(c.is_nullable AS bit) AS IsNullable,
  dc.definition AS DefaultDefinition,
  CAST(ic.seed_value AS decimal(38,0)) AS SeedValue,
  CAST(ic.increment_value AS decimal(38,0)) AS IncrementValue,
  cc.definition AS ComputedDefinition,
  CASE WHEN pk.column_id IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS IsPk,
  CAST(ep.value AS nvarchar(max)) AS Description
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc
  ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.identity_columns ic
  ON ic.object_id = t.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns cc
  ON cc.object_id = t.object_id AND cc.column_id = c.column_id
LEFT JOIN pk_cols pk
  ON pk.object_id = t.object_id AND pk.column_id = c.column_id
LEFT JOIN sys.extended_properties ep
  ON ep.major_id = t.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
ORDER BY s.name, t.name, c.column_id;";

    private const string SqlIndexes = @"
SELECT
  s.name AS SchemaName,
  t.name AS TableName,
  i.name AS IndexName,
  i.type_desc AS TypeDesc,
  CAST(i.is_unique AS bit) AS IsUnique,
  CAST(i.is_primary_key AS bit) AS IsPrimaryKey,
  ic.key_ordinal AS KeyOrdinal,
  c.name AS ColumnName,
  CAST(ic.is_descending_key AS bit) AS IsDescendingKey,
  CAST(ic.is_included_column AS bit) AS IsIncludedColumn
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE i.type_desc <> 'HEAP'
ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal;";

    private const string SqlFks = @"
SELECT
  ps.name AS ParentSchema,
  pt.name AS ParentTable,
  pc.name AS ParentColumn,
  rs.name AS RefSchema,
  rt.name AS RefTable,
  rc.name AS RefColumn,
  fk.name AS FkName,
  fk.delete_referential_action_desc AS OnDelete,
  fk.update_referential_action_desc AS OnUpdate,
  fkc.constraint_column_id AS ConstraintColumnId
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;";

    // -----------------------
    // DTO (Dapper friendly)
    // -----------------------

    private sealed class ConnSummaryRow
    {
        public string DbName { get; set; } = default!;
        public string ServerName { get; set; } = default!;
        public string Version { get; set; } = default!;
    }

    private sealed class TableRow
    {
        public string SchemaName { get; set; } = default!;
        public string TableName { get; set; } = default!;
        public string? Description { get; set; }
    }

    private sealed class ColumnRow
    {
        public string SchemaName { get; set; } = default!;
        public string TableName { get; set; } = default!;
        public int ColumnId { get; set; }
        public string ColumnName { get; set; } = default!;
        public string TypeName { get; set; } = default!;
        public int MaxLength { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultDefinition { get; set; }
        public decimal? SeedValue { get; set; }
        public decimal? IncrementValue { get; set; }
        public string? ComputedDefinition { get; set; }
        public bool IsPk { get; set; }
        public string? Description { get; set; }
    }

    private sealed class IndexRow
    {
        public string SchemaName { get; set; } = default!;
        public string TableName { get; set; } = default!;
        public string IndexName { get; set; } = default!;
        public string TypeDesc { get; set; } = default!;
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int KeyOrdinal { get; set; }
        public string ColumnName { get; set; } = default!;
        public bool IsDescendingKey { get; set; }
        public bool IsIncludedColumn { get; set; }
    }

    private sealed class FkRow
    {
        public string ParentSchema { get; set; } = default!;
        public string ParentTable { get; set; } = default!;
        public string ParentColumn { get; set; } = default!;
        public string RefSchema { get; set; } = default!;
        public string RefTable { get; set; } = default!;
        public string RefColumn { get; set; } = default!;
        public string FkName { get; set; } = default!;
        public string OnDelete { get; set; } = default!;
        public string OnUpdate { get; set; } = default!;
        public int ConstraintColumnId { get; set; }
    }

    private sealed class TableBucket
    {
        public TableBucket(TableRow table) => Table = table;

        public TableRow Table { get; }
        public List<ColumnRow> Columns { get; } = new();
        public List<IndexRow> Indexes { get; } = new();
        public List<FkRow> ForeignKeys { get; } = new();
    }
}
