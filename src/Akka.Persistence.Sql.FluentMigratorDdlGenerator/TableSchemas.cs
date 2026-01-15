// -----------------------------------------------------------------------
//  <copyright file="TableSchemas.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.FluentMigratorDdlGenerator;

/// <summary>
/// Kawaii table schema definitions for Akka.Persistence.Sql! ✨
/// These define the structure of our persistence tables, uwu~
/// </summary>
/// <remarks>
/// CopilotNotes: This file contains the shared table schema definitions
/// that are used by both the migration generators and the DDL output.
/// Column types are defined abstractly and translated by FluentMigrator
/// to provider-specific types automatically! So sugoi! 💖
/// </remarks>
public static class TableSchemas
{
    /// <summary>
    /// Schema definition for the Journal table - stores all persisted events! 📚
    /// </summary>
    public record JournalTableSchema(
        string TableName,
        string SchemaName,
        string OrderingColumn,
        string DeletedColumn,
        string PersistenceIdColumn,
        string SequenceNumberColumn,
        string TimestampColumn,
        string TagsColumn,
        string PayloadColumn,
        string SerializerIdColumn,
        string ManifestColumn,
        string? WriterUuidColumn = null);

    /// <summary>
    /// Schema definition for the Tag table - normalized tag storage! 🏷️
    /// </summary>
    public record TagTableSchema(
        string TableName,
        string SchemaName,
        string OrderingIdColumn,
        string TagColumn,
        string SequenceNumberColumn,
        string PersistenceIdColumn);

    /// <summary>
    /// Schema definition for the Snapshot table - actor state snapshots! 📸
    /// </summary>
    public record SnapshotTableSchema(
        string TableName,
        string SchemaName,
        string PersistenceIdColumn,
        string SequenceNumberColumn,
        string TimestampColumn,
        string PayloadColumn,
        string ManifestColumn,
        string SerializerIdColumn,
        bool UseDateTimeForTimestamp = false);

    /// <summary>
    /// Schema definition for the Metadata table - delete compatibility mode! 🗑️
    /// </summary>
    public record MetadataTableSchema(
        string TableName,
        string SchemaName,
        string PersistenceIdColumn,
        string SequenceNumberColumn);

    /// <summary>
    /// Gets the default table schemas - used for fresh deployments! 🌸
    /// All snake_case column names, generic table names~
    /// </summary>
    public static class Default
    {
        public static JournalTableSchema Journal => new(
            TableName: "journal",
            SchemaName: "dbo",
            OrderingColumn: "ordering",
            DeletedColumn: "deleted",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_number",
            TimestampColumn: "created",
            TagsColumn: "tags",
            PayloadColumn: "message",
            SerializerIdColumn: "identifier",
            ManifestColumn: "manifest",
            WriterUuidColumn: "writer_uuid");

        public static TagTableSchema Tags => new(
            TableName: "tags",
            SchemaName: "dbo",
            OrderingIdColumn: "ordering_id",
            TagColumn: "tag",
            SequenceNumberColumn: "sequence_nr",
            PersistenceIdColumn: "persistence_id");

        public static SnapshotTableSchema Snapshot => new(
            TableName: "snapshot",
            SchemaName: "dbo",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_number",
            TimestampColumn: "created",
            PayloadColumn: "snapshot",
            ManifestColumn: "manifest",
            SerializerIdColumn: "identifier");

        public static MetadataTableSchema Metadata => new(
            TableName: "metadata",
            SchemaName: "dbo",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_number");
    }

    /// <summary>
    /// SQL Server compatibility schemas - for migration from Akka.Persistence.SqlServer! 💙
    /// </summary>
    public static class SqlServerCompat
    {
        public static JournalTableSchema Journal => new(
            TableName: "EventJournal",
            SchemaName: "dbo",
            OrderingColumn: "Ordering",
            DeletedColumn: "IsDeleted",
            PersistenceIdColumn: "PersistenceId",
            SequenceNumberColumn: "SequenceNr",
            TimestampColumn: "Timestamp",
            TagsColumn: "Tags",
            PayloadColumn: "Payload",
            SerializerIdColumn: "SerializerId",
            ManifestColumn: "Manifest");

        public static TagTableSchema Tags => new(
            TableName: "tags",
            SchemaName: "dbo",
            OrderingIdColumn: "ordering_id",
            TagColumn: "tag",
            SequenceNumberColumn: "sequence_nr",
            PersistenceIdColumn: "persistence_id");

        public static SnapshotTableSchema Snapshot => new(
            TableName: "SnapshotStore",
            SchemaName: "dbo",
            PersistenceIdColumn: "PersistenceId",
            SequenceNumberColumn: "SequenceNr",
            TimestampColumn: "Timestamp",
            PayloadColumn: "Snapshot",
            ManifestColumn: "Manifest",
            SerializerIdColumn: "SerializerId",
            UseDateTimeForTimestamp: true); // SQL Server uses datetime2!

        public static MetadataTableSchema Metadata => new(
            TableName: "Metadata",
            SchemaName: "dbo",
            PersistenceIdColumn: "PersistenceId",
            SequenceNumberColumn: "SequenceNr");
    }

    /// <summary>
    /// PostgreSQL compatibility schemas - for migration from Akka.Persistence.PostgreSql! 🐘
    /// </summary>
    public static class PostgreSqlCompat
    {
        public static JournalTableSchema Journal => new(
            TableName: "event_journal",
            SchemaName: "public",
            OrderingColumn: "ordering",
            DeletedColumn: "is_deleted",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "created_at",
            TagsColumn: "tags",
            PayloadColumn: "payload",
            SerializerIdColumn: "serializer_id",
            ManifestColumn: "manifest");

        public static TagTableSchema Tags => new(
            TableName: "tags",
            SchemaName: "public",
            OrderingIdColumn: "ordering_id",
            TagColumn: "tag",
            SequenceNumberColumn: "sequence_nr",
            PersistenceIdColumn: "persistence_id");

        public static SnapshotTableSchema Snapshot => new(
            TableName: "snapshot_store",
            SchemaName: "public",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "created_at",
            PayloadColumn: "payload",
            ManifestColumn: "manifest",
            SerializerIdColumn: "serializer_id");

        public static MetadataTableSchema Metadata => new(
            TableName: "metadata",
            SchemaName: "public",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr");
    }

    /// <summary>
    /// MySQL compatibility schemas - for migration from Akka.Persistence.MySql! 🐬
    /// </summary>
    public static class MySqlCompat
    {
        public static JournalTableSchema Journal => new(
            TableName: "event_journal",
            SchemaName: "", // MySQL doesn't use schemas the same way
            OrderingColumn: "ordering",
            DeletedColumn: "is_deleted",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "created_at",
            TagsColumn: "tags",
            PayloadColumn: "payload",
            SerializerIdColumn: "serializer_id",
            ManifestColumn: "manifest");

        public static TagTableSchema Tags => new(
            TableName: "tags",
            SchemaName: "",
            OrderingIdColumn: "ordering_id",
            TagColumn: "tag",
            SequenceNumberColumn: "sequence_nr",
            PersistenceIdColumn: "persistence_id");

        public static SnapshotTableSchema Snapshot => new(
            TableName: "snapshot_store",
            SchemaName: "",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "created_at",
            PayloadColumn: "payload",
            ManifestColumn: "manifest",
            SerializerIdColumn: "serializer_id");

        public static MetadataTableSchema Metadata => new(
            TableName: "metadata",
            SchemaName: "",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr");
    }

    /// <summary>
    /// SQLite compatibility schemas - for migration from Akka.Persistence.Sqlite! 🪶
    /// </summary>
    public static class SqliteCompat
    {
        public static JournalTableSchema Journal => new(
            TableName: "event_journal",
            SchemaName: "", // SQLite doesn't use schemas
            OrderingColumn: "ordering",
            DeletedColumn: "is_deleted",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "timestamp",
            TagsColumn: "tags",
            PayloadColumn: "payload",
            SerializerIdColumn: "serializer_id",
            ManifestColumn: "manifest");

        public static TagTableSchema Tags => new(
            TableName: "tags",
            SchemaName: "",
            OrderingIdColumn: "ordering_id",
            TagColumn: "tag",
            SequenceNumberColumn: "sequence_nr",
            PersistenceIdColumn: "persistence_id");

        public static SnapshotTableSchema Snapshot => new(
            TableName: "snapshot_store",
            SchemaName: "",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr",
            TimestampColumn: "created_at",
            PayloadColumn: "payload",
            ManifestColumn: "manifest",
            SerializerIdColumn: "serializer_id");

        public static MetadataTableSchema Metadata => new(
            TableName: "metadata",
            SchemaName: "",
            PersistenceIdColumn: "persistence_id",
            SequenceNumberColumn: "sequence_nr");
    }
}

