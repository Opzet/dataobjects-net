// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2009.11.11

using System;
using System.Linq;
using System.Threading;
using System.Transactions;
using NUnit.Framework;
using Xtensive.Core.Aspects;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Rse;
using Xtensive.Core.Reflection;

namespace Xtensive.Storage.Manual.Concurrency.Versions
{
  #region Model

  [Serializable]
  [HierarchyRoot]
  public class Person : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field(Length = 200)]
    public string Name { get; set; }

    [Field(Length = 200)]
    public string SecondName { get; set; }

    [Field]
    [Association(PairTo = "Friends")]
    public EntitySet<Person> Friends { get; private set; }

    [Field]
    public Company Company { get; set; }

    public string FullName {
      get {
        return "{Name} {SecondName}".FormatWith(this);
      }
    }

    public override string ToString()
    {
      return ToString(false);
    }

    public string ToString(bool withFriends)
    {
      if (withFriends)
        return "Person('{0}', Friends={{1}})".FormatWith(FullName, Friends.ToCommaDelimitedString());
      else
        return "Person('{0}')".FormatWith(FullName);
    }

    public Person(string fullName)
    {
      var pair = fullName.RevertibleSplitFirstAndTail('\\', ',');
      SecondName = pair.First.Trim();
      Name = pair.Second.Trim();
    }
  }

  [Serializable]
  [HierarchyRoot]
  public class Company : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field(Length = 200)]
    public string Name { get; set; }

    [Field, Version] 
    // If HandleVersionInfoUpdate() isn't implemented, default implementation
    // (relying on VersionGenerator.Next(...) method) is used.
    public int Version { get; private set; }

    [Field]
    [Association(PairTo = "Company")]
    // By default any EntitySet change also leads to Version change
    public EntitySet<Person> Employees { get; private set; }

    public override string ToString()
    {
      return ToString(true);
    }

    public string ToString(bool withPersons)
    {
      if (withPersons)
        return "Company('{0}', Employees: {1})".FormatWith(Name, Employees.ToCommaDelimitedString());
      else
        return "Company('{0}')".FormatWith(Name);
    }

    public Company(string name)
    {
      Name = name;
    }
  }

  #endregion
  
  [TestFixture]
  public class VersionsTest
  {
    private Domain existingDomain;

    [Test]
    public void CombinedTest()
    {
      var domain = GetDomain();

      using (Session.Open(domain)) {
        // Auto transactions!
        var alex = new Person("Yakunin, Alex");
        var alexVersion = alex.VersionInfo;
        Dump(alex);
        var dmitri = new Person("Maximov, Dmitri");
        var dmitriVersion = dmitri.VersionInfo;
        Dump(dmitri);

        alex.Friends.Add(dmitri);
        // Versions won't change!
        Assert.AreEqual(alexVersion, alex.VersionInfo);
        Assert.AreEqual(dmitriVersion, dmitri.VersionInfo);

        var xtensive = new Company("X-tensive.com");
        var xtensiveVersion = xtensive.VersionInfo;
        Dump(xtensive);
        
        string newName = "Xtensive";
        Console.WriteLine("Changing {0} name to {1}", xtensive.Name, newName);
        xtensive.Name = newName;
        Dump(xtensive);
        Assert.AreNotEqual(xtensiveVersion, xtensive.VersionInfo);
        xtensiveVersion = xtensive.VersionInfo;
        
        Console.WriteLine("Xtensive.Employees.Add(Alex)");
        xtensive.Employees.Add(alex);
        Dump(xtensive);
        Assert.AreNotEqual(xtensiveVersion, xtensive.VersionInfo);
        Assert.AreNotEqual(alexVersion, alex.VersionInfo);
        xtensiveVersion = xtensive.VersionInfo;
        alexVersion = alex.VersionInfo;

        Console.WriteLine("Dmitri.Company = Xtensive");
        dmitri.Company = xtensive;
        Dump(xtensive);
        Assert.AreNotEqual(xtensiveVersion, xtensive.VersionInfo);
        Assert.AreNotEqual(dmitriVersion, dmitri.VersionInfo);
        xtensiveVersion = xtensive.VersionInfo;
        dmitriVersion = dmitri.VersionInfo;

        Console.WriteLine("Transaction rollback test, before:");
        Dump(xtensive);
        var xtensiveVersionFieldValue = xtensive.Version;
        using (var tx = Transaction.Open()) {

          xtensive.Employees.Remove(alex);
          // Xtensive version is changed
          var newXtensiveVersionInsideTransaction = xtensive.VersionInfo;
          Assert.AreNotEqual(xtensiveVersion, newXtensiveVersionInsideTransaction);
          Assert.AreEqual(xtensiveVersionFieldValue, xtensive.Version - 1); // Incremented
          // Alex version is changed
          Assert.AreNotEqual(alexVersion, alex.VersionInfo);

          xtensive.Employees.Remove(dmitri);
          // Xtensive version is NOT changed, since we try to update each version
          // just once per transaction
          Assert.AreEqual(newXtensiveVersionInsideTransaction, xtensive.VersionInfo);
          Assert.AreEqual(xtensiveVersionFieldValue, xtensive.Version - 1); // No increment now
          // Dmitri's version is changed
          Assert.AreNotEqual(dmitriVersion, dmitri.VersionInfo);

          Console.WriteLine("Transaction rollback test, inside:");
          Dump(xtensive);
          // tx.Complete(); // Rollback!
        }

        Console.WriteLine("Transaction rollback test, after:");
        Dump(xtensive);

        // Let's check if everything is rolled back
        Assert.AreEqual(xtensiveVersion, xtensive.VersionInfo);
        Assert.AreEqual(xtensiveVersionFieldValue, xtensive.Version);
        Assert.AreEqual(xtensiveVersion, xtensive.VersionInfo);
        Assert.AreEqual(dmitriVersion, dmitri.VersionInfo);
      }
    }

    private void Dump(Entity entity)
    {
      Console.WriteLine("Entity: {0}", entity);
      Console.WriteLine("          Key: {0}", entity.Key);
      Console.WriteLine("  VersionInfo: {0}", entity.VersionInfo);
      Console.WriteLine();
    }

    private Domain GetDomain()
    {
      if (existingDomain==null) {
        var config = new DomainConfiguration("sqlserver://localhost/DO40-Tests") {
          UpgradeMode = DomainUpgradeMode.Recreate
        };
        config.Types.Register(typeof (Person).Assembly, typeof (Person).Namespace);
        var domain = Domain.Build(config);
        existingDomain = domain;
      }
      return existingDomain;
    }
  }
}