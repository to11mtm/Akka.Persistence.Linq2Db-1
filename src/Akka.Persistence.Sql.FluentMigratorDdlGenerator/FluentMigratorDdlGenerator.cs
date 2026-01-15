// -----------------------------------------------------------------------
//  <copyright file="FluentMigratorDdlGenerator.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;
using FluentMigrator.Expressions;
using FluentMigrator.Infrastructure;
using FluentMigrator.Model;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Generators.Generic;
using FluentMigrator.Runner.Generators.MySql;
using FluentMigrator.Runner.Generators.Postgres;
using FluentMigrator.Runner.Generators.SQLite;
using FluentMigrator.Runner.Generators.SqlServer;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;
using static Akka.Persistence.Sql.FluentMigratorDdlGenerator.TableSchemas;

namespace Akka.Persistence.Sql.FluentMigratorDdlGenerator;

/// <summary>
/// Specifies the database provider for DDL generation - so kawaii! ?
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql,
    Sqlite
}

/// <summary>
/// Specifies the table mapping mode for DDL generation, uwu~ ??
/// </summary>
public enum TableMappingMode
{
    /// <summary>
    /// Default mapping for fresh deployments - uses generic names! ??
    /// </summary>
    Default,

    /// <summary>
    /// Compatibility mapping for migrations from legacy plugins! ??
    /// </summary>
    Compat
}

/// <summary>
/// Generates DDL scripts using FluentMigrator's expression generators! ??
/// This is SO much cleaner than string manipulation, uwu~
/// </summary>
/// <remarks>
/// CopilotNotes: This class uses FluentMigrator's IMigrationGenerator implementations
/// to generate database-specific SQL from abstract table definitions.
/// The generators handle all the provider-specific syntax automatically! ?
/// </remarks>
public class FluentMigratorDdlGenerator
{
    private readonly string _outputPath;

    public FluentMigratorDdlGenerator(string outputPath)
    {
        _outputPath = outputPath;
    }

    /// <summary>
    /// Generates DDL for all providers and mapping modes! ??
    /// </summary>
    public async Task GenerateAll()
    {
        foreach (var mode in new[] { TableMappingMode.Default, TableMappingMode.Compat })
        {
            foreach (var provider in new[] { DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MySql, DatabaseProvider.Sqlite })
            {
                await GenerateForProvider(provider, mode);
            }
        }
    }

    /// <summary>
    /// Generates DDL for a specific provider and mapping mode! ??
    /// </summary>
    public async Task GenerateForProvider(DatabaseProvider provider, TableMappingMode mappingMode)
    {
        var mappingName = mappingMode == TableMappingMode.Default ? "default" : "compat";
        var providerName = provider.ToString().ToLowerInvariant();
        
        Console.WriteLine($"? Generating DDL for {provider} ({mappingName} mapping)...");

        var providerDir = Path.Combine(_outputPath, mappingName, providerName);
        Directory.CreateDirectory(providerDir);

        var generator = CreateGenerator(provider);
        var schemas = GetSchemas(provider, mappingMode);

        // Generate Journal DDL ??
        var journalSql = GenerateJournalDdl(generator, provider, schemas.Journal, mappingMode);
        await File.WriteAllTextAsync(Path.Combine(providerDir, "journal.sql"), journalSql);
        Console.WriteLine("  ? journal.sql");

        // Generate Tags DDL ???
        var tagsSql = GenerateTagsDdl(generator, provider, schemas.Tags, schemas.Journal.TableName, mappingMode);
        await File.WriteAllTextAsync(Path.Combine(providerDir, "journal-tags.sql"), tagsSql);
        Console.WriteLine("  ? journal-tags.sql");

        // Generate Snapshot DDL ??
        var snapshotSql = GenerateSnapshotDdl(generator, provider, schemas.Snapshot, mappingMode);
        await File.WriteAllTextAsync(Path.Combine(providerDir, "snapshot.sql"), snapshotSql);
        Console.WriteLine("  ? snapshot.sql");

        // Generate Metadata DDL ???
        var metadataSql = GenerateMetadataDdl(generator, provider, schemas.Metadata, mappingMode);
        await File.WriteAllTextAsync(Path.Combine(providerDir, "metadata.sql"), metadataSql);
        Console.WriteLine("  ? metadata.sql");

        Console.WriteLine($"? Completed {provider} ({mappingName}) uwu~\n");
    }

