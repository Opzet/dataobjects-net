// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.09.18

using Xtensive.Core;

namespace Xtensive.Storage.Providers.Sql.Expressions
{
  internal class ExpressionTranslationScope : Scope<ExpressionTranslationContext>
  {
    public static new ExpressionTranslationContext CurrentContext
    {
      get { return Scope<ExpressionTranslationContext>.CurrentContext; }
    }

    public ExpressionTranslationScope(Driver driver)
      : base(new ExpressionTranslationContext(driver))
    {
    }
  }
}