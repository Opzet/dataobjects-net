// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Aleksey Gamzov
// Created:    2008.08.15

namespace Xtensive.Sql.Dom.Database.Comparer
{
  public interface ISqlComparer<T> 
    where T : SchemaNode
  {
    CompareResult<T> Compare(T originalNode, T newNode);
  }
}