﻿#region license
// Transformalize
// Configurable Extract, Transform, and Load
// Copyright 2013-2016 Dale Newman
//  
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//       http://www.apache.org/licenses/LICENSE-2.0
//   
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Autofac;
using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transformalize.Configuration;
using Transformalize.Containers.Autofac;
using Transformalize.Contracts;
using Transformalize.Providers.Console;
using Transformalize.Providers.MySql;
using Transformalize.Providers.MySql.Autofac;
using Transformalize.Providers.SqlServer;
using Transformalize.Providers.SqlServer.Autofac;
using Transformalize.Transforms.Jint.Autofac;

namespace Test {

   [TestClass]
   public class NorthWindIntegrationMySql {

      public string TestFile { get; set; } = @"Files\NorthWindSqlServerToMySql.xml";
      private const string Password = "DevDev1!"; // "Wr0ngP@$$w0rd";

      public Connection InputConnection { get; set; } = new Connection {
         Name = "input",
         Provider = "sqlserver",
         ConnectionString = $"server=localhost;database=NorthWind;User Id=sa;Password={Password};Trust Server Certificate=True;"
      };

      public Connection OutputConnection { get; set; } = new Connection {
         Name = "output",
         Provider = "mysql",
         ConnectionString = $"Server=localhost;Database=northwindstar;Uid=root;Pwd={Password};"
      };

      public Process ResolveRoot(IContainer container, string file, bool init) {
         return container.Resolve<Process>(new NamedParameter("cfg", file + (init ? "?Mode=init" : string.Empty)));
      }

      [TestMethod]
      //[Ignore("Needs local sql server and mysql databases and you have to set the password (above)")]
      public void Integration() {

         var logger = new ConsoleLogger(LogLevel.Debug);

         // CORRECT DATA AND INITIAL LOAD
         using (var cn = new SqlServerConnectionFactory(InputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(2, cn.Execute(@"
                    UPDATE [Order Details] SET UnitPrice = 14.40, Quantity = 42 WHERE OrderId = 10253 AND ProductId = 39;
                    UPDATE Orders SET CustomerID = 'CHOPS', Freight = 22.98 WHERE OrderId = 10254;
                "));
         }

         using (var outer = new ConfigurationContainer().CreateScope(TestFile + $"?Mode=init&Password={Password}", logger)) {

            var process = outer.Resolve<Process>();
            using (var inner = new Container(new MySqlModule(), new SqlServerModule(), new JintTransformModule()).CreateScope(process, logger)) {
               var controller = inner.Resolve<IProcessController>();
               controller.Execute();
            }
         }

         using (var cn = new MySqlConnectionFactory(OutputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(2155, cn.ExecuteScalar<int>("SELECT COUNT(*) FROM NorthWindStar;"));
            Assert.AreEqual(2155, cn.ExecuteScalar<int>("SELECT Inserts FROM NorthWindControl WHERE Entity = 'Order Details' AND BatchId = 1 LIMIT 1;"));
         }

         // FIRST DELTA, NO CHANGES
         using (var outer = new ConfigurationContainer().CreateScope(TestFile + $"?Password={Password}", logger)) {
            var process = outer.Resolve<Process>();
            using (var inner = new Container(new MySqlModule(), new SqlServerModule(), new JintTransformModule()).CreateScope(process, logger)) {
               var controller = inner.Resolve<IProcessController>();
               controller.Execute();
            }
         }

         using (var cn = new MySqlConnectionFactory(OutputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(2155, cn.ExecuteScalar<int>("SELECT COUNT(*) FROM NorthWindStar;"));
            Assert.AreEqual(0, cn.ExecuteScalar<int>("SELECT Inserts+Updates+Deletes FROM NorthWindControl WHERE Entity = 'Order Details' AND BatchId = 9 LIMIT 1;"));
         }


         // CHANGE 2 FIELDS IN 1 RECORD IN MASTER TABLE THAT WILL CAUSE CALCULATED FIELD TO BE UPDATED TOO 
         using (var cn = new SqlServerConnectionFactory(InputConnection).GetConnection()) {
            cn.Open();
            const string sql = @"UPDATE [Order Details] SET UnitPrice = 15, Quantity = 40 WHERE OrderId = 10253 AND ProductId = 39;";
            Assert.AreEqual(1, cn.Execute(sql));
         }

         using (var outer = new ConfigurationContainer().CreateScope(TestFile + $"?Password={Password}", logger)) {
            var process = outer.Resolve<Process>();
            using (var inner = new Container(new MySqlModule(), new SqlServerModule(), new JintTransformModule()).CreateScope(process, logger)) {
               var controller = inner.Resolve<IProcessController>();
               controller.Execute();
            }
         }

         using (var cn = new MySqlConnectionFactory(OutputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(1, cn.ExecuteScalar<int>("SELECT Updates FROM NorthWindControl WHERE Entity = 'Order Details' AND BatchId = 17 LIMIT 1;"));
            Assert.AreEqual(15.0M, cn.ExecuteScalar<decimal>("SELECT OrderDetailsUnitPrice FROM NorthWindStar WHERE OrderDetailsOrderId= 10253 AND OrderDetailsProductId = 39;"));
            Assert.AreEqual(40, cn.ExecuteScalar<int>("SELECT OrderDetailsQuantity FROM NorthWindStar WHERE OrderDetailsOrderId= 10253 AND OrderDetailsProductId = 39;"));
            Assert.AreEqual(15.0 * 40, cn.ExecuteScalar<int>("SELECT OrderDetailsExtendedPrice FROM NorthWindStar WHERE OrderDetailsOrderId= 10253 AND OrderDetailsProductId = 39;"));
         }

         // CHANGE 1 RECORD'S CUSTOMERID AND FREIGHT ON ORDERS TABLE
         using (var cn = new SqlServerConnectionFactory(InputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(1, cn.Execute("UPDATE Orders SET CustomerID = 'VICTE', Freight = 20.11 WHERE OrderId = 10254;"));
         }

         using (var outer = new ConfigurationContainer().CreateScope(TestFile + $"?Password={Password}", logger)) {
            var process = outer.Resolve<Process>();
            using (var inner = new Container(new MySqlModule(), new SqlServerModule(), new JintTransformModule()).CreateScope(process, logger)) {
               var controller = inner.Resolve<IProcessController>();
               controller.Execute();
            }
         }

         using (var cn = new MySqlConnectionFactory(OutputConnection).GetConnection()) {
            cn.Open();
            Assert.AreEqual(1, cn.ExecuteScalar<int>("SELECT Updates FROM NorthWindControl WHERE Entity = 'Orders' AND BatchId = 26;"));
            Assert.AreEqual("VICTE", cn.ExecuteScalar<string>("SELECT OrdersCustomerId FROM NorthWindStar WHERE OrderDetailsOrderId= 10254;"));
            Assert.AreEqual(20.11M, cn.ExecuteScalar<decimal>("SELECT OrdersFreight FROM NorthWindStar WHERE OrderDetailsOrderId= 10254;"));
         }

      }
   }
}
