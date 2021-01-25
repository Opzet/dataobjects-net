// Copyright (C) 2009-2020 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Alexander Nikolaev
// Created:    2009.10.20

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Collections;
using Xtensive.Core;

namespace Xtensive.Orm.Internals.Prefetch
{
  internal sealed class Fetcher
  {
    private readonly SetSlim<EntityGroupTask> tasks = new SetSlim<EntityGroupTask>();
    private readonly HashSet<Key> foundKeys = new HashSet<Key>();

    private readonly PrefetchManager manager;

    public int ExecuteTasks(IEnumerable<GraphContainer> containers, bool skipPersist)
    {
      var batchExecuted = 0;
      try {
        var rootEntityContainers = containers
          .Where(container => container.RootEntityContainer != null)
          .Select(container => container.RootEntityContainer);
        foreach (var container in rootEntityContainers) {
          AddTask(container);
        }

        RegisterAllEntityGroupTasks();
        RegisterAllEntitySetTasks(containers);

        batchExecuted += manager.Owner.Session.ExecuteInternalDelayedQueries(skipPersist) ? 1 : 0;
        UpdateCacheFromAllEntityGroupTasks();
        UpdateCacheFromAllEntitySetTasks(containers);

        tasks.Clear();

        var referencedEntityContainers = containers
          .Select(container => container.ReferencedEntityContainers)
          .Where(referencedEntityPrefetchContainers => referencedEntityPrefetchContainers != null)
          .SelectMany(referencedEntityPrefetchContainers => referencedEntityPrefetchContainers);
        foreach (var container in referencedEntityContainers) {
          AddTask(container);
        }

        if (tasks.Count == 0) {
          return batchExecuted;
        }

        RegisterAllEntityGroupTasks();

        batchExecuted += manager.Owner.Session.ExecuteInternalDelayedQueries(skipPersist) ? 1 : 0;
        UpdateCacheFromAllEntityGroupTasks();
        return batchExecuted;
      }
      finally {
        tasks.Clear();
        foundKeys.Clear();
      }
    }

    private static void UpdateCacheFromAllEntitySetTasks(IEnumerable<GraphContainer> containers)
    {
      foreach (var container in containers.Where(c => c.EntitySetTasks != null)) {
        var entitySetPrefetchTasks = container.EntitySetTasks;
        foreach (var entitySetPrefetchTask in entitySetPrefetchTasks) {
          entitySetPrefetchTask.UpdateCache();
        }
      }
    }

    private static void RegisterAllEntitySetTasks(IEnumerable<GraphContainer> containers)
    {
      foreach (var container in containers.Where(c => c.EntitySetTasks != null)) {
        var entitySetPrefetchTasks = container.EntitySetTasks;
        foreach (var entitySetPrefetchTask in entitySetPrefetchTasks) {
          entitySetPrefetchTask.RegisterQueryTask();
        }
      }
    }

    private void AddTask(EntityContainer container)
    {
      var newTask = container.GetTask();
      if (newTask != null) {
        var existingTask = tasks[newTask];
        if (existingTask == null) {
          _ = tasks.Add(newTask);
          existingTask = newTask;
        }
        existingTask.AddKey(container.Key, container.ExactType);
      }
    }

    private void UpdateCacheFromAllEntityGroupTasks()
    {
      foreach (var task in tasks) {
        foundKeys.Clear();
        task.UpdateCache(foundKeys);
      }
    }

    private void RegisterAllEntityGroupTasks()
    {
      foreach (var task in tasks) {
        task.RegisterQueryTasks();
      }
    }


    // Constructors

    public Fetcher(PrefetchManager manager)
    {
      ArgumentValidator.EnsureArgumentNotNull(manager, "processor");

      this.manager = manager;
    }
  }
}