// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2009.10.02

using System;
using System.Diagnostics;

namespace Xtensive.Storage.Linq.Expressions.Visitors
{
  [Serializable]
  internal class EntityExpressionJoiner : ExtendedExpressionVisitor
  {
    private readonly ItemProjectorExpression itemProjectorExpression;

    protected override System.Linq.Expressions.Expression VisitEntityExpression(EntityExpression expression)
    {
      Translator.EnsureEntityFieldsAreJoined(expression, itemProjectorExpression, true);
      return base.VisitEntityExpression(expression);
    }

    public static ItemProjectorExpression JoinEntities(ItemProjectorExpression itemProjectorExpression)
    {
      var item = new EntityExpressionJoiner(itemProjectorExpression).Visit(itemProjectorExpression.Item);
      return new ItemProjectorExpression(item, itemProjectorExpression.DataSource, itemProjectorExpression.Context);
    }

    private EntityExpressionJoiner(ItemProjectorExpression itemProjectorExpression)
    {
      this.itemProjectorExpression = itemProjectorExpression;
    }
  }
}