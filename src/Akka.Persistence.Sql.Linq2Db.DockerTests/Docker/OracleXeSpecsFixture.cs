using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker
{
    [CollectionDefinition("OracleXeSpec")]
    public sealed class OracleXeSpecsFixture : ICollectionFixture<OracleXeFixture>
    {
    }
}