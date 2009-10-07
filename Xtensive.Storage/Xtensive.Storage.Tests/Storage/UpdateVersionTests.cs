// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Ivan Galkin
// Created:    2009.10.06

using System;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Xtensive.Storage.Configuration;
using System.Linq;

# region Model

namespace Xtensive.Storage.Tests.Storage.VersionModel
{
  [HierarchyRoot]
  [KeyGenerator(null)]
  public class Address : Entity
  {
    [Key(0), Field]
    public string City { get; private set; }

    [Key(1), Field]
    public string RegionCode { get; private set; }

    public Address(string city, string regionCode)
      : base(city, regionCode)
    {
    }
  }

  public class Phone : Structure
  {
    [Field]
    public int Code { get; set; }

    [Field]
    public int Number { get; set; }
  }

  [HierarchyRoot]
  public class Person : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field]
    public string Name { get; set; }
  }

  public class Customer : Person
  {
    [Field]
    public Address Address { get; set; }

    [Field]
    public Phone Phone { get; set; }
  }

  [HierarchyRoot]
  public class Author : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Version, Field]
    public int VersionId { get; private set; }

    [Field]
    public string Name { get; set; }

    [Field]
    public Phone Phone { get; set; }

    [Field, Association(PairTo = "Authors")]
    public EntitySet<Book> Books { get; private set; }

    [Field, Association(PairTo = "Author")]
    public EntitySet<Comment> Comments { get; private set; }
  }

  [HierarchyRoot]
  public class Book : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Version, Field]
    public int VersionId { get; private set; }

    [Field]
    public string Title { get; set; }

    [Field]
    public EntitySet<Author> Authors { get; private set; }
  }

  [HierarchyRoot]
  public class Comment : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field]
    public Author Author { get; set; }
  }


  public class VersionStructure : Structure
  {
    [Field]
    public int Version { get; set; }
  }

  [HierarchyRoot]
  public class VersionEntity : Entity
  {
    [Key, Field]
    public int Id { get; private set; }
  }

  [HierarchyRoot]
  public class ItemWithStructureVersion : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Version, Field]
    public VersionStructure VersionId { get; private set;}

    [Field]
    public string Field { get; set;}

    protected override void UpdateVersion()
    {
      VersionId = new VersionStructure{Version = Field==null ? 0 : Field.GetHashCode()};
    }
  }

  [HierarchyRoot]
  public class ItemWithEntityVersion : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Version, Field]
    public VersionEntity VersionId { get; private set;}

    [Field]
    public string Field { get; set;}

    protected override void UpdateVersion()
    {
      VersionId = new VersionEntity();
    }
  }

  [HierarchyRoot]
  public class ItemWithCustomVersions : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field, Version]
    public long VersionId1 { get; set; }

    [Field]
    public string Field { get; set; }

    protected override void UpdateVersion()
    {
      VersionId1 = DateTime.Now.Ticks;
    }
  }

  [HierarchyRoot]
  public class ItemWithAutoVersions : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field, Version]
    public int VersionId1 { get; set; }

    [Field, Version]
    public long VersionId2 { get; set; }

    [Field]
    public string Field { get; set; }
  }
}

# endregion

namespace Xtensive.Storage.Tests.Storage
{
  using VersionModel;

