// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2009.12.24

using NUnit.Framework;
using Xtensive.Core.Diagnostics;

namespace Xtensive.Core.Tests.Diagnostics
{
  [TestFixture]
  public class TypeLevelLogTestTest<T1, T2>
  {
    protected static ILog Log = LogProvider.GetLog(typeof (TypeLevelLogTestTest<,>));
  }

  public class TypeLevelLogTestTest : TypeLevelLogTestTest<int, string>
  {
    [Test]
    public void CombinedTest()
    {
      Log.Info("Type-level logging works!");
    }
  }
}