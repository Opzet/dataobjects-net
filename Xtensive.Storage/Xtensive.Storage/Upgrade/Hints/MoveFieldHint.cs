// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Ivan Galkin
// Created:    2009.10.14

using System;
using System.Diagnostics;
using Xtensive.Core;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Reflection;

namespace Xtensive.Storage.Upgrade
{
  /// <summary>
  /// Move field hint.
  /// </summary>
  [Serializable]
  public class MoveFieldHint : UpgradeHint
  {
    private const string ToStringFormat = "Move field: {0}.{1} -> {2}.{3}";

    /// <summary>
    /// Gets the source type.
    /// </summary>
    public string SourceType { get; private set; }
    /// <summary>
    /// Gets the source field.
    /// </summary>
    public string SourceField { get; private set; }
    /// <summary>
    /// Gets the target type.
    /// </summary>
    public Type TargetType { get; private set; }
    /// <summary>
    /// Gets the target field.
    /// </summary>
    public string TargetField { get; private set; }

    /// <inheritdoc/>
    public override string ToString()
    {
      return string.Format(ToStringFormat,
        SourceType, SourceField, TargetType.GetFullName(), TargetField);
    }

    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="sourceType">Value for <see cref="SourceType"/>.</param>
    /// <param name="sourceField">Value for <see cref="SourceField"/>.</param>
    /// <param name="targetType">Value for <see cref="TargetType"/>.</param>
    /// <param name="targetField">Value for <see cref="TargetField"/>.</param>
    public MoveFieldHint(string sourceType, string sourceField, Type targetType, string targetField)
    {
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(sourceType, "sourceType");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(sourceField, "sourceField");
      ArgumentValidator.EnsureArgumentNotNull(targetType, "targetType");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(targetField, "targetField");
      SourceType = sourceType;
      SourceField = sourceField;
      TargetType = targetType;
      TargetField = targetField;
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="sourceType">Value for <see cref="SourceType"/>.</param>
    /// <param name="field">Value for <see cref="SourceField"/> and <see cref="TargetField"/>.</param>
    /// <param name="targetType">Value for <see cref="TargetType"/>.</param>
    public MoveFieldHint(string sourceType, string field, Type targetType)
      : this(sourceType, field, targetType, field)
    {
    }
  }
}