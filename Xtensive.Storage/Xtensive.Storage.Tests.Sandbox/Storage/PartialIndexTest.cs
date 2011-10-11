﻿// Copyright (C) 2011 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2011.10.06

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using Xtensive.Core;
using Xtensive.Core.Testing;
using Xtensive.Storage.Model;
using Xtensive.Storage.Providers;
using Xtensive.Storage.Tests.Sandbox.Storage.PartialIndexTestModel;

namespace Xtensive.Storage.Tests.Sandbox.Storage.PartialIndexTestModel
{
  public class TestBase : Entity
  {
    [Field, Key]
    public int Id { get; private set; }
  }

  [HierarchyRoot]
  public class TargetEntity : TestBase
  {
  }

  [HierarchyRoot, Index("TestField", Filter = "Index")]
  public class SimpleFilterWithMethod : TestBase
  {
    public static Expression<Func<SimpleFilterWithMethod, bool>> Index()
    {
      return test => test.TestField.GreaterThan("hello world");
    }

    [Field]
    public string TestField { get; set; }
  }

  [HierarchyRoot, Index("TestField", Filter = "Index")]
  public class SimpleFilterWithProperty : TestBase
  {
    public static Expression<Func<SimpleFilterWithProperty, bool>> Index
    {
      get { return test => test.TestField.GreaterThan("hello world"); }
    }

    [Field]
    public string TestField { get; set; }
  }

  [HierarchyRoot, Index("Target", Filter = "Index")]
  public class FilterOnReferenceField : TestBase
  {
    public static Expression<Func<FilterOnReferenceField, bool>> Index()
    {
      return test => test.Target!=null;
    }

    [Field]
    public TargetEntity Target { get; set; } 
  }

  [HierarchyRoot, Index("Target", Filter = "Index")]
  public class FilterOnReferenceIdField : TestBase
  {
    public static Expression<Func<FilterOnReferenceIdField, bool>> Index()
    {
      return test => test.Target.Id > 0;
    }

    [Field]
    public TargetEntity Target { get; set; }
  }

  [HierarchyRoot, Index("TestField1", Filter = "Index")]
  public class FilterOnAlienField : TestBase
  {
    public static Expression<Func<FilterOnAlienField, bool>> Index
    {
      get { return test => test.TestField2.GreaterThan("hello world"); }
    }

    [Field]
    public string TestField1 { get; set; }

    [Field]
    public string TestField2 { get; set; }
  }

  [HierarchyRoot(InheritanceSchema = InheritanceSchema.ClassTable)]
  public class InheritanceClassTableBase : TestBase
  {
    [Field]
    public int BaseField { get; set; }
  }

  [Index("TestField", Filter = "Index")]
  public class InheritanceClassTable : InheritanceClassTableBase
  {
    public static Expression<Func<InheritanceClassTable, bool>> Index()
    {
      return test => test.BaseField > 0;
    }

    [Field]
    public int TestField { get; set; }
  }

  [HierarchyRoot(InheritanceSchema = InheritanceSchema.SingleTable)]
  public class InheritanceSingleTableBase : TestBase
  {
    [Field]
    public int BaseField { get; set; }
  }

  [Index("TestField", Filter = "Index")]
  public class InheritanceSingleTable : InheritanceSingleTableBase
  {
    public static Expression<Func<InheritanceSingleTable, bool>> Index()
    {
      return test => test.BaseField > 0;
    }

    [Field]
    public int TestField { get; set; }
  }

  [HierarchyRoot(InheritanceSchema = InheritanceSchema.ConcreteTable)]
  public class InheritanceConcreteTableBase : TestBase
  {
    [Field]
    public int BaseField { get; set; }
  }

  [Index("TestField", Filter = "Index")]
  public class InheritanceConcreteTable : InheritanceConcreteTableBase
  {
    public static Expression<Func<InheritanceConcreteTable, bool>> Index()
    {
      return test => test.BaseField > 0;
    }

    [Field]
    public int TestField { get; set; }
  }
}

namespace Xtensive.Storage.Tests.Sandbox.Storage
{
  [TestFixture]
  public class PartialIndexTest
  {
    private Domain domain;

    [TestFixtureSetUp]
    public void TestFixtureSetUp()
    {
      Require.AllFeaturesSupported(ProviderFeatures.PartialIndexes);
    }

    [TearDown]
    public void TearDown()
    {
      if (domain==null)
        return;
      try {
        domain.Dispose();
      }
      finally {
        domain = null;
      }
    }

    private void BuildDomain(IEnumerable<Type> entities)
    {
      var config = DomainConfigurationFactory.Create();
      foreach (var entity in entities)
        config.Types.Register(entity);
      domain = Domain.Build(config);
    }

    private void AssertBuildSuccess(params Type[] entities)
    {
      BuildDomain(entities);
      var partialIndexes = domain.Model.RealIndexes
        .Where(index => index.IsPartial && index.FilterExpression!=null && index.Filter!=null)
        .ToList();
      Assert.IsNotEmpty(partialIndexes);
    }

    private void AssertBuildFailure(params Type[] entities)
    {
      AssertEx.Throws<DomainBuilderException>(() => BuildDomain(entities));
    }

    [Test]
    public void SimpleFilterWithMethodTest()
    {
      AssertBuildSuccess(typeof (SimpleFilterWithMethod));
    }

    [Test]
    public void SimpleFilterWithPropertyTest()
    {
      AssertBuildSuccess(typeof (SimpleFilterWithProperty));
    }

    [Test]
    public void FilterOnReferenceFieldTest()
    {
      AssertBuildSuccess(typeof (FilterOnReferenceField));
    }

    [Test]
    public void FilterOnReferenceFieldIdTest()
    {
      AssertBuildSuccess(typeof (FilterOnReferenceIdField));
    }

    [Test]
    public void FilterOnAlienFieldTest()
    {
      AssertBuildSuccess(typeof (FilterOnAlienField));
    }

    [Test]
    public void InheritanceClassTableTest()
    {
      AssertBuildFailure(typeof (InheritanceClassTable));
    }

    [Test]
    public void InheritanceSingleTableTest()
    {
      AssertBuildSuccess(typeof(InheritanceSingleTable));
    }

    [Test]
    public void InheritanceConcreteTableTest()
    {
      AssertBuildSuccess(typeof(InheritanceConcreteTable));
    }
  }
}