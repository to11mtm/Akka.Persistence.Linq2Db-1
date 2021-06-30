using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Xunit;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Docker.Linq2Db
{
    [CollectionDefinition("OracleXeSpec")]
    public sealed class
        OracleXeSpecsFixture : ICollectionFixture<OracleXeFixture>
    {
        
    }
}