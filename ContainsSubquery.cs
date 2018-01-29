using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using log4net;
using NHibernate;
using NUnit.Framework;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Tool.hbm2ddl;

namespace Nh5Regression
{
    [TestFixture]
    public class ContainsSubquery
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        const string ConnectionString = "Data Source=:memory:;Version=3;New=True;";

        const string Mapping = @"<hibernate-mapping assembly=""Nh5Regression"" namespace=""Nh5Regression"" xmlns=""urn:nhibernate-mapping-2.2"">
    <class name=""ChildEntity"">
        <id name=""Id"" type=""Int32"">
            <generator class=""assigned""/>
        </id>
        <version name=""IntegrityVersion"" column=""IntegrityVersion"" unsaved-value=""0"" />
        <property name=""Name"" type=""string"" />
        <many-to-one name=""Parent"" class=""ParentEntity"" column=""ParentId"" not-null=""true"" not-found=""exception"" />
    </class>

    <class name=""ParentEntity"">
        <id name=""Id"" type=""Int32"">
            <generator class=""assigned""/>
        </id>
        <version name=""IntegrityVersion"" column=""IntegrityVersion"" unsaved-value=""0"" />
        <set name=""Children"" lazy=""true"" generic=""true"" inverse=""true"" cascade=""all-delete-orphan"">
            <key column=""ParentId"" />
            <one-to-many class=""ChildEntity"" />
        </set>
    </class>

    <class name=""GrandChildEntity"">
        <id name=""Id"" type=""Int32"">
            <generator class=""assigned""/>
        </id>
        <version name=""IntegrityVersion"" column=""IntegrityVersion"" unsaved-value=""0"" />
        <many-to-one name=""Parent"" class=""ChildEntity"" column=""ParentId"" />
    </class>

</hibernate-mapping>";

        [Test]
        public void Test()
        {
            var configuration = new Configuration();
            configuration
                .DataBaseIntegration(
                    x =>
                    {
                        x.ConnectionString = ConnectionString;
                        x.Driver<SQLite20Driver>();
                        x.Dialect<SQLiteDialect>();
                        x.LogSqlInConsole = true;
                        x.LogFormattedSql = true;
                    })
                .AddXmlString(Mapping);

            var sessionFactory = configuration.BuildSessionFactory();

            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();
                using (var session = sessionFactory.OpenSession(connection))
                {
                    new SchemaExport(configuration).Execute(false, true, false, connection, Console.Out);

                    var parent = new ParentEntity() { Id = 1 };
                    parent.Children.Add(new ChildEntity() {Id = 1, Parent = parent});
                    parent.Children.Add(new ChildEntity() {Id = 2, Parent = parent});
                    session.Save(parent);
                    session.Flush();
                }

                using (var session = sessionFactory.OpenSession(connection))
                {
                    var parent = session.Get<ParentEntity>(1);

                    Assert.IsFalse(NHibernateUtil.IsInitialized(parent.Children));

                    Assert.DoesNotThrow(() => session.Query<GrandChildEntity>()
                        .Where(e => parent.Children.Contains(e.Parent))
                        .FirstOrDefault());
                }
            }
        }

    }

    public class Entity
    {
        public virtual int Id { get; set; }
        public virtual int IntegrityVersion { get; set; }
    }

    public class ChildEntity : Entity
    {

        public virtual string Name { get; set; }

        public virtual ParentEntity Parent { get; set; }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj as ChildEntity)?.Id == Id;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public class ParentEntity : Entity
    {
        public virtual ISet<ChildEntity> Children { get; set; } = new HashSet<ChildEntity>();
    }

    public class GrandChildEntity : Entity
    {
        public virtual ChildEntity Parent { get; set; }
    }
}
