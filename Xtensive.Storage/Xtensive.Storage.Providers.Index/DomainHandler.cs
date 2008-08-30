// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2008.07.01

using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Comparison;
using Xtensive.Core.Tuples;
using Xtensive.Core.Tuples.Transform;
using Xtensive.Indexing;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse;

namespace Xtensive.Storage.Providers.Index
{
  public class DomainHandler: Providers.DomainHandler
  {
    private readonly Dictionary<IndexInfo, IUniqueOrderedIndex<Tuple, Tuple>> realIndexes = new Dictionary<IndexInfo, IUniqueOrderedIndex<Tuple, Tuple>>();
    private readonly Dictionary<Pair<IndexInfo,TypeInfo>, MapTransform> indexTransforms = new Dictionary<Pair<IndexInfo, TypeInfo>, MapTransform>();

    /// <inheritdoc/>
    protected override Rse.Compilation.CompilationContext BuildCompilationContext()
    {
      return new CompilationContext(new Compilers.Compiler(Handlers));
    }

    /// <inheritdoc/>
    public override void Build()
    {
      BuildRealIndexes();
      foreach (var pair in Handlers.Domain.Model.Types.SelectMany(type => type.Indexes.Where(i => i.ReflectedType==type).Union(type.AffectedIndexes).Distinct().Select(i => new Pair<IndexInfo, TypeInfo>(i, type)))) {
        MapTransform transform = BuildIndexTransform(pair.First, pair.Second);
        indexTransforms.Add(pair, transform);
      }
    }

    #region Private / internal methods

    internal IUniqueOrderedIndex<Tuple, Tuple> GetRealIndex(IndexInfoRef indexInfoRef)
    {
      var index = indexInfoRef.Resolve(Handlers.Domain.Model);
      return realIndexes[index];
    }

    internal IUniqueOrderedIndex<Tuple, Tuple> GetRealIndex(IndexInfo indexInfo)
    {
      return realIndexes[indexInfo];
    }

    internal MapTransform GetIndexTransform(IndexInfo indexInfo, TypeInfo type)
    {
      return indexTransforms[new Pair<IndexInfo, TypeInfo>(indexInfo, type)];
    }

    private static MapTransform BuildIndexTransform(IndexInfo indexInfo, TypeInfo type)
    {
      TupleDescriptor descriptor = TupleDescriptor.Create(indexInfo.Columns.Select(c => c.ValueType));
      int[] map = indexInfo.Columns
        .Select(c => {
          var column = type.Columns.TryGetValue(c.Field.Column.Name);
          return column==null ? MapTransform.NoMapping : column.Field.MappingInfo.Offset; 
        }).ToArray();
      return new MapTransform(true, descriptor, map);
    }

    private void BuildRealIndexes()
    {
      foreach (IndexInfo indexInfo in Handlers.Domain.Model.RealIndexes) {
        var indexConfig = new IndexConfiguration<Tuple, Tuple>();
        DirectionCollection<ColumnInfo> orderingRule;
        if (indexInfo.IsUnique | indexInfo.IsPrimary)
          orderingRule = new DirectionCollection<ColumnInfo>(indexInfo.KeyColumns);
        else
          orderingRule = new DirectionCollection<ColumnInfo>(
            indexInfo.KeyColumns
              .Union(indexInfo.ValueColumns.Select(info => new KeyValuePair<ColumnInfo, Direction>(info, Direction.Positive))));
        indexConfig.KeyComparer = AdvancedComparer<Tuple>.Default.ApplyRules(new ComparisonRules(
          ComparisonRule.Positive,
          orderingRule.Select(pair => (ComparisonRules)new ComparisonRule(pair.Value, CultureInfo.InvariantCulture)).ToArray(),
          ComparisonRules.None));
        indexConfig.KeyExtractor = input => input;
        IUniqueOrderedIndex<Tuple, Tuple> index = IndexFactory.CreateUniqueOrdered<Tuple, Tuple, Index<Tuple, Tuple>>(indexConfig);
        realIndexes[indexInfo] = index;
      }
    }

    #endregion
  }
}