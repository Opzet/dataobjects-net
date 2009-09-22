// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.09.14

using System;
using NUnit.Framework;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Interfaces.TypeIdModeConflictTestModel;

namespace Xtensive.Storage.Tests.Interfaces.TypeIdModeConflictTestModel
{
  public interface IRoot : IEntity
  {
  }

  [HierarchyRoot]
  public class Root1 : Entity, IRoot
  {
    [Field, Key]
    public int Id { get; private set; }
  }

  [HierarchyRoot(IncludeTypeId = true)]
  public class Root2 : Entity, IRoot
  {
    [Field, Key]
    public int Id { get; private set; }
  }
}

namespace Xtensive.Storage.Tests.Interfaces
{
  public class TypeIdModeConflictTest : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(typeof (Root1).Assembly, typeof (Root1).Namespace);
      return config;
    }

    protected override Domain BuildDomain(DomainConfiguration configuration)
    {
      try {
        base.BuildDomain(configuration);
        Assert.Fail();
      }
      catch (DomainBuilderException e) {
        Console.WriteLine(e);
      }
      return null;
    }
  }
}