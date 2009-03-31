// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.02.27

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Linq;
using Xtensive.Core.Parameters;
using Xtensive.Core.Reflection;
using Xtensive.Core.Tuples;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse;
using Xtensive.Storage.Rse.Providers;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Linq
{
  internal sealed partial class Translator : MemberPathVisitor
  {
    private readonly TranslatorContext context;

    public ResultExpression Translate()
    {
      using (new ParameterScope()) {
        joinFinalEntity.Value = false;
        calculateExpressions.Value = false;
        recordIsUsed.Value = false;
        return (ResultExpression) Visit(context.Query);
      }
    }

    public static Dictionary<string, Segment<int>> BuildFieldMapping(TypeInfo type, int offset)
    {
      var fieldMapping = new Dictionary<string, Segment<int>>();
      foreach (var field in type.Fields) {
        fieldMapping.Add(field.Name, new Segment<int>(offset + field.MappingInfo.Offset, field.MappingInfo.Length));
        if (field.IsEntity)
          fieldMapping.Add(field.Name + ".Key", new Segment<int>(offset + field.MappingInfo.Offset, field.MappingInfo.Length));
      }
      var keySegment = new Segment<int>(offset, type.Hierarchy.KeyInfo.Fields.Sum(pair => pair.Key.MappingInfo.Length));
      fieldMapping.Add("Key", keySegment);

      return fieldMapping;
    }

    protected override Expression VisitConstant(ConstantExpression c)
    {
      if (c.Value==null)
        return c;
      var rootPoint = c.Value as IQueryable;
      if (rootPoint!=null)
        return ConstructQueryable(rootPoint);
      return base.VisitConstant(c);
    }

    protected override Expression VisitQueryableMethod(MethodCallExpression mc, QueryableMethodKind methodKind)
    {
      switch (methodKind) {
        case QueryableMethodKind.AsEnumerable:
          break;
        case QueryableMethodKind.AsQueryable:
          break;
        case QueryableMethodKind.ToArray:
          break;
        case QueryableMethodKind.ToList:
          break;
        case QueryableMethodKind.Cast:
          break;
        case QueryableMethodKind.OfType:
          break;
        case QueryableMethodKind.Aggregate:
          break;
        case QueryableMethodKind.ElementAt:
          break;
        case QueryableMethodKind.ElementAtOrDefault:
          break;
        case QueryableMethodKind.Last:
          break;
        case QueryableMethodKind.LastOrDefault:
          break;
        case QueryableMethodKind.Except:
          break;
        case QueryableMethodKind.Intersect:
          break;
        case QueryableMethodKind.Concat:
          break;
        case QueryableMethodKind.Union:
          break;
        case QueryableMethodKind.Reverse:
          break;
        case QueryableMethodKind.SequenceEqual:
          break;
        case QueryableMethodKind.DefaultIfEmpty:
          break;
        case QueryableMethodKind.SkipWhile:
          break;
        case QueryableMethodKind.TakeWhile:
          break;
        case QueryableMethodKind.All:
          if (mc.Arguments.Count==2)
            return VisitAll(mc.Arguments[0], mc.Arguments[1].StripQuotes(), context.IsRoot(mc));
          break;
        case QueryableMethodKind.Any:
          if (mc.Arguments.Count==1)
            return VisitAny(mc.Arguments[0], null, context.IsRoot(mc));
          if (mc.Arguments.Count==2)
            return VisitAny(mc.Arguments[0], mc.Arguments[1].StripQuotes(), context.IsRoot(mc));
          break;
        case QueryableMethodKind.Contains:
          if (mc.Arguments.Count==2)
            return VisitContains(mc.Arguments[0], mc.Arguments[1], context.IsRoot(mc));
          break;
        case QueryableMethodKind.Distinct:
          if (mc.Arguments.Count==1)
            return VisitDistinct(mc.Arguments[0]);
          break;
        case QueryableMethodKind.First:
        case QueryableMethodKind.FirstOrDefault:
        case QueryableMethodKind.Single:
        case QueryableMethodKind.SingleOrDefault:
          if (mc.Arguments.Count==1) {
            return VisitFirstSingle(mc.Arguments[0], null, mc.Method, context.IsRoot(mc));
          }
          if (mc.Arguments.Count==2) {
            LambdaExpression predicate = (mc.Arguments[1].StripQuotes());
            return VisitFirstSingle(mc.Arguments[0], predicate, mc.Method, context.IsRoot(mc));
          }
          break;
        case QueryableMethodKind.GroupBy:
          if (mc.Arguments.Count==2) {
            return VisitGroupBy(
              mc.Method,
              mc.Arguments[0],
              mc.Arguments[1].StripQuotes(),
              null,
              null
              );
          }
          if (mc.Arguments.Count==3) {
            LambdaExpression lambda1 = mc.Arguments[1].StripQuotes();
            LambdaExpression lambda2 = mc.Arguments[2].StripQuotes();
            if (lambda2.Parameters.Count==1) {
              // second lambda is element selector
              return VisitGroupBy(
                mc.Method,
                mc.Arguments[0],
                lambda1,
                lambda2,
                null);
            }
            if (lambda2.Parameters.Count==2) {
              // second lambda is result selector
              return VisitGroupBy(
                mc.Method,
                mc.Arguments[0],
                lambda1,
                null,
                lambda2);
            }
          }
          else if (mc.Arguments.Count==4) {
            return VisitGroupBy(
              mc.Method,
              mc.Arguments[0],
              mc.Arguments[1].StripQuotes(),
              mc.Arguments[2].StripQuotes(),
              mc.Arguments[3].StripQuotes()
              );
          }
          break;
        case QueryableMethodKind.GroupJoin:
          return VisitGroupJoin(
            mc.Type, mc.Arguments[0], mc.Arguments[1],
            mc.Arguments[2].StripQuotes(),
            mc.Arguments[3].StripQuotes(),
            mc.Arguments[4].StripQuotes());
        case QueryableMethodKind.Join:
          return VisitJoin(mc.Arguments[0], mc.Arguments[1],
            mc.Arguments[2].StripQuotes(),
            mc.Arguments[3].StripQuotes(),
            mc.Arguments[4].StripQuotes());
        case QueryableMethodKind.OrderBy:
          return VisitOrderBy(mc.Arguments[0], mc.Arguments[1].StripQuotes(), Direction.Positive);
        case QueryableMethodKind.OrderByDescending:
          return VisitOrderBy(mc.Arguments[0], mc.Arguments[1].StripQuotes(), Direction.Negative);
        case QueryableMethodKind.Select:
          return VisitSelect(mc.Arguments[0], mc.Arguments[1].StripQuotes());
        case QueryableMethodKind.SelectMany:
          if (mc.Arguments.Count==2)
            return VisitSelectMany(
              mc.Type, mc.Arguments[0],
              mc.Arguments[1].StripQuotes(),
              null);
          if (mc.Arguments.Count==3)
            return VisitSelectMany(
              mc.Type, mc.Arguments[0],
              mc.Arguments[1].StripQuotes(),
              mc.Arguments[2].StripQuotes());
          break;
        case QueryableMethodKind.LongCount:
        case QueryableMethodKind.Count:
        case QueryableMethodKind.Max:
        case QueryableMethodKind.Min:
        case QueryableMethodKind.Sum:
        case QueryableMethodKind.Average:
          if (mc.Arguments.Count == 1)
            return VisitAggregate(mc.Arguments[0], mc.Method, null, context.IsRoot(mc));
          if (mc.Arguments.Count == 2)
            return VisitAggregate(mc.Arguments[0], mc.Method, mc.Arguments[1].StripQuotes(), context.IsRoot(mc));
          break;
        case QueryableMethodKind.Skip:
          if (mc.Arguments.Count==2)
            return VisitSkip(mc.Arguments[0], mc.Arguments[1]);
          break;
        case QueryableMethodKind.Take:
          if (mc.Arguments.Count==2)
            return VisitTake(mc.Arguments[0], mc.Arguments[1]);
          break;
        case QueryableMethodKind.ThenBy:
          return VisitThenBy(mc.Arguments[0], mc.Arguments[1].StripQuotes(), Direction.Positive);
        case QueryableMethodKind.ThenByDescending:
          return VisitThenBy(mc.Arguments[0], mc.Arguments[1].StripQuotes(), Direction.Negative);
        case QueryableMethodKind.Where:
          return VisitWhere(mc.Arguments[0], mc.Arguments[1].StripQuotes());
        default:
          throw new ArgumentOutOfRangeException("methodKind");
      }
      throw new NotSupportedException();
    }

    private Expression VisitContains(Expression source, Expression match, bool isRoot)
    {
      var p = Expression.Parameter(match.Type, "p");
      var le = Expression.Lambda(Expression.Equal(p, match), p);

      if (isRoot)
        return VisitRootExists(source, le, false);

      if (source.IsQuery())
        return VisitExists(source, le, false);

      throw new NotImplementedException();
    }

    private Expression VisitAll(Expression source, LambdaExpression predicate, bool isRoot)
    {
      predicate = Expression.Lambda(Expression.Not(predicate.Body), predicate.Parameters[0]);

      if (isRoot)
        return VisitRootExists(source, predicate, true);

      if (source.IsQuery())
        return VisitExists(source, predicate, true);

      throw new NotImplementedException();
    }

    private Expression VisitAny(Expression source, LambdaExpression predicate, bool isRoot)
    {
      if (isRoot)
        return VisitRootExists(source, predicate, false);

      if (source.IsQuery())
        return VisitExists(source, predicate, false);

      throw new NotImplementedException();
    }

    private Expression VisitFirstSingle(Expression source, LambdaExpression predicate, MethodInfo method, bool isRoot)
    {
      if (!isRoot)
        throw new NotImplementedException();
      ResultExpression result = predicate!=null
        ? (ResultExpression) VisitWhere(source, predicate)
        : (ResultExpression) Visit(source);
      RecordSet recordSet = null;
      switch (method.Name) {
        case WellKnown.Queryable.First:
        case WellKnown.Queryable.FirstOrDefault:
          recordSet = result.RecordSet.Take(1);
          break;
        case WellKnown.Queryable.Single:
        case WellKnown.Queryable.SingleOrDefault:
          recordSet = result.RecordSet.Take(2);
          break;
      }
      var enumerableType = typeof (Enumerable);
      MethodInfo enumerableMethod = enumerableType
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        .First(m => m.Name==method.Name && m.GetParameters().Length==1)
        .MakeGenericMethod(method.ReturnType);
      var lambda = BuildProjector(result.ItemProjector, false);
      var projector = Expression.Lambda(
        Expression.Convert(Expression.Call(null, enumerableMethod, lambda.Body), typeof (object)),
        lambda.Parameters.ToArray());
      return new ResultExpression(method.ReturnType, recordSet, result.Mapping, (Expression<Func<RecordSet,object>>)projector, null);
    }

    private Expression VisitTake(Expression source, Expression take)
    {
      var projection = (ResultExpression) Visit(source);
      var parameter = context.ParameterExtractor.ExtractParameter<int>(take);
      var rs = projection.RecordSet.Take(parameter.Compile());
      return new ResultExpression(projection.Type, rs, projection.Mapping, projection.Projector, projection.ItemProjector);
    }

    private Expression VisitSkip(Expression source, Expression skip)
    {
      var projection = (ResultExpression) Visit(source);
      var parameter = context.ParameterExtractor.ExtractParameter<int>(skip);
      var rs = projection.RecordSet.Skip(parameter.Compile());
      return new ResultExpression(projection.Type, rs, projection.Mapping, projection.Projector, projection.ItemProjector);
    }

    private Expression VisitDistinct(Expression expression)
    {
      var result = (ResultExpression) Visit(expression);
      var rs = result.RecordSet.Distinct();
      return new ResultExpression(result.Type, rs, result.Mapping, result.Projector, result.ItemProjector);
    }

    private Expression VisitAggregate(Expression source, MethodInfo method, LambdaExpression argument, bool isRoot)
    {
      bool isLongCount = false;
      bool isIntCount = false;
      int aggregateColumn;
      AggregateType aggregateType;
      ResultExpression innerResult;

      switch (method.Name) {
        case WellKnown.Queryable.Count:
          isIntCount = true;
          aggregateType = AggregateType.Count;
          break;
        case WellKnown.Queryable.LongCount:
          isLongCount = true;
          aggregateType = AggregateType.Count;
          break;
        case WellKnown.Queryable.Min:
          aggregateType = AggregateType.Min;
          break;
        case WellKnown.Queryable.Max:
          aggregateType = AggregateType.Max;
          break;
        case WellKnown.Queryable.Sum:
          aggregateType = AggregateType.Sum;
          break;
        case WellKnown.Queryable.Average:
          aggregateType = AggregateType.Avg;
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      if (aggregateType == AggregateType.Count) {
        aggregateColumn = 0;
        if (argument != null)
          innerResult = (ResultExpression)VisitWhere(source, argument);
        else
          innerResult = (ResultExpression)Visit(source);
      }
      else {
        innerResult = (ResultExpression)Visit(source);
        var columnList = new List<int>();

        if (argument == null) {
          if (innerResult.Mapping.Segment.Length > 1 ||
              innerResult.ItemProjector.Body.Type != innerResult.RecordSet.Header.Columns[innerResult.Mapping.Segment.Offset].Type)
            throw new NotSupportedException();
          columnList.Add(innerResult.Mapping.Segment.Offset);
        }
        else {
          using (context.Bindings.Add(argument.Parameters[0], innerResult))
          using (new ParameterScope()) {
            resultMapping.Value = new ResultMapping();
            Visit(argument);
            columnList = resultMapping.Value.GetColumns().ToList();
            innerResult = context.Bindings[argument.Parameters[0]];
          }
        }

        if (columnList.Count != 1)
          throw new NotSupportedException();
        aggregateColumn = columnList[0];
      }

      var innerRecordSet = innerResult.RecordSet.Aggregate(null,
        new AggregateColumnDescriptor(context.GetNextColumnAlias(), aggregateColumn, aggregateType));

      if (!isRoot) {
        var expression = ApplyOneColumnSubquery(innerRecordSet);
        if (isIntCount)
          expression = Expression.Convert(expression, typeof (int));
        return expression;
      }

      Expression<Func<RecordSet, object>> shaper;
      if (isLongCount)
        shaper = set => (set.First().GetValue<long>(0));
      else if (isIntCount)
        shaper = set => (int)(set.First().GetValue<long>(0));
      else
        shaper = set => set.First().GetValueOrDefault(0);
      return new ResultExpression(innerResult.Type, innerRecordSet, null, shaper, null);
    }

    private Expression VisitGroupBy(MethodInfo method, Expression source, LambdaExpression keySelector, LambdaExpression elementSelector, LambdaExpression resultSelector)
    {
      var result = (ResultExpression) Visit(source);

      List<int> columnList;
      var newResultMapping = new ResultMapping();
      LambdaExpression originalCompiledKeyExpression;
      using (context.Bindings.Add(keySelector.Parameters[0], result))
      using (new ParameterScope()) {
        resultMapping.Value = new ResultMapping();
        originalCompiledKeyExpression = (LambdaExpression)Visit(keySelector);
        columnList = resultMapping.Value.GetColumns().ToList();
        
        result = context.Bindings[keySelector.Parameters[0]];

        if (!resultMapping.Value.MapsToPrimitive) {
          var keyMapping = new ResultMapping();
          newResultMapping.JoinedRelations.Add("Key", keyMapping);
          foreach (var field in resultMapping.Value.Fields) {
            var segment = new Segment<int>(columnList.IndexOf(field.Value.Offset), field.Value.Length);
            newResultMapping.RegisterFieldMapping("Key." + field.Key, segment);
            keyMapping.RegisterFieldMapping(field.Key, segment);
          }
        }
        else
          newResultMapping.RegisterFieldMapping("Key", new Segment<int>(columnList.IndexOf(resultMapping.Value.Segment.Offset), resultMapping.Value.Segment.Length));
      }

      var recordSet = result.RecordSet.Aggregate(columnList.ToArray());

      var resultGroupingType = method.ReturnType.GetGenericArguments()[0];
      Type[] groupingArguments = resultGroupingType.GetGenericArguments();
      var keyType = groupingArguments[0];
      var elementType = groupingArguments[1];
      var parameterGroupingType = typeof (Grouping<,>).MakeGenericType(keyType, elementType);
      var constructor = parameterGroupingType.GetConstructor(new[] {keyType, typeof (IEnumerable<>).MakeGenericType(elementType)});

      // Remap 
      var tupleAccessProcessor = new TupleAccessProcessor();
      var groupMapping = result.RecordSet.Header.ColumnGroups
        .Select((cg,i) => new {Group = cg, Index = i})
        .Where(gi => gi.Group.Keys.All(columnList.Contains))
        .Select(gi => gi.Index)
        .ToList();
      var remappedExpression = (LambdaExpression)tupleAccessProcessor.ReplaceMappings(originalCompiledKeyExpression, columnList, groupMapping, recordSet.Header);

      // record => new Grouping<TKey, TElement>(record.Key, source.Where(groupingItem => groupingItem.Key == record.Key))
      var pRecord = Expression.Parameter(typeof (Record), "record");
      var pTuple = Expression.Parameter(typeof (Tuple), "tuple");
      var parameterRewriter = new ParameterRewriter(pTuple, pRecord);
      var recordKeyExpression = parameterRewriter.Rewrite(remappedExpression.Body);

      Expression leftKeySelector = keySelector.Body;
      Expression rightKeySelector = recordKeyExpression.First;
      Expression groupingKeyResolver = rightKeySelector;

      var predicateExpression = Expression.Lambda(Expression.Equal(leftKeySelector, rightKeySelector), keySelector.Parameters.ToArray());


      var callMehtod = WellKnownMethods.QueryableWhere.MakeGenericMethod(elementType);

      var queryExpression = Expression.Call(callMehtod, source, predicateExpression);
      var projectorBody = Expression.New(constructor, groupingKeyResolver, queryExpression);
      var itemProjector = Expression.Lambda(projectorBody, recordKeyExpression.Second
        ? new[] {pTuple, pRecord}
        : new[] {pTuple});

      var rs = Expression.Parameter(typeof (RecordSet), "rs");
      Expression<Func<RecordSet, object>> projector;
      if (itemProjector.Parameters.Count > 1) {
        var makeProjectionMethod = typeof (Translator)
          .GetMethod("MakeProjection", BindingFlags.NonPublic | BindingFlags.Static)
          .MakeGenericMethod(itemProjector.Body.Type);
        projector = Expression.Lambda<Func<RecordSet, object>>(
          Expression.Convert(
            Expression.Call(makeProjectionMethod, rs, itemProjector),
            typeof (object)),
          rs);
      }
      else {
        var makeProjectionMethod = WellKnownMethods.EnumerableSelect.MakeGenericMethod(typeof (Tuple), itemProjector.Body.Type);
        projector = Expression.Lambda<Func<RecordSet, object>>(Expression.Convert(Expression.Call(makeProjectionMethod, rs, itemProjector), typeof (object)), rs);
      }

      return new ResultExpression(method.ReturnType, recordSet, newResultMapping, projector, itemProjector); //      Expression result = null;
    }

    private Expression VisitOrderBy(Expression expression, LambdaExpression le, Direction direction)
    {
      using (context.Bindings.Add(le.Parameters[0], (ResultExpression) Visit(expression)))
      using (new ParameterScope()) {
        resultMapping.Value = new ResultMapping();
        calculateExpressions.Value = true;
        Visit(le);
        var orderItems = resultMapping.Value.GetColumns()
          .Select(ci => new KeyValuePair<int, Direction>(ci, direction));
        var dc = new DirectionCollection<int>(orderItems);
        var result = context.Bindings[le.Parameters[0]];
        var rs = result.RecordSet.OrderBy(dc);
        return new ResultExpression(result.Type, rs, result.Mapping, result.Projector, result.ItemProjector);
      }
    }

    private Expression VisitThenBy(Expression expression, LambdaExpression le, Direction direction)
    {
      using (context.Bindings.Add(le.Parameters[0], (ResultExpression) Visit(expression)))
      using (new ParameterScope()) {
        resultMapping.Value = new ResultMapping();
        calculateExpressions.Value = true;
        Visit(le);
        var orderItems = resultMapping.Value.GetColumns()
          .Select(ci => new KeyValuePair<int, Direction>(ci, direction));
        var result = context.Bindings[le.Parameters[0]];
        var dc = ((SortProvider) result.RecordSet.Provider).Order;
        foreach (var item in orderItems) {
          if (!dc.ContainsKey(item.Key))
            dc.Add(item);
        }
        return result;
      }
    }

    private Expression VisitJoin(Expression outerSource, Expression innerSource, LambdaExpression outerKey, LambdaExpression innerKey, LambdaExpression resultSelector)
    {
      var outerParameter = outerKey.Parameters[0];
      var innerParameter = innerKey.Parameters[0];
      using (context.Bindings.Add(outerParameter, (ResultExpression) Visit(outerSource)))
      using (context.Bindings.Add(innerParameter, (ResultExpression) Visit(innerSource))) {
        var outerMapping = new ResultMapping();
        var innerMapping = new ResultMapping();
        using (new ParameterScope()) {
          resultMapping.Value = outerMapping;
          Visit(outerKey);
          resultMapping.Value = innerMapping;
          Visit(innerKey);
        }
        var keyPairs = outerMapping.GetColumns().ZipWith(innerMapping.GetColumns(), (o, i) => new Pair<int>(o, i)).ToArray();

        var outer = context.Bindings[outerParameter];
        var inner = context.Bindings[innerParameter];
        var recordSet = outer.RecordSet.Join(inner.RecordSet.Alias(context.GetNextAlias()), keyPairs);
        return CombineResultExpressions(outer, inner, recordSet, resultSelector);
      }
    }

    private Expression CombineResultExpressions(ResultExpression outer, ResultExpression inner,
      RecordSet recordSet, LambdaExpression resultSelector)
    {
      var outerLength = outer.RecordSet.Header.Length;
      var innerLength = inner.RecordSet.Header.Length;

      var tupleAccessProcessor = new TupleAccessProcessor();
      var tupleMapping = new List<int>(
        Enumerable.Repeat(-1, outerLength).Concat(Enumerable.Range(0, innerLength))
        );
      var groupMapping = new List<int>(
        Enumerable.Repeat(-1, outer.RecordSet.Header.ColumnGroups.Count)
          .Concat(Enumerable.Range(0, inner.RecordSet.Header.ColumnGroups.Count))
        );

      outer = new ResultExpression(outer.Type, recordSet, outer.Mapping, outer.Projector, outer.ItemProjector);
      var innerProjector = (Expression<Func<RecordSet, object>>) tupleAccessProcessor.ReplaceMappings(inner.Projector, tupleMapping, groupMapping, recordSet.Header);
      var innerItemProjector = (LambdaExpression)tupleAccessProcessor.ReplaceMappings(inner.ItemProjector, tupleMapping, groupMapping, recordSet.Header);
      inner = new ResultExpression(inner.Type, recordSet, inner.Mapping.ShiftOffset(outerLength), innerProjector, innerItemProjector);

      using (context.Bindings.Add(resultSelector.Parameters[0], outer))
      using (context.Bindings.Add(resultSelector.Parameters[1], inner)) {
        return BuildProjection(resultSelector);
      }
    }

    private Expression VisitGroupJoin(Type resultType, Expression outerSource, Expression innerSource, LambdaExpression outerKey, LambdaExpression innerKey, LambdaExpression resultSelector)
    {
      throw new NotImplementedException();
    }

    private Expression VisitSelectMany(Type resultType, Expression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
    {
      var parameter = collectionSelector.Parameters[0];
      using (context.Bindings.Add(parameter, (ResultExpression) Visit(source))) {
        var outerResult = context.Bindings[parameter];
        bool isOuter = false;
        if (collectionSelector.Body.NodeType==ExpressionType.Call) {
          var call = (MethodCallExpression) collectionSelector.Body;
          isOuter = call.Method.IsGenericMethod
            && call.Method.GetGenericMethodDefinition()==WellKnownMethods.QueryableDefaultIfEmpty;
          if (isOuter)
            collectionSelector = Expression.Lambda(call.Arguments[0], parameter);
        }
        ResultExpression innerResult;
        Parameter<Tuple> applyParameter;
        using (context.SubqueryParameterBindings.Bind(collectionSelector.Parameters)) {
          innerResult = (ResultExpression) Visit(collectionSelector.Body);
          applyParameter = context.SubqueryParameterBindings.GetBound(parameter);
        }
        var recordSet = outerResult.RecordSet.Apply(applyParameter,
          innerResult.RecordSet.Alias(context.GetNextAlias()),
          isOuter ? ApplyType.Outer : ApplyType.Cross);
        return CombineResultExpressions(outerResult, innerResult, recordSet, resultSelector);
      }
    }

    private Expression VisitSelect(Expression expression, LambdaExpression le)
    {
      using (context.Bindings.Add(le.Parameters[0], (ResultExpression) Visit(expression))) {
        return BuildProjection(le);
      }
    }

    private Expression BuildProjection(LambdaExpression le)
    {
      using (new ParameterScope()) {
        resultMapping.Value = new ResultMapping();
        joinFinalEntity.Value = true;
        calculateExpressions.Value = true;
        var itemProjector = (LambdaExpression) Visit(le);
        var projector = (Expression<Func<RecordSet, object>>)BuildProjector(itemProjector, true);
        var source = context.Bindings[le.Parameters[0]];
        return new ResultExpression(
          typeof (IQueryable<>).MakeGenericType(le.Body.Type),
          source.RecordSet,
          resultMapping.Value,
          projector,
          itemProjector);
      }
    }

    private static LambdaExpression BuildProjector(LambdaExpression itemProjector, bool castToObject)
    {
      var rs = Expression.Parameter(typeof (RecordSet), "rs");
      var severalArguments = itemProjector.Parameters.Count > 1;
      var method = severalArguments
        ? typeof (Translator)
          .GetMethod("MakeProjection", BindingFlags.NonPublic | BindingFlags.Static)
          .MakeGenericMethod(itemProjector.Body.Type)
        : WellKnownMethods.EnumerableSelect.MakeGenericMethod(itemProjector.Parameters[0].Type, itemProjector.Body.Type);
      Expression body = (!severalArguments && itemProjector.Parameters[0].Type == typeof(Record)) 
        ? Expression.Call(method, Expression.Call(WellKnownMethods.RecordSetParse, rs), itemProjector)
        : Expression.Call(method, rs, itemProjector);
      var projector = Expression.Lambda(
        castToObject
          ? Expression.Convert(
              body,
              typeof(object))
          : body,
        rs);
      return projector;
    }

    private Expression VisitWhere(Expression expression, LambdaExpression le)
    {
      var parameter = le.Parameters[0];
      using (context.Bindings.Add(parameter, (ResultExpression) Visit(expression)))
      using (new ParameterScope()) {
        resultMapping.Value = new ResultMapping();
        var predicate = Visit(le);
        var source = context.Bindings[parameter];
        var recordSet = source.RecordSet.Filter((Expression<Func<Tuple, bool>>) predicate);
        return new ResultExpression(
          expression.Type,
          recordSet,
          source.Mapping,
          source.Projector,
          source.ItemProjector);
      }
    }

    private Expression VisitRootExists(Expression source, LambdaExpression predicate, bool notExists)
    {
      ResultExpression result;

      if (predicate == null)
        result = (ResultExpression)Visit(source);
      else
        result = (ResultExpression)VisitWhere(source, predicate);

      Expression<Func<RecordSet, object>> shaper;

      if (notExists)
        shaper = rs => !rs.First().GetValue<bool>(0);
      else
        shaper = rs => rs.First().GetValue<bool>(0);

      var newRecordSet = result.RecordSet.Existence(context.GetNextColumnAlias());
      return new ResultExpression(typeof (bool), newRecordSet, null, shaper, null);
    }

    private Expression VisitExists(Expression source, LambdaExpression predicate, bool notExists)
    {
      ResultExpression subquery;
      using (new ParameterScope()) {
        calculateExpressions.Value = false;
        joinFinalEntity.Value = false;
        if (predicate == null)
          subquery = (ResultExpression)Visit(source);
        else
          subquery = (ResultExpression)VisitWhere(source, predicate);
      }
      var filter = ApplyOneColumnSubquery(subquery.RecordSet.Existence(context.GetNextColumnAlias()));
      if (notExists)
        filter = Expression.Not(filter);
      return filter;
    }

    private Expression ApplyOneColumnSubquery(RecordSet subquery)
    {
      if (subquery.Header.Length != 1)
        throw new ArgumentException();
      var column = subquery.Header.Columns[0];
      var lambdaParameter = parameters.Value[0];
      var applyParameter = context.SubqueryParameterBindings.GetBound(lambdaParameter);
      context.SubqueryParameterBindings.InvalidateParameter(lambdaParameter);
      var oldResult = context.Bindings[lambdaParameter];
      int columnIndex = oldResult.RecordSet.Header.Length;
      var newMapping = new ResultMapping();
      newMapping.Replace(oldResult.Mapping);
      newMapping.RegisterFieldMapping(column.Name, new Segment<int>(columnIndex, 1));
      var newRecordSet = oldResult.RecordSet.Apply(applyParameter, subquery);
      var newResult = new ResultExpression(
        oldResult.Type, newRecordSet, newMapping, oldResult.Projector, oldResult.ItemProjector);
      context.Bindings.ReplaceBound(lambdaParameter, newResult);
      return MakeTupleAccess(lambdaParameter, column.Type, Expression.Constant(columnIndex));
    }

    // Constructor

    /// <exception cref="InvalidOperationException">There is no current <see cref="Session"/>.</exception>
    internal Translator(TranslatorContext context)
      : base(context.Model)
    {
      this.context = context;
      this.recordIsUsed = new Parameter<bool>(oldValue => recordIsUsed.Value |= oldValue);
    }
  }
}