  [TestFixture]
  public class UpdateVersionTests
    : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      DomainConfiguration config = base.BuildConfiguration();
      config.Types.Register(Assembly.GetExecutingAssembly(), "Xtensive.Storage.Tests.Storage.VersionModel");
      return config;
    }

    [Test]
    public void GenerateAutoVersionTest()
    {
      Key key;
      int version1;
      long version2;
      VersionInfo versionInfo;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = new ItemWithAutoVersions();
          key = instance.Key;
          version1 = instance.VersionId1;
          version2 = instance.VersionId2;
          versionInfo = instance.GetVersion();
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithAutoVersions>.Single(key);
          Assert.AreEqual(versionInfo, instance.GetVersion());
          instance.Field = "New value";
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithAutoVersions>.Single(key);
          Assert.AreEqual(version1 + 1, instance.VersionId1);
          Assert.AreEqual(version2 + 1, instance.VersionId2);
          Assert.IsFalse(versionInfo==instance.GetVersion());
          transactionScope.Complete();
        }
      }
    }

    [Test]
    public void GenerateCustomVersionTest()
    {
      Key key;
      VersionInfo versionInfo;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = new ItemWithCustomVersions();
          key = instance.Key;
          versionInfo = instance.GetVersion();
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithCustomVersions>.Single(key);
          Assert.AreEqual(versionInfo, instance.GetVersion());
          instance.Field = "New value";
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithCustomVersions>.Single(key);
          Assert.IsFalse(versionInfo==instance.GetVersion());
          transactionScope.Complete();
        }
      }
    }

    [Test]
    public void GenerateStructureVersionTest()
    {
      Key key;
      VersionInfo version;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = new ItemWithStructureVersion();
          key = instance.Key;
          version = instance.GetVersion();
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithStructureVersion>.Single(key);
          Assert.IsTrue(version == instance.GetVersion());
          instance.Field = "NextValue";
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithStructureVersion>.Single(key);
          Assert.IsFalse(version == instance.GetVersion());
          transactionScope.Complete();
        }
      }
    }

    [Test]
    public void GenerateEntityVersionTest()
    {
      Key key;
      VersionInfo version;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = new ItemWithEntityVersion();
          key = instance.Key;
          version = instance.GetVersion();
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithEntityVersion>.Single(key);
          Assert.IsTrue(version==instance.GetVersion());
          instance.Field = "NextValue";
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var instance = Query<ItemWithEntityVersion>.Single(key);
          Assert.IsFalse(version==instance.GetVersion());
          transactionScope.Complete();
        }
      }
    }

    [Test]
    public void FetchFieldOnGetVersionTest()
    {
      Key customerKey;
      VersionInfo customerVersion;
      Key orderKey;
      VersionInfo orderVersion;
      Key orderItemKey;
      VersionInfo orderItemVersion;
      Key bookKey;
      VersionInfo bookVersion;
      Key authorKey;
      VersionInfo authorVersion;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var customer1 = new Customer {
            Name = "Customer1",
            Address = new Address("City1", "Region1"),
            Phone = new Phone {Code = 123, Number = 321}
          };
          customerKey = customer1.Key;
          customerVersion = customer1.GetVersion();
          transactionScope.Complete();
        }
      }

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var customer = Query<Person>.All.First(person => person.Name=="Customer1") as Customer;
          Assert.IsTrue(customerVersion==customer.GetVersion());
          customer.Address = new Address("City2", "Region2");
          transactionScope.Complete();
        }
      }
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var customer = Query<Person>.All.First(person => person.Name=="Customer1") as Customer;
          Assert.IsFalse(customerVersion==customer.GetVersion());
          customerVersion = customer.GetVersion();
          customer.Phone.Number = 0;
          transactionScope.Complete();
        }
      }
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var customer = Query<Person>.All.First(person => person.Name=="Customer1") as Customer;
          Assert.IsFalse(customerVersion==customer.GetVersion());
          transactionScope.Complete();
        }
      }
    }

    [Test]
    public void AutoUpdateVersion()
    {
      Key bookKey;
      VersionInfo bookVersion;
      Key authorKey;
      VersionInfo authorVersion;
      Key commentKey;

      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var book = new Book {Title = "Book1"};
          var author = new Author {Name = "Author1"};
          book.Authors.Add(author);
          Assert.AreEqual(1, book.Authors.Count);
          Assert.AreEqual(1, author.Books.Count);
          bookKey = book.Key;
          bookVersion = book.GetVersion();
          authorKey = author.Key;
          var comment = new Comment {Author = author};
          commentKey = comment.Key;
          authorVersion = author.GetVersion();
          
          transactionScope.Complete();
        }
      }

      // Single property changed
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var author = Query<Author>.Single(authorKey);
          Assert.IsTrue(authorVersion==author.GetVersion());
          author.Name = "Author2";
          Assert.IsFalse(authorVersion==author.GetVersion());
          authorVersion = author.GetVersion();
          transactionScope.Complete();
        }
      }

      // Structure field changed
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var author = Query<Author>.Single(authorKey);
          author.Phone = new Phone{Code = 123, Number = 321};
          Assert.IsFalse(authorVersion==author.GetVersion());
          authorVersion = author.GetVersion();
          transactionScope.Complete();
        }
      }
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var author = Query<Author>.Single(authorKey);
          author.Phone.Code = 0;
          Assert.IsFalse(authorVersion==author.GetVersion());
          authorVersion = author.GetVersion();
          transactionScope.Complete();
        }
      }

      // OneToMany EntitySet changed
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var comment = Query<Comment>.Single(commentKey);
          var author = Query<Author>.Single(authorKey);
          comment.Author = null;
          Assert.IsFalse(authorVersion==author.GetVersion());
          authorVersion = author.GetVersion();
          transactionScope.Complete();
        }
      }

      // ManyToMany EntitySet changed
      using (var session = Session.Open(Domain)) {
        using (var transactionScope = Transaction.Open()) {
          var book = Query<Book>.Single(bookKey);
          var author = Query<Author>.Single(authorKey);
          Assert.IsTrue(bookVersion==book.GetVersion());
          Assert.IsTrue(authorVersion==author.GetVersion());
          book.Authors.Remove(author);
          Assert.AreEqual(0, book.Authors.Count);
          Assert.AreEqual(0, author.Books.Count);
          Assert.IsFalse(authorVersion==author.GetVersion());
          Assert.IsFalse(bookVersion==book.GetVersion());
          transactionScope.Complete();
        }
      }
    }
  }
}