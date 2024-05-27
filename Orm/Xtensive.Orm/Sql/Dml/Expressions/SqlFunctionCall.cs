// Copyright (C) 2009-2024 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xtensive.Core;

namespace Xtensive.Sql.Dml
{
  [Serializable]
  public class SqlFunctionCall: SqlExpression
  {
    /// <summary>
    /// Gets the expressions.
    /// </summary>
    public IList<SqlExpression> Arguments { get; private set; }

    /// <summary>
    /// Gets the function type.
    /// </summary>
    public SqlFunctionType FunctionType { get; private set; }

    public override void ReplaceWith(SqlExpression expression)
    {
      ArgumentValidator.EnsureArgumentNotNull(expression, "expression");
      ArgumentValidator.EnsureArgumentIs<SqlFunctionCall>(expression, "expression");
      var replacingExpression = (SqlFunctionCall) expression;
      FunctionType = replacingExpression.FunctionType;
      Arguments.Clear();
      foreach (SqlExpression argument in replacingExpression.Arguments)
        Arguments.Add(argument);
    }

    internal override object Clone(SqlNodeCloneContext context)
    {
      if (!context.NodeMapping.TryGetValue(this, out var clone)) {
        context.NodeMapping[this] = clone = new SqlFunctionCall(FunctionType, Arguments.Select(o => (SqlExpression) o.Clone(context)).ToArray(Arguments.Count));
      }
      return clone;
    }

    public override void AcceptVisitor(ISqlVisitor visitor)
    {
      visitor.Visit(this);
    }

    // Constructors

    internal SqlFunctionCall(SqlFunctionType functionType, IEnumerable<SqlExpression> arguments)
      : base(SqlNodeType.FunctionCall)
    {
      FunctionType = functionType;
      Arguments = new Collection<SqlExpression>();
      foreach (SqlExpression argument in arguments)
        Arguments.Add(argument);
    }

    internal SqlFunctionCall(SqlFunctionType functionType, params SqlExpression[] arguments)
      : base(SqlNodeType.FunctionCall)
    {
      FunctionType = functionType;
      Arguments = new Collection<SqlExpression>();
      if (arguments != null)
        foreach (SqlExpression argument in arguments)
          Arguments.Add(argument);
    }
  }
}