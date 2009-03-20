// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2008.08.07

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Threading;
using Xtensive.Storage.Model;

namespace Xtensive.Storage.Rse
{
  /// <summary>
  /// Read only collection of <see cref="ColumnGroup"/>.
  /// </summary>
  [Serializable]
  public class ColumnGroupCollection : ReadOnlyCollection<ColumnGroup>
  {
    private static ThreadSafeCached<ColumnGroupCollection> cachedEmpty =
      ThreadSafeCached<ColumnGroupCollection>.Create(new object());

    /// <summary>
    /// Gets the <see cref="Xtensive.Storage.Model.ColumnGroup"/> by specified group index.
    /// </summary>
    public ColumnGroup this[int groupIndex]
    {
      get
      {
        return this.ElementAt(groupIndex);
      }
    }

    /// <summary>
    /// Gets the empty <see cref="ColumnGroupCollection"/>.
    /// </summary>    
    public static ColumnGroupCollection Empty {
      [DebuggerStepThrough]
      get {
        return cachedEmpty.GetValue(
          () => new ColumnGroupCollection(Enumerable.Empty<ColumnGroup>()));
      }
    }

    /// <summary>
    /// Gets the index of the group by provided <paramref name="segment"/>.
    /// </summary>
    /// <param name="segment">Segment of record' columns.</param>
    public int GetGroupIndexBySegment(Segment<int> segment)
    {
      int index = 0;
      foreach (var columnGroup in this) {
        Func<int, bool> predicate = i => i >= segment.Offset && i < segment.EndOffset;
        if (columnGroup.Keys.Any(predicate))
          return index;
        if (columnGroup.Columns.Any(predicate))
          return index;
        index++;
      }
      throw new InvalidOperationException("Column group could not be found.");
    }

    
    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="items">The collection items.</param>
    public ColumnGroupCollection(IEnumerable<ColumnGroup> items)
      : base(items.ToList())
    {      
    }
  }
}