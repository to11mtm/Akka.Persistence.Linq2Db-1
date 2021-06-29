using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Akka.Persistence.TCK.Snapshot;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.OracleXe
{
    [Collection("OracleXeSpec")]
    
    public class OracleXeSnapshotSpec : SnapshotStoreSpec
    {
        public static string _snapshotBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                snapshot-store {{
                    plugin = ""akka.persistence.snapshot-store.testspec""
                    testspec {{
                        class = ""{0}""
                        #plugin-dispatcher = ""akka.actor.default-dispatcher""
                        plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                
                        connection-string = ""{1}""
#connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = """ + LinqToDB.ProviderName.OracleManaged + @"""
                        use-clone-connection = true
                        oracle-version-11 = true
                        tables.snapshot {{ 
                           auto-init = true
                           warn-on-auto-init-fail = false
                           table-name = ""{2}"" 
                           }}
                    }}
                }}
            }}
        ";
        
        
        //private static
        static Configuration.Config conf(OracleXeFixture fixture) => ConfigurationFactory.ParseString(
            string.Format(_snapshotBaseConfig,
                typeof(Linq2DbSnapshotStore).AssemblyQualifiedName,fixture.ConnectionString
                ,"l2dbsnapshotSpec"));

        public OracleXeSnapshotSpec(ITestOutputHelper outputHelper, OracleXeFixture fixture) :
            base(conf(fixture))
        {
            DebuggingHelpers.SetupTraceDump(outputHelper);
            var connFactory = new AkkaPersistenceDataConnectionFactory(
                new SnapshotConfig(
                    conf(fixture).GetConfig("akka.persistence.snapshot-store.testspec")));
            using (var conn = connFactory.GetConnection())
            {
                
                try
                {
                    conn.GetTable<SnapshotRow>().Delete();
                }
                catch (Exception e)
                {

                }
            }
            
            Initialize();
        }
    }
}