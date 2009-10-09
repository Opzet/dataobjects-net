// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2008.11.02

using System.Collections.Generic;
using System.Linq;
using Xtensive.Core;
using Xtensive.Core.Caching;
using Xtensive.Core.Tuples;
using Xtensive.Core.Tuples.Transform;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse;
using Xtensive.Core.Collections;

namespace Xtensive.Storage.Internals
{
  internal class RecordSetReader : DomainBound
  {
    private readonly ICache<RecordSetHeader, RecordSetMapping> cache;

    public Record ReadSingleRow(IEnumerable<Tuple> source, RecordSetHeader header, Key key)
    {
      var item = source.FirstOrDefault();
      if (item==null)
        return null;

      if (key != null && key.IsTypeCached) {
        var typeMapping = GetMapping(header).Mappings[0].GetTypeMapping(key.Type.TypeId);
        var entityTuple = typeMapping.Transform.Apply(TupleTransformType.Tuple, item);
        return new Record(item, new Pair<Key, Tuple>(key, entityTuple));
      }
      return ParseRow(item, GetMapping(header));
    }

    public IEnumerable<Record> Read(IEnumerable<Tuple> source, RecordSetHeader header)
    {
      var mapping = GetMapping(header);
      foreach (var item in source)
        yield return ParseRow(item, mapping);
    }

    private Record ParseRow(Tuple tuple, RecordSetMapping mapping)
    {
      var count = mapping.Mappings.Count;

      if (count == 1)
        return new Record(tuple, ParseColumnGroup(tuple, mapping.Mappings[0]));

      var pairs = new List<Pair<Key, Tuple>>(count);
      foreach (var groupMapping in mapping.Mappings)
        pairs.Add(ParseColumnGroup(tuple, groupMapping));

      return new Record(tuple, pairs);
    }

    private Pair<Key, Tuple> ParseColumnGroup(Tuple tuple, ColumnGroupMapping groupMapping)
    {
      var rootType = groupMapping.Type;
      bool exactType;
      int typeId = ExtractTypeId(rootType, tuple, groupMapping.TypeIdColumnIndex, out exactType);
      var typeMapping = typeId==TypeInfo.NoTypeId ? null : groupMapping.GetTypeMapping(typeId);
      if (typeMapping==null)
        return new Pair<Key, Tuple>(null, null);

      Key key;
      var entityType = exactType ? typeMapping.Type : rootType;
      if (typeMapping.KeyTransform.Descriptor.Count <= WellKnown.MaxGenericKeyLength)
        key = KeyFactory.Create(Domain, entityType, tuple, typeMapping.KeyIndexes, exactType, exactType);
      else {
        var keyTuple = typeMapping.KeyTransform.Apply(TupleTransformType.TransformedTuple, tuple);
        key = KeyFactory.Create(Domain, entityType, keyTuple, null, exactType, exactType);
      }
      if (exactType) {
        var entityTuple = typeMapping.Transform.Apply(TupleTransformType.Tuple, tuple);
        return new Pair<Key, Tuple>(key, entityTuple);
      }
      return new Pair<Key, Tuple>(key, null);
    }

    internal static int ExtractTypeId(TypeInfo type, Tuple tuple, int typeIdIndex, out bool exactType)
    {
      if (typeIdIndex < 0) {
        exactType = type.IsLeaf;
        return type.TypeId;
      }
      exactType = true;
      TupleFieldState state;
      var value = tuple.GetValue<int>(typeIdIndex, out state);
      if (type.IsLeaf)
        return state.HasValue() ? type.TypeId : TypeInfo.NoTypeId;
      // Hack here: 0 (default) = TypeInfo.NoTypeId
      return value;
    }

    internal RecordSetMapping GetMapping(RecordSetHeader header)
    {
      RecordSetMapping result;
      lock (cache) {
        result = cache[header, true];
        if (result != null)
          return result;
      }
      var mappings = new List<ColumnGroupMapping>();
      var typeIdColumnName = Domain.NameBuilder.TypeIdColumnName;

      foreach (var group in header.ColumnGroups) {
        var model = Domain.Model;
        int typeIdColumnIndex = -1;
        var columnMapping = new Dictionary<ColumnInfo, MappedColumn>(group.Columns.Count);
        var type = group.TypeInfoRef.Resolve(model);

        foreach (int columnIndex in group.Columns) {
          var column = (MappedColumn)header.Columns[columnIndex];
          ColumnInfo columnInfo = column.ColumnInfoRef.Resolve(model);
          columnMapping[columnInfo] = column;
          if (columnInfo.Name == typeIdColumnName)
            typeIdColumnIndex = column.Index;
        }

        var implementors = (type.IsInterface 
          ? type.GetImplementors(true)
          : type.GetDescendants(true)).AddOne(type).ToList();
        var typeMappings = new IntDictionary<TypeMapping>(implementors.Count + 1);
        foreach (TypeInfo childType in implementors) {
          // Building typeMap
          var columnCount = childType.Columns.Count;
          var typeMap = new int[columnCount];
          for (int i = 0; i < columnCount; i++) {
            var columnInfo = childType.Columns[i];
            MappedColumn column;
            if (columnMapping.TryGetValue(columnInfo, out column))
              typeMap[i] = column.Index;
            else
              typeMap[i] = MapTransform.NoMapping;
          }
  
          // Building keyMap
          var columns = childType.KeyInfo.Columns;
          columnCount = columns.Count;
          var keyMap = new int[columnCount];
          bool hasKey = false;
          for (int i = 0; i < columnCount; i++) {
            var columnInfo = columns[i];
            MappedColumn column;
            if (columnMapping.TryGetValue(columnInfo, out column)) {
              keyMap[i] = column.Index;
              hasKey = true;
            }
            else
              keyMap[i] = MapTransform.NoMapping;
          }
          if (hasKey) {
            var typeMapping = new TypeMapping(childType,
              new MapTransform(true, childType.KeyInfo.TupleDescriptor, keyMap),
              new MapTransform(true, childType.TupleDescriptor, typeMap));
            typeMappings.Add(childType.TypeId, typeMapping);
          }
        }
        var mapping =  new ColumnGroupMapping(type, typeIdColumnIndex, typeMappings);
        mappings.Add(mapping);
      }
      result = new RecordSetMapping(Domain, header, mappings);
      lock (cache) {
        cache.Add(result);
      }
      return result;
    }


    // Constructors

    internal RecordSetReader(Domain domain)
      : base(domain)
    {
      cache = 
        new LruCache<RecordSetHeader, RecordSetMapping>(
          domain.Configuration.RecordSetMappingCacheSize, 
          m => m.Header,
          new WeakestCache<RecordSetHeader, RecordSetMapping>(false, false, 
            m => m.Header));
    }
  }
}