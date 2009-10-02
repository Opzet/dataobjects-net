// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.09.17

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xtensive.Core;
using Xtensive.Core.Threading;
using Xtensive.Core.Tuples;
using Xtensive.Storage.Model;

namespace Xtensive.Storage.Internals
{
  [Serializable]
  internal sealed class PrefetchTaskContainer
  {
    private Dictionary<FieldInfo, ReferencedEntityPrefetchTask> referencedEntityPrefetchTasks;
    private Dictionary<FieldInfo, EntitySetPrefetchTask> entitySetPrefetchTasks;
    private readonly PrefetchProcessor processor;
    private readonly bool exactType;
    private int? cachedHashCode;

    public readonly Key Key;
    
    public readonly TypeInfo Type;

    public EntityPrefetchTask EntityPrefetchTask { get; private set; }

    public IEnumerable<ReferencedEntityPrefetchTask> ReferencedEntityPrefetchTasks
    {
      get { return referencedEntityPrefetchTasks!=null ? referencedEntityPrefetchTasks.Values : null; }
    }

    public IEnumerable<EntitySetPrefetchTask> EntitySetPrefetchTasks
    {
      get { return entitySetPrefetchTasks!=null ? entitySetPrefetchTasks.Values : null; }
    }

    public void AddEntityColumns(IEnumerable<ColumnInfo> columns)
    {
      if (EntityPrefetchTask == null)
        EntityPrefetchTask = new EntityPrefetchTask(Key, Type, exactType, processor);
      EntityPrefetchTask.AddColumns(columns);
    }

    public void RegisterReferencedEntityPrefetchTask(Tuple ownerEntityTuple, FieldInfo referencingField)
    {
      if (referencedEntityPrefetchTasks != null && referencedEntityPrefetchTasks.ContainsKey(referencingField))
        return;
      var notLoadedForeignKeyColumns = GetNotLoadedFieldColumns(ownerEntityTuple, referencingField);
      var areAllForeignKeyColumnsLoaded = notLoadedForeignKeyColumns.Count()==0;
      if (!areAllForeignKeyColumnsLoaded) {
        if (referencedEntityPrefetchTasks == null)
          referencedEntityPrefetchTasks = new Dictionary<FieldInfo, ReferencedEntityPrefetchTask>();
        referencedEntityPrefetchTasks.Add(referencingField, new ReferencedEntityPrefetchTask(Key,
          referencingField, exactType, processor));
        AddEntityColumns(notLoadedForeignKeyColumns);
      }
      else {
        var referencedKeyTuple = referencingField.Association.ExtractForeignKey(ownerEntityTuple);
        var referencedKey = Key.Create(referencingField.Association.TargetType,
          referencedKeyTuple, false);
        processor.PrefetchHierarchyRootColumns(referencedKey);
      }
    }

    public void RegisterEntitySetPrefetchTask(PrefetchFieldDescriptor referencingFieldDescriptor)
    {
      if (entitySetPrefetchTasks == null)
        entitySetPrefetchTasks = new Dictionary<FieldInfo, EntitySetPrefetchTask>();
      if (entitySetPrefetchTasks.ContainsKey(referencingFieldDescriptor.Field))
        return;
      if (EntityPrefetchTask == null)
        AddEntityColumns(Key.Hierarchy.Root.Fields
          .Where(PrefetchTask.IsFieldIntrinsicNonLazy).SelectMany(field => field.Columns));
      entitySetPrefetchTasks.Add(referencingFieldDescriptor.Field,
        new EntitySetPrefetchTask(Key, referencingFieldDescriptor, processor));
    }

    public bool Equals(PrefetchTaskContainer other)
    {
      if (ReferenceEquals(null, other))
        return false;
      if (ReferenceEquals(this, other))
        return true;
      if (!Type.Equals(other.Type))
        return false;
      return Key.Equals(other.Key);
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj))
        return false;
      if (ReferenceEquals(this, obj))
        return true;
      var otherType = obj.GetType();
      if (otherType != (typeof (PrefetchTaskContainer)))
        return false;
      return Equals((PrefetchTaskContainer) obj);
    }

    public override int GetHashCode()
    {
      if (cachedHashCode==null)
        unchecked {
          cachedHashCode = (Key.GetHashCode() * 397) ^ Type.GetHashCode();
        }
      return cachedHashCode.Value;
    }

    private IEnumerable<ColumnInfo> GetNotLoadedFieldColumns(Tuple tuple, FieldInfo field)
    {
      return field.Columns.Where(column => !IsColumnLoaded(tuple, column));
    }

    private bool IsColumnLoaded(Tuple tuple, ColumnInfo column)
    {
      var columnIndex = Type.Indexes.PrimaryIndex.Columns.IndexOf(column);
      return tuple!=null
        && tuple.GetFieldState(columnIndex).IsAvailable();
    }


    // Constructors

    public PrefetchTaskContainer(Key key, TypeInfo type, bool exactType, PrefetchProcessor processor)
    {
      ArgumentValidator.EnsureArgumentNotNull(key, "key");
      ArgumentValidator.EnsureArgumentNotNull(type, "type");
      ArgumentValidator.EnsureArgumentNotNull(processor, "processor");
      Key = key;
      Type = type;
      this.processor = processor;
      this.exactType = exactType;
    }
  }
}