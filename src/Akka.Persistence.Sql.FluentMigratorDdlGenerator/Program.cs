// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.CommandLine;
using Akka.Persistence.Sql.FluentMigratorDdlGenerator;

Console.WriteLine("✨ Akka.Persistence.Sql FluentMigrator DDL Generator ✨");
Console.WriteLine("~~ So kawaii, so clean, uwu~~ 💖\n");

var rootCommand = new RootCommand("Generates DDL scripts using FluentMigrator for Akka.Persistence.Sql tables");

var outputOption = new Option<string>(
    name: "--output",
    description: "Output directory for generated DDL files",
    getDefaultValue: () => Path.Combine(Directory.GetCurrentDirectory(), "ddl"));
outputOption.AddAlias("-o");

var providerOption = new Option<string?>(
    name: "--provider",
    description: "Database provider (sqlserver, postgresql, mysql, sqlite). If not specified, generates for all.");
providerOption.AddAlias("-p");

var mappingOption = new Option<string?>(
    name: "--mapping",
    description: "Table mapping mode (default, compat). If not specified, generates for both.");
mappingOption.AddAlias("-m");

rootCommand.AddOption(outputOption);
rootCommand.AddOption(providerOption);
rootCommand.AddOption(mappingOption);

rootCommand.SetHandler(async (string output, string? provider, string? mapping) =>
{
    var generator = new FluentMigratorDdlGenerator(output);

    if (provider == null && mapping == null)
    {
        // Generate all! 🌟
        await generator.GenerateAll();
    }
    else
    {
        var providers = provider != null
            ? new[] { ParseProvider(provider) }
            : new[] { DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MySql, DatabaseProvider.Sqlite };

        var mappings = mapping != null
            ? new[] { ParseMapping(mapping) }
            : new[] { TableMappingMode.Default, TableMappingMode.Compat };

        foreach (var m in mappings)
        {
            foreach (var p in providers)
            {
                await generator.GenerateForProvider(p, m);
            }
        }
    }

    Console.WriteLine("\n🎉 All done! Your DDL files are ready, senpai! uwu~");
}, outputOption, providerOption, mappingOption);

return await rootCommand.InvokeAsync(args);

DatabaseProvider ParseProvider(string provider)
{
    return provider.ToLowerInvariant() switch
    {
        "sqlserver" or "sql-server" => DatabaseProvider.SqlServer,
        "postgresql" or "postgres" => DatabaseProvider.PostgreSql,
        "mysql" => DatabaseProvider.MySql,
        "sqlite" => DatabaseProvider.Sqlite,
        _ => throw new ArgumentException($"Unknown provider: {provider}. Use sqlserver, postgresql, mysql, or sqlite~")
    };
}

TableMappingMode ParseMapping(string mapping)
{
    return mapping.ToLowerInvariant() switch
    {
        "default" => TableMappingMode.Default,
        "compat" or "compatibility" => TableMappingMode.Compat,
        _ => throw new ArgumentException($"Unknown mapping: {mapping}. Use default or compat~")
    };
}

