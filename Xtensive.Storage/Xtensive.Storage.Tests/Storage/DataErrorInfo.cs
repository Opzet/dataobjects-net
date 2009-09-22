// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2009.09.17

using System.ComponentModel;
using NUnit.Framework;
using Xtensive.Integrity.Aspects.Constraints;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Storage.DataErrorInfoTestModel;

namespace Xtensive.Storage.Tests.Storage.DataErrorInfoTestModel
{
  [HierarchyRoot]
  public class Person : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field]
    [NotNullConstraint(Message = "Name is empty.")]
    public string Name { get; set; }

    [Field]
    [RangeConstraint(Min = 1, Message = "Age is negative.")]
    public int Age { get; set;}
  }
}

namespace Xtensive.Storage.Tests.Storage
{
  public class DataErrorInfoTest : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(typeof (Person).Assembly, typeof (Person).Namespace);
      return config;
    }

    [Test]
    public void MainTest()
    {
      using (Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {

          using (Xtensive.Storage.Validation.Disable()) {

            var person = new Person();

            Assert.AreEqual("Name is empty.", ((IDataErrorInfo) person)["Name"]);
            Assert.AreEqual("Age is negative.", ((IDataErrorInfo) person)["Age"]);

            person.Name = "Alex";
            person.Age = 26;

            Assert.AreEqual(string.Empty, ((IDataErrorInfo) person)["Name"]);
            Assert.AreEqual(string.Empty, ((IDataErrorInfo) person)["Age"]);
          }

          // Rollback
        }
      }
    }
  }
}