using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.OracleXe
{
    [Collection("OracleXeSpec")]
    public class DockerLinq2DbOracleXeJournalSpec : JournalSpec
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
                        provider-name = """ + LinqToDB.ProviderName.OracleManaged + @"""
                        use-clone-connection = true
                        oracle-version-11 = true
                        tables.journal {{ 
                           auto-init = true
                           warn-on-auto-init-fail = false
                           table-name = ""{2}"" 
                           }}
                    }}
                }}
            }}
        ";
        
        public static Configuration.Config Create(string connString)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString,"testJournal"));
        }
        public DockerLinq2DbOracleXeJournalSpec(ITestOutputHelper output,
            OracleXeFixture fixture) : base(InitConfig(fixture),
            "oracleXeperf", output)
        {
            
            DebuggingHelpers.SetupTraceDump(output);
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(Create(DockerDbUtils.ConnectionString).GetConfig("akka.persistence.journal.testspec")));
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<JournalRow>().Delete();
                }
                catch (Exception e)
                {
                }
            }

            Initialize();
        }
            
        public static Configuration.Config InitConfig(OracleXeFixture fixture)
        {
            //need to make sure db is created before the tests start
            //DbUtils.Initialize(fixture.ConnectionString);
            

            return Create(fixture.ConnectionString);
        }  
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
//            DbUtils.Clean();
        }

        protected override bool SupportsSerialization => false;
    
    }
}