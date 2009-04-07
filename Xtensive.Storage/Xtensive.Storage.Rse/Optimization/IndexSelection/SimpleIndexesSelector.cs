// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.04.06

using System.Collections.Generic;
using System.Linq.Expressions;
using Xtensive.Core;
using Xtensive.Storage.Model;

namespace Xtensive.Storage.Rse.Optimization.IndexSelection
{
  internal class SimpleIndexesSelector : IIndexesSelector
  {
    private readonly ICostEvaluator costEvaluator;

    #region Implementation of IIndexesSelector

    public Dictionary<IndexInfo, RangeSetInfo> Select(Dictionary<Expression,
      List<RsExtractionResult>> extractionResults)
    {
      var result = new Dictionary<IndexInfo, RangeSetInfo>(extractionResults.Count);
      foreach (var pair in extractionResults) {
        var cheapestResult = SelectCheapestResult(pair.Value);
        if (!result.ContainsKey(cheapestResult.IndexInfo))
          result.Add(cheapestResult.IndexInfo, cheapestResult.RangeSetInfo);
        else
          result[cheapestResult.IndexInfo] = RangeSetExpressionBuilder
            .BuildUnite(result[cheapestResult.IndexInfo], cheapestResult.RangeSetInfo);
      }
      return result;
    }

    #endregion

    private RsExtractionResult SelectCheapestResult(IEnumerable<RsExtractionResult> extractionResults)
    {
      RsExtractionResult cheapestResult = null;
      double minimalCost = double.MaxValue;
      foreach (var result in extractionResults) {
        double currentCost;
        if (result.RangeSetInfo.AlwaysFull)
          currentCost = double.MaxValue;
        else
          currentCost = costEvaluator.Evaluate(result.IndexInfo, result.RangeSetInfo.GetRangeSet());
        if(currentCost <= minimalCost) {
          minimalCost = currentCost;
          cheapestResult = result;
        }
      }
      return cheapestResult;
    }

    // Constructors

    public SimpleIndexesSelector(ICostEvaluator costEvaluator)
    {
      ArgumentValidator.EnsureArgumentNotNull(costEvaluator, "costEvaluator");
      this.costEvaluator = costEvaluator;
    }
  }
}