// Copyright (C) 2009-2024 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.

using System;
using Xtensive.Sql.Model;

namespace Xtensive.Sql.Ddl
{
  [Serializable]
  public class SqlAlterDomain : SqlStatement, ISqlCompileUnit
  {
    private SqlAction action;
    private Domain domain;

    public SqlAction Action {
      get {
        return action;
      }
    }

    public Domain Domain {
      get {
        return domain;
      }
    }


    internal override object Clone(SqlNodeCloneContext context) =>
      context.NodeMapping.TryGetValue(this, out var clone)
        ? clone
        : context.NodeMapping[this] = new SqlAlterDomain(domain, (SqlAction) action.Clone(context));

    public override void AcceptVisitor(ISqlVisitor visitor)
    {
      visitor.Visit(this);
    }

    internal SqlAlterDomain(Domain domain, SqlAction action)
      : base(SqlNodeType.Alter)
    {
      this.action = action;
      this.domain = domain;
    }
  }
}
