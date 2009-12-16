// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.12.16

using NUnit.Framework;
using Xtensive.Core.Diagnostics;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Tests.Storage.IoC.Model;

namespace Xtensive.Storage.Tests.Storage.IoC.Model
{
  [HierarchyRoot]
  public class MyEntity : Entity
  {
    [Field, Key]
    public int Id { get; private set; }
  }

  public interface IMyService
  {
  }

  public class MyServiceImpl : SessionBound, IMyService
  {
  }
}

namespace Xtensive.Storage.Tests.Storage.IoC
{
  [TestFixture]
  public abstract class ServiceTestBase : AutoBuildTest
  {
    protected override DomainConfiguration BuildConfiguration()
    {
      var config = base.BuildConfiguration();
      config.Types.Register(typeof(MyEntity));
      return config;
    }

    [Test]
    public void ContainerTest()
    {

      using (var session = Session.Open(Domain)) {
        using (Transaction.Open()) {

          // Domain-level singleton service
          var domainSingleton1 = Domain.Services.GetInstance<IMyService>("singleton");
          var domainSingleton2 = Domain.Services.GetInstance<IMyService>("singleton");
          Assert.AreSame(domainSingleton1, domainSingleton2);

          // Domain-level transient service
          var domainTransient1 = Domain.Services.GetInstance<IMyService>("transient");
          var domainTransient2 = Domain.Services.GetInstance<IMyService>("transient");
          Assert.AreNotSame(domainTransient1, domainTransient2);

          // Session-level singleton service
          var sessionSingleton1 = session.Services.GetInstance<IMyService>();
          var sessionSingleton2 = session.Services.GetInstance<IMyService>();
          Assert.AreSame(sessionSingleton1, sessionSingleton2);

          using (Session.Open(Domain)) {
            using (Transaction.Open()) {
              // Session-level singleton service from another session
              var sessionSingleton3 = Session.Current.Services.GetInstance<IMyService>();
              Assert.AreNotSame(sessionSingleton1, sessionSingleton3);
            }
          }
        }
      }
    }

    [Test]
    public void PerformanceTest()
    {
      const int iterationCount = 100000;
      using (var session = Session.Open(Domain)) {
        using (Transaction.Open()) {
          using (new Measurement("Getting domain-level singleton service.", iterationCount))
            for (int i = 0; i < iterationCount; i++)
              Domain.Services.GetInstance<IMyService>("singleton");

          using (new Measurement("Getting domain-level transient service.", iterationCount))
            for (int i = 0; i < iterationCount; i++)
              Domain.Services.GetInstance<IMyService>("transient");

          using (new Measurement("Getting session-level singleton service.", iterationCount))
            for (int i = 0; i < iterationCount; i++)
              session.Services.GetInstance<IMyService>();
        }
      }
    }
  }
}