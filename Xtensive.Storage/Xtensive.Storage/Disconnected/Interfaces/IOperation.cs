// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.10.21

using Xtensive.Storage.Disconnected.Log;

namespace Xtensive.Storage.Disconnected.Interfaces
{
  public interface IOperation
  {
    Key Key { get; }
    void Prepare(PrefetchContext prefetchContext);
    void Execute(IOperationExecutionContext executionContext);
  }
}