    /// <summary>
    /// Gets the generated DDL for a specific table without writing to file! ??
    /// Super useful for testing desu ne~ ?
    /// </summary>
    public string GenerateJournalDdl(DatabaseProvider provider, TableMappingMode mappingMode)
    {
        var generator = CreateGenerator(provider);
        var schemas = GetSchemas(provider, mappingMode);
        return GenerateJournalDdl(generator, provider, schemas.Journal, mappingMode);
    }

    public string GenerateTagsDdl(DatabaseProvider provider, TableMappingMode mappingMode)
    {
        var generator = CreateGenerator(provider);
        var schemas = GetSchemas(provider, mappingMode);
        return GenerateTagsDdl(generator, provider, schemas.Tags, schemas.Journal.TableName, mappingMode);
    }

    public string GenerateSnapshotDdl(DatabaseProvider provider, TableMappingMode mappingMode)
    {
        var generator = CreateGenerator(provider);
        var schemas = GetSchemas(provider, mappingMode);
        return GenerateSnapshotDdl(generator, provider, schemas.Snapshot, mappingMode);
    }

    public string GenerateMetadataDdl(DatabaseProvider provider, TableMappingMode mappingMode)
    {
        var generator = CreateGenerator(provider);
        var schemas = GetSchemas(provider, mappingMode);
        return GenerateMetadataDdl(generator, provider, schemas.Metadata, mappingMode);
    }

    private string GenerateJournalDdl(
        GenericGenerator generator,
        DatabaseProvider provider,
        JournalTableSchema schema,
        TableMappingMode mappingMode)
    {
        var sql = new StringBuilder();
        var tableMapping = GetTableMappingName(provider, mappingMode);

        sql.AppendLine("-- Journal Table DDL");
        sql.AppendLine($"-- Generated for {provider} (table-mapping = {tableMapping})");
        sql.AppendLine("-- This table stores all persisted events");
        if (mappingMode == TableMappingMode.Compat)
            sql.AppendLine($"-- Use this DDL when migrating from Akka.Persistence.{GetLegacyPluginName(provider)}");
        sql.AppendLine();

        // Create table expression using FluentMigrator
        var createTable = new CreateTableExpression
        {
            TableName = schema.TableName,
            SchemaName = string.IsNullOrEmpty(schema.SchemaName) ? null : schema.SchemaName
        };

        // Ordering column (identity/serial)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.OrderingColumn,
            Type = System.Data.DbType.Int64,
            IsIdentity = true,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // IsDeleted/deleted column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.DeletedColumn,
            Type = System.Data.DbType.Boolean,
            IsNullable = false,
            DefaultValue = false
        });

        // PersistenceId column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PersistenceIdColumn,
            Type = System.Data.DbType.String,
            Size = 255,
            IsNullable = false
        });

        // SequenceNr column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SequenceNumberColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false
        });

        // Timestamp column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.TimestampColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false
        });

        // Tags column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.TagsColumn,
            Type = System.Data.DbType.String,
            Size = int.MaxValue, // Max length for nvarchar(max)/text
            IsNullable = true
        });

        // Payload column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PayloadColumn,
            Type = System.Data.DbType.Binary,
            Size = int.MaxValue,
            IsNullable = false
        });

        // SerializerId column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SerializerIdColumn,
            Type = System.Data.DbType.Int32,
            IsNullable = true
        });

        // Manifest column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.ManifestColumn,
            Type = System.Data.DbType.String,
            Size = 500,
            IsNullable = true
        });

        // WriterUuid column (only for default mapping)
        if (schema.WriterUuidColumn != null)
        {
            createTable.Columns.Add(new ColumnDefinition
            {
                Name = schema.WriterUuidColumn,
                Type = System.Data.DbType.String,
                Size = 128,
                IsNullable = true
            });
        }

        // Generate the CREATE TABLE statement
        sql.AppendLine(generator.Generate(createTable));
        sql.AppendLine();

        // Generate additional constraints and indexes
        sql.AppendLine("-- Additional constraints and indexes:");
        sql.AppendLine();

        // Unique constraint on (PersistenceId, SequenceNr)
        var uniqueConstraint = new CreateConstraintExpression(ConstraintType.Unique)
        {
            Constraint = new ConstraintDefinition(ConstraintType.Unique)
            {
                TableName = schema.TableName,
                SchemaName = string.IsNullOrEmpty(schema.SchemaName) ? null : schema.SchemaName,
                ConstraintName = GetConstraintName(schema.TableName, "uq", provider),
                Columns = new[] { schema.PersistenceIdColumn, schema.SequenceNumberColumn }
            }
        };
        sql.AppendLine(WrapInIfNotExists(generator.Generate(uniqueConstraint), schema.TableName, schema.SchemaName, GetConstraintName(schema.TableName, "uq", provider), "constraint", provider));
        sql.AppendLine();

        // Index on Ordering
        sql.AppendLine(GenerateIndex(generator, provider, schema.TableName, schema.SchemaName,
            GetIndexName(schema.TableName, schema.OrderingColumn, provider), schema.OrderingColumn));

        // Index on Timestamp
        sql.AppendLine(GenerateIndex(generator, provider, schema.TableName, schema.SchemaName,
            GetIndexName(schema.TableName, schema.TimestampColumn, provider), schema.TimestampColumn));

        // Index on PersistenceId
        sql.AppendLine(GenerateIndex(generator, provider, schema.TableName, schema.SchemaName,
            GetIndexName(schema.TableName, schema.PersistenceIdColumn, provider), schema.PersistenceIdColumn));

        return sql.ToString();
    }

    private string GenerateTagsDdl(
        GenericGenerator generator,
        DatabaseProvider provider,
        TagTableSchema schema,
        string journalTableName,
        TableMappingMode mappingMode)
    {
        var sql = new StringBuilder();
        var tableMapping = GetTableMappingName(provider, mappingMode);

        sql.AppendLine("-- Journal Tags Table DDL");
        sql.AppendLine($"-- Generated for {provider} (table-mapping = {tableMapping})");
        sql.AppendLine("-- This table stores tags in normalized form (TagMode.TagTable)");
        sql.AppendLine();

        var createTable = new CreateTableExpression
        {
            TableName = schema.TableName,
            SchemaName = string.IsNullOrEmpty(schema.SchemaName) ? null : schema.SchemaName
        };

        // OrderingId column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.OrderingIdColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // Tag column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.TagColumn,
            Type = System.Data.DbType.String,
            Size = 64,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // SequenceNr column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SequenceNumberColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false
        });

        // PersistenceId column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PersistenceIdColumn,
            Type = System.Data.DbType.String,
            Size = 255,
            IsNullable = false
        });

        sql.AppendLine(generator.Generate(createTable));
        sql.AppendLine();

        sql.AppendLine("-- Additional constraints and indexes:");
        sql.AppendLine();

        // Index on (persistence_id, sequence_nr)
        sql.AppendLine(GenerateCompositeIndex(generator, provider, schema.TableName, schema.SchemaName,
            $"IX_{schema.TableName}_{schema.PersistenceIdColumn}_{schema.SequenceNumberColumn}",
            schema.PersistenceIdColumn, schema.SequenceNumberColumn));

        return sql.ToString();
    }

    private string GenerateSnapshotDdl(
        GenericGenerator generator,
        DatabaseProvider provider,
        SnapshotTableSchema schema,
        TableMappingMode mappingMode)
    {
        var sql = new StringBuilder();
        var tableMapping = GetTableMappingName(provider, mappingMode);

        sql.AppendLine("-- Snapshot Table DDL");
        sql.AppendLine($"-- Generated for {provider} (table-mapping = {tableMapping})");
        sql.AppendLine("-- This table stores actor state snapshots");
        sql.AppendLine();

        var createTable = new CreateTableExpression
        {
            TableName = schema.TableName,
            SchemaName = string.IsNullOrEmpty(schema.SchemaName) ? null : schema.SchemaName
        };

        // PersistenceId column (PK part 1)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PersistenceIdColumn,
            Type = System.Data.DbType.String,
            Size = 255,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // SequenceNr column (PK part 2)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SequenceNumberColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // Timestamp column (datetime2 for SQL Server compat, bigint otherwise)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.TimestampColumn,
            Type = schema.UseDateTimeForTimestamp ? System.Data.DbType.DateTime2 : System.Data.DbType.Int64,
            IsNullable = false
        });

        // Payload column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PayloadColumn,
            Type = System.Data.DbType.Binary,
            Size = int.MaxValue,
            IsNullable = true
        });

        // Manifest column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.ManifestColumn,
            Type = System.Data.DbType.String,
            Size = 500,
            IsNullable = true
        });

        // SerializerId column
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SerializerIdColumn,
            Type = System.Data.DbType.Int32,
            IsNullable = true
        });

        sql.AppendLine(generator.Generate(createTable));
        sql.AppendLine();

        sql.AppendLine("-- Additional constraints and indexes:");
        sql.AppendLine();

        // Index on SequenceNr
        sql.AppendLine(GenerateIndex(generator, provider, schema.TableName, schema.SchemaName,
            GetIndexName(schema.TableName, schema.SequenceNumberColumn, provider), schema.SequenceNumberColumn));

        // Index on Timestamp
        sql.AppendLine(GenerateIndex(generator, provider, schema.TableName, schema.SchemaName,
            GetIndexName(schema.TableName, schema.TimestampColumn, provider), schema.TimestampColumn));

        return sql.ToString();
    }

    private string GenerateMetadataDdl(
        GenericGenerator generator,
        DatabaseProvider provider,
        MetadataTableSchema schema,
        TableMappingMode mappingMode)
    {
        var sql = new StringBuilder();
        var tableMapping = GetTableMappingName(provider, mappingMode);

        sql.AppendLine("-- Journal Metadata Table DDL");
        sql.AppendLine($"-- Generated for {provider} (table-mapping = {tableMapping})");
        sql.AppendLine("-- This table is used for delete-compatibility-mode");
        sql.AppendLine();

        var createTable = new CreateTableExpression
        {
            TableName = schema.TableName,
            SchemaName = string.IsNullOrEmpty(schema.SchemaName) ? null : schema.SchemaName
        };

        // PersistenceId column (PK part 1)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.PersistenceIdColumn,
            Type = System.Data.DbType.String,
            Size = 255,
            IsNullable = false,
            IsPrimaryKey = true
        });

        // SequenceNr column (PK part 2)
        createTable.Columns.Add(new ColumnDefinition
        {
            Name = schema.SequenceNumberColumn,
            Type = System.Data.DbType.Int64,
            IsNullable = false,
            IsPrimaryKey = true
        });

        sql.AppendLine(generator.Generate(createTable));
        sql.AppendLine();

        return sql.ToString();
    }

    private string GenerateIndex(
        GenericGenerator generator,
        DatabaseProvider provider,
        string tableName,
        string? schemaName,
        string indexName,
        string columnName)
    {
        var createIndex = new CreateIndexExpression
        {
            Index = new IndexDefinition
            {
                Name = indexName,
                TableName = tableName,
                SchemaName = string.IsNullOrEmpty(schemaName) ? null : schemaName,
                Columns = new List<IndexColumnDefinition>
                {
                    new() { Name = columnName }
                }
            }
        };

        return WrapInIfNotExists(generator.Generate(createIndex), tableName, schemaName, indexName, "index", provider);
    }

    private string GenerateCompositeIndex(
        GenericGenerator generator,
        DatabaseProvider provider,
        string tableName,
        string? schemaName,
        string indexName,
        params string[] columnNames)
    {
        var createIndex = new CreateIndexExpression
        {
            Index = new IndexDefinition
            {
                Name = indexName,
                TableName = tableName,
                SchemaName = string.IsNullOrEmpty(schemaName) ? null : schemaName,
                Columns = columnNames.Select(c => new IndexColumnDefinition { Name = c }).ToList()
            }
        };

        return WrapInIfNotExists(generator.Generate(createIndex), tableName, schemaName, indexName, "index", provider);
    }

    private string WrapInIfNotExists(string sql, string tableName, string? schemaName, string objectName, string objectType, DatabaseProvider provider)
    {
        // FluentMigrator doesn't always generate IF NOT EXISTS, so we wrap it ourselves~
        return provider switch
        {
            DatabaseProvider.SqlServer => WrapSqlServerIfNotExists(sql, tableName, schemaName ?? "dbo", objectName, objectType),
            DatabaseProvider.PostgreSql => WrapPostgreSqlIfNotExists(sql, tableName, schemaName ?? "public", objectName, objectType),
            DatabaseProvider.MySql => sql, // MySQL CREATE INDEX IF NOT EXISTS is handled by generator
            DatabaseProvider.Sqlite => sql, // SQLite CREATE INDEX IF NOT EXISTS is handled by generator
            _ => sql
        };
    }

    private string WrapSqlServerIfNotExists(string sql, string tableName, string schemaName, string objectName, string objectType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("IF NOT EXISTS (");
        sb.AppendLine("    SELECT 1 FROM sys.indexes");
        sb.AppendLine("    WHERE");
        sb.AppendLine($"        object_id = OBJECT_ID('{schemaName}.{tableName}') AND");
        sb.AppendLine($"        name = '{objectName}'");
        sb.AppendLine(")");
        sb.AppendLine("BEGIN TRY");
        sb.AppendLine($"    {sql.Trim()}");
        sb.AppendLine("END TRY");
        sb.AppendLine("BEGIN CATCH");
        if (objectType == "index")
        {
            sb.AppendLine("    IF ERROR_NUMBER() = 1913 -- Error code for 'index already exists'");
            sb.AppendLine("    BEGIN");
            sb.AppendLine($"        PRINT 'Index {objectName} already exists, skipping.';");
            sb.AppendLine("    END");
        }
        else
        {
            sb.AppendLine("    IF ERROR_NUMBER() = 2714 -- Error code for 'constraint already exists'");
            sb.AppendLine("    BEGIN");
            sb.AppendLine($"        PRINT 'Constraint {objectName} already exists, skipping.';");
            sb.AppendLine("    END");
        }
        sb.AppendLine("    ELSE");
        sb.AppendLine("    BEGIN");
        sb.AppendLine("        THROW;");
        sb.AppendLine("    END");
        sb.AppendLine("END CATCH;");
        sb.AppendLine();
        return sb.ToString();
    }

    private string WrapPostgreSqlIfNotExists(string sql, string tableName, string schemaName, string objectName, string objectType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DO $BLOCK$");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    BEGIN");
        sb.AppendLine($"        {sql.Trim()}");
        sb.AppendLine("    EXCEPTION");
        sb.AppendLine("        WHEN duplicate_table OR duplicate_object");
        sb.AppendLine($"        THEN RAISE NOTICE '{objectType} \"{objectName}\" on \"{schemaName}\".\"{tableName}\" already exists, skipping';");
        sb.AppendLine("    END;");
        sb.AppendLine("END;");
        sb.AppendLine("$BLOCK$");
        sb.AppendLine();
        return sb.ToString();
    }

    private string GetIndexName(string tableName, string columnName, DatabaseProvider provider)
    {
        // Match existing naming conventions
        return provider switch
        {
            DatabaseProvider.SqlServer => $"IX_{tableName}_{columnName}",
            DatabaseProvider.PostgreSql => $"{tableName.ToLowerInvariant()}_{columnName.ToLowerInvariant()}_idx",
            DatabaseProvider.MySql => $"{tableName.ToLowerInvariant()}_{columnName.ToLowerInvariant()}_idx",
            DatabaseProvider.Sqlite => $"{tableName.ToLowerInvariant()}_{columnName.ToLowerInvariant()}_idx",
            _ => $"IX_{tableName}_{columnName}"
        };
    }

    private string GetConstraintName(string tableName, string suffix, DatabaseProvider provider)
    {
        // Match the naming convention from existing DDL files
        return provider switch
        {
            DatabaseProvider.SqlServer => $"UQ_{tableName}",
            DatabaseProvider.PostgreSql => $"{tableName.ToLowerInvariant()}_uq",
            DatabaseProvider.MySql => $"{tableName.ToLowerInvariant()}_uq",
            DatabaseProvider.Sqlite => $"{tableName.ToLowerInvariant()}_uq",
            _ => $"{tableName}_{suffix}"
        };
    }

    private string GetTableMappingName(DatabaseProvider provider, TableMappingMode mode)
    {
        if (mode == TableMappingMode.Default)
            return "default";

        return provider switch
        {
            DatabaseProvider.SqlServer => "sql-server",
            DatabaseProvider.PostgreSql => "postgresql",
            DatabaseProvider.MySql => "mysql",
            DatabaseProvider.Sqlite => "sqlite",
            _ => "default"
        };
    }

    private string GetLegacyPluginName(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => "SqlServer",
            DatabaseProvider.PostgreSql => "PostgreSql",
            DatabaseProvider.MySql => "MySql",
            DatabaseProvider.Sqlite => "Sqlite",
            _ => ""
        };
    }

    private (JournalTableSchema Journal, TagTableSchema Tags, SnapshotTableSchema Snapshot, MetadataTableSchema Metadata) GetSchemas(
        DatabaseProvider provider,
        TableMappingMode mode)
    {
        if (mode == TableMappingMode.Default)
        {
            // For default mode, adjust schema names per provider
            var journal = Default.Journal with
            {
                SchemaName = GetDefaultSchemaName(provider)
            };
            var tags = Default.Tags with
            {
                SchemaName = GetDefaultSchemaName(provider)
            };
            var snapshot = Default.Snapshot with
            {
                SchemaName = GetDefaultSchemaName(provider)
            };
            var metadata = Default.Metadata with
            {
                SchemaName = GetDefaultSchemaName(provider)
            };
            return (journal, tags, snapshot, metadata);
        }

        return provider switch
        {
            DatabaseProvider.SqlServer => (SqlServerCompat.Journal, SqlServerCompat.Tags, SqlServerCompat.Snapshot, SqlServerCompat.Metadata),
            DatabaseProvider.PostgreSql => (PostgreSqlCompat.Journal, PostgreSqlCompat.Tags, PostgreSqlCompat.Snapshot, PostgreSqlCompat.Metadata),
            DatabaseProvider.MySql => (MySqlCompat.Journal, MySqlCompat.Tags, MySqlCompat.Snapshot, MySqlCompat.Metadata),
            DatabaseProvider.Sqlite => (SqliteCompat.Journal, SqliteCompat.Tags, SqliteCompat.Snapshot, SqliteCompat.Metadata),
            _ => (Default.Journal, Default.Tags, Default.Snapshot, Default.Metadata)
        };
    }

    private string GetDefaultSchemaName(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => "dbo",
            DatabaseProvider.PostgreSql => "public",
            DatabaseProvider.MySql => "", // MySQL uses database, not schema
            DatabaseProvider.Sqlite => "", // SQLite doesn't have schemas
            _ => "dbo"
        };
    }

    /// <summary>
    /// Creates the appropriate FluentMigrator generator for the given database provider! 🏭
    /// </summary>
    /// <remarks>
    /// CopilotNotes: FluentMigrator registers specific generator types, not the abstract GenericGenerator.
    /// We need to get the concrete types: SqlServer2016Generator, PostgresGenerator, MySql8Generator, SQLiteGenerator
    /// </remarks>
    private GenericGenerator CreateGenerator(DatabaseProvider provider)
    {
        var services = new ServiceCollection();

        // Add FluentMigrator core services
        services.AddFluentMigratorCore();

        // Configure database based on provider
        switch (provider)
        {
            case DatabaseProvider.SqlServer:
                services.ConfigureRunner(rb => rb.AddSqlServer2016());
                break;
            case DatabaseProvider.PostgreSql:
                services.ConfigureRunner(rb => rb.AddPostgres());
                break;
            case DatabaseProvider.MySql:
                services.ConfigureRunner(rb => rb.AddMySql8());
                break;
            case DatabaseProvider.Sqlite:
                services.ConfigureRunner(rb => rb.AddSQLite());
                break;
        }

        var serviceProvider = services.BuildServiceProvider();

        // Get the specific generator type for each provider ✨
        return provider switch
        {
            DatabaseProvider.SqlServer => serviceProvider.GetRequiredService<SqlServer2016Generator>(),
            DatabaseProvider.PostgreSql => serviceProvider.GetRequiredService<PostgresGenerator>(),
            DatabaseProvider.MySql => serviceProvider.GetRequiredService<MySql8Generator>(),
            DatabaseProvider.Sqlite => serviceProvider.GetRequiredService<SQLiteGenerator>(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider")
        };
    }
}
