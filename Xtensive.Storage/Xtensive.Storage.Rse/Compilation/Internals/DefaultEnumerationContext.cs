// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2008.09.01

using System;
using Xtensive.Storage.Rse.Providers;
using Xtensive.Storage.Rse.Providers.Executable;

namespace Xtensive.Storage.Rse.Compilation
{
  /// <summary>
  /// Default <see cref="EnumerationContext"/> implementation.
  /// </summary>
  public sealed class DefaultEnumerationContext : EnumerationContext
  {
    private static readonly GlobalTemporaryData globalTemporaryData = new GlobalTemporaryData();

    public override bool MultipleActiveResultSetSupported
    {
      get { return true; }
    }

    /// <inheritdoc/>
    public override GlobalTemporaryData GlobalTemporaryData {
      get {
        return globalTemporaryData;
      }
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown by this property getter.</exception>
    public override TransactionTemporaryData TransactionTemporaryData
    {
      get { throw new NotSupportedException(); }
    }

    /// <inheritdoc/>
    public override EnumerationContext CreateNew()
    {
      return new DefaultEnumerationContext();
    }

    /// <inheritdoc/>
    protected override EnumerationScope CreateActiveScope()
    {
      return new EnumerationScope(this);
    }
  }
}