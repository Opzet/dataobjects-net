// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2008.07.14

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core.Tuples;
using Xtensive.Sql.Dom;
using Xtensive.Sql.Dom.Dml;
using Xtensive.Storage.Rse.Compilation;
using Xtensive.Storage.Rse.Providers;
using Xtensive.Storage.Rse.Providers.Compilable;
using SqlFactory = Xtensive.Sql.Dom.Sql;

namespace Xtensive.Storage.Providers.Sql.Compilers
{
  public class JoinProviderCompiler : TypeCompiler<JoinProvider>
  {
    /// <inheritdoc/>
    protected override ExecutableProvider Compile(JoinProvider provider)
    {
      var left = provider.Left.Compile() as SqlProvider;
      var right = provider.Right.Compile() as SqlProvider;

      if (left == null || right == null)
        return null;
      var leftQuery = SqlFactory.QueryRef(left.Request.Statement as SqlSelect);
      var rightQuery = SqlFactory.QueryRef(right.Request.Statement as SqlSelect);
      var joinedTable = SqlFactory.Join(
        provider.LeftJoin ? SqlJoinType.LeftOuterJoin : SqlJoinType.InnerJoin,
        leftQuery,
        rightQuery,
        provider.EqualIndexes
          .Select(pair => leftQuery.Columns[pair.First] == rightQuery.Columns[pair.Second])
          .Aggregate(null as SqlExpression, (expression,binary) => expression & binary)
        );

      SqlSelect query = SqlFactory.Select(joinedTable);
      query.Columns.AddRange(leftQuery.Columns.Union(rightQuery.Columns).Cast<SqlColumn>());
      //      if (!SqlExpression.IsNull(left.Query.Where))
      //        query.Where &= left.Query.Where;
      //      if (!SqlExpression.IsNull(right.Query.Where))
      //        query.Where &= right.Query.Where;
      SqlQueryRequest request = new SqlQueryRequest(query, provider.Header.TupleDescriptor, left.Request.ParameterBindings.Union(right.Request.ParameterBindings));
      return new SqlProvider(provider, request, Handlers);
    }


    // Constructors

    public JoinProviderCompiler(Rse.Compilation.Compiler provider)
      : base(provider)
    {
    }
  }
}