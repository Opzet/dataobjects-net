// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2009.04.13

using System;
using Xtensive.Core;
using Xtensive.Indexing.Measures;

namespace Xtensive.Indexing.Statistics
{
  internal class RangeMeasurableStatistics<TKey, TItem> : IStatistics<TKey>
  {
    private readonly IRangeMeasurable<TKey, TItem> measuresProvider;
    private readonly bool providerIsInMemory;

    public StatisticsData GetData(Range<Entire<TKey>> range)
    {
      return new StatisticsData(GetRecordCount(range), GetSeekCount(range));
    }

    private double GetRecordCount(Range<Entire<TKey>> range)
    {
      return (long) measuresProvider.GetMeasureResult(range, CountMeasure<TKey, long>.CommonName);
    }

    private double GetSeekCount(Range<Entire<TKey>> range)
    {
      // An index is in memory.
      if (providerIsInMemory)
        return 0;
      //TODO: Implement the calculation of seek count for a serialized index.
      throw new NotImplementedException();
    }


    // Constructors

    public RangeMeasurableStatistics(IRangeMeasurable<TKey, TItem> measuresProvider,
      bool providerIsInMemory)
    {
      ArgumentValidator.EnsureArgumentNotNull(measuresProvider, "measuresProvider");
      this.measuresProvider = measuresProvider;
      this.providerIsInMemory = providerIsInMemory;
    }
  }
}