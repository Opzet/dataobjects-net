// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2008.11.02

using System;
using Xtensive.Core;
using Xtensive.Core.Aspects;
using Xtensive.Core.IoC;
using Xtensive.Core.Tuples;
using Xtensive.Storage.Model;
using Xtensive.Storage.Resources;
using Activator=Xtensive.Storage.Internals.Activator;

namespace Xtensive.Storage.Services
{
  /// <summary>
  /// Provides access to low-level operations with <see cref="Persistent"/> descendants.
  /// </summary>
  [Service(typeof(DirectPersistentAccessor))]
  [Infrastructure]
  public class DirectPersistentAccessor : SessionBound,
    ISessionService,
    IUsesSystemLogicOnlyRegions
  {
    #region Entity/Structure-related methods

    /// <summary>
    /// Creates new entity instance of the specified type.
    /// </summary>
    /// <param name="entityType">The type of entity to create. Must be descendant of the <see cref="Entity"/> type.</param>
    /// <returnsCreated entity.</returns>
    public Entity CreateEntity(Type entityType)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ArgumentValidator.EnsureArgumentNotNull(entityType, "entityType");
        if (!typeof (Entity).IsAssignableFrom(entityType))
          throw new InvalidOperationException(
            string.Format(Strings.TypeXIsNotAnYDescendant, entityType, typeof (Entity)));

        var key = Key.Create(entityType);
        var state = Session.CreateEntityState(key);
        return Activator.CreateEntity(entityType, state);
      }
    }

    /// <summary>
    /// Creates new entity instance of the specified type with the specified value.
    /// </summary>
    /// <param name="entityType">The type of structure to create. Must be descendant of the <see cref="Entity"/> type.</param>
    /// <param name="tuple">The tuple with entity data.</param>
    /// <returns>Created entity.</returns>
    public Entity CreateEntity(Type entityType, Tuple tuple)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ArgumentValidator.EnsureArgumentNotNull(entityType, "entityType");
        ArgumentValidator.EnsureArgumentNotNull(tuple, "tuple");
        if (!typeof (Entity).IsAssignableFrom(entityType))
          throw new InvalidOperationException(
            string.Format(Strings.TypeXIsNotAnYDescendant, entityType, typeof (Entity)));

        var domain = Session.Domain;
        var key = Key.Create(domain, domain.Model.Types[entityType], TypeReferenceAccuracy.ExactType, tuple);
        var state = Session.CreateEntityState(key);
        return Activator.CreateEntity(entityType, state);
      }
    }

    /// <summary>
    /// Creates new entity instance with the specified key. Key should have exact type.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Created entity.</returns>
    public Entity CreateEntity(Key key)
    {
      using (this.OpenSystemLogicOnlyRegion())
      {
        ArgumentValidator.EnsureArgumentNotNull(key, "key");
        if (key.TypeRef.Accuracy != TypeReferenceAccuracy.ExactType)
          throw new InvalidOperationException(string.Format(Strings.ExKeyXShouldHaveExactType, key));
        var entityType = key.TypeRef.Type;
        var domain = Session.Domain;
        var state = Session.CreateEntityState(key);
        return Activator.CreateEntity(entityType.UnderlyingType, state);
      }
    }

    /// <summary>
    /// Creates new <see cref="Structure"/> of the specified type.
    /// </summary>
    /// <param name="structureType">The type of structure to create. Must be descendant of the <see cref="Structure"/> type.</param>
    /// <returns>Created structure.</returns>
    public Structure CreateStructure(Type structureType)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ArgumentValidator.EnsureArgumentNotNull(structureType, "structureType");
        if (!typeof (Structure).IsAssignableFrom(structureType))
          throw new InvalidOperationException(string.Format(Strings.TypeXIsNotAnYDescendant, structureType, typeof (Structure)));

        return Activator.CreateStructure(structureType, null, null);
      }
    }

    /// <summary>
    /// Creates new <see cref="Structure"/> of the specified type filled with provided data.
    /// </summary>
    /// <param name="structureType">The type of structure to create. Must be descendant of the <see cref="Structure"/> type.</param>
    /// <param name="structureData">The structure data tuple.</param>
    /// <returns>Created structure.</returns>
    public Structure CreateStructure(Type structureType, Tuple structureData)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ArgumentValidator.EnsureArgumentNotNull(structureType, "structureType");
        if (!typeof(Structure).IsAssignableFrom(structureType))
          throw new InvalidOperationException(string.Format(Strings.TypeXIsNotAnYDescendant, structureType, typeof(Structure)));

        return Activator.CreateStructure(structureType, structureData);
      }
    }

    /// <summary>
    /// Gets the value of the specified persistent field of the target.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <param name="field">The field.</param>
    /// <returns>Field value.</returns>
    public object GetFieldValue(Persistent target, FieldInfo field)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ValidateArguments(target, field);
        return target.GetFieldValue(field);
      }
    }

    /// <summary>
    /// Gets the value of the specified persistent field of the target.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target">The target.</param>
    /// <param name="field">The field.</param>
    /// <returns>Field value.</returns>
    public T GetFieldValue<T>(Persistent target, FieldInfo field)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ValidateArguments(target, field);
        return target.GetFieldValue<T>(field);
      }
    }

    /// <summary>
    /// Gets the key of the entity, that is referenced by specified field 
    /// of the target persistent object.
    /// </summary>
    /// <remarks>
    /// Result is the same as <c>target.GetValue&lt;Entity&gt;(field).Key</c>, 
    /// but referenced entity will not be materialized.
    /// </remarks>
    /// <param name="target">The target persistent object.</param>
    /// <param name="field">The reference field. Field value type must be 
    /// <see cref="Entity"/> descendant.</param>
    /// <returns>Referenced entity key.</returns>
    /// <exception cref="InvalidOperationException">Field is not a reference field.</exception>
    public Key GetReferenceKey(Persistent target, FieldInfo field)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ValidateArguments(target, field);
        return target.GetReferenceKey(field);
      }
    }

    /// <summary>
    /// Sets the value of the specified persistent field of the target.
    /// </summary>
    /// <param name="target">The target persistent object.</param>
    /// <param name="field">The field to set value for.</param>
    /// <param name="value">The value to set.</param>
    public void SetFieldValue(Persistent target, FieldInfo field, object value)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ValidateArguments(target, field);
        target.SetFieldValue(field, value);
      }
    }

    /// <summary>
    /// Sets the value of the specified persistent field of the target.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="target">The target persistent object.</param>
    /// <param name="field">The field to set value for.</param>
    /// <param name="value">The value to set.</param>
    public void SetFieldValue<T>(Persistent target, FieldInfo field, T value)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ValidateArguments(target, field);
        target.SetFieldValue(field, value);
      }
    }

    /// <summary>
    /// Removes the specified entity.
    /// </summary>
    /// <param name="target">The entity to remove.</param>
    public void Remove(Entity target)
    {
      using (this.OpenSystemLogicOnlyRegion()) {
        ArgumentValidator.EnsureArgumentNotNull(target, "target");
        target.Remove();
      }
    }

    #endregion

    #region Protected members

    /// <summary>
    /// Validates the arguments passed to some of methods.
    /// </summary>
    /// <param name="target">The persistent type.</param>
    /// <param name="field">The field of persistent type.</param>
    protected static void ValidateArguments(Persistent target, FieldInfo field)
    {
      ArgumentValidator.EnsureArgumentNotNull(target, "target");
      ArgumentValidator.EnsureArgumentNotNull(field, "field");
      if (!target.Type.Fields.Contains(field))
        throw new InvalidOperationException(string.Format(
          Strings.ExTypeXDoesNotContainYField, target.Type.Name, field.Name));
    }

    #endregion


    // Constructors

    /// <inheritdoc/>
   [ServiceConstructor]
   public DirectPersistentAccessor(Session session)
      : base(session)
    {
    }
  }
}