// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.09.04

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core;
using Xtensive.Core.Tuples;
using Xtensive.Core.Tuples.Transform;
using Xtensive.Storage.Model;
using Xtensive.Storage.Resources;

namespace Xtensive.Storage.Internals
{
  [Serializable]
  internal sealed class ReferencedEntityPrefetchTask : PrefetchTask
  {
    private QueryTask queryFetchingColumns;
    private readonly MapTransform referencedEntityKeyTransform;
    private readonly TypeInfo rootType;
    private readonly Key ownerKey;
    private readonly bool isOwnerTypeKnown;

    public readonly FieldInfo ReferencingField;

    public override bool IsActive { get { return queryFetchingColumns != null; } }
    
    public override List<Tuple> Result
    {
      get { return queryFetchingColumns.Result; }
    }

    public override void RegisterQueryTask()
    {
      if (!IsActive) {
        EntityState ownerState;
        if (!Processor.Owner.TryGetEntityState(ownerKey, out ownerState)
          || ownerState.Tuple == null || ownerState.PersistenceState == PersistenceState.Removed)
          throw new KeyNotFoundException(
            String.Format(Strings.ExReferencingEntityWithKeyXIsNotFound, ownerKey));
        if (!ownerState.IsTupleLoaded)
          throw Exceptions.InternalError(Strings.ExReferencingEntityTupleIsNotLoaded, Log.Instance);
        var foreignKeyTuple = ReferencingField.Association.ExtractForeignKey(ownerState.Tuple);
        for (int i = 0; i < foreignKeyTuple.Count; i++) {
          if (!foreignKeyTuple.GetFieldState(i).IsAvailable())
            if (isOwnerTypeKnown)
              throw Exceptions.InternalError(Strings.ExForeignKeyValueHaveNotBeenLoaded, Log.Instance);
            else
              return;
          if ((foreignKeyTuple.GetFieldState(i) & TupleFieldState.Null)==TupleFieldState.Null)
            return;
        }
        Key = Key.Create(rootType, foreignKeyTuple, false);
        if (!TryActivate())
          return;
        queryFetchingColumns = CreateQueryTask(Key);
      }
      Processor.Owner.Session.RegisterDelayedQuery(queryFetchingColumns);
    }


    // Constructors

    public ReferencedEntityPrefetchTask(Key ownerKey, FieldInfo referencingField, bool isOwnerTypeKnown,
      PrefetchProcessor processor)
      : base(null, referencingField.Association.TargetType, true, processor)
    {
      ArgumentValidator.EnsureArgumentNotNull(referencingField, "referencingField");
      ArgumentValidator.EnsureArgumentNotNull(ownerKey, "ownerKey");
      rootType = referencingField.Association.TargetType;
      this.ownerKey = ownerKey;
      ReferencingField = referencingField;
      this.isOwnerTypeKnown = isOwnerTypeKnown;
      var fieldsToBeLoaded = rootType.Fields.Where(IsFieldIntrinsicNonLazy);
      foreach (var field in fieldsToBeLoaded)
        AddColumns(field.Columns);
    }
  }
}