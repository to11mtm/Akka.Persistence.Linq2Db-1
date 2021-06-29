﻿using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Postgres
{
    public static class OracleXeJournalSpecConfig
    {
        public static string _journalBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                journal {{
                    plugin = ""akka.persistence.journal.testspec""
                    testspec {{
                        class = ""{0}""
                        #plugin-dispatcher = ""akka.actor.default-dispatcher""
plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                        connection-string = ""{1}""
#connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = ""{2}""
                        use-clone-connection = false
                        oracle-version-11 = true
                        tables.journal {{ 
                           auto-init = true 
                           warn-on-auto-init-fail = false
                        }}
                    }}
                }}
            }}
        ";
        
        public static Configuration.Config Create(string connString, string providerName)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString, providerName));
        }
    }
}