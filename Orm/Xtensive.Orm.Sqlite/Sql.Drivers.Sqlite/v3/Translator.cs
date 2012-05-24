// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Malisa Ncube
// Created:    2011.04.

using System;
using System.Text;
using System.Linq;
using Xtensive.Core;
using Xtensive.Sql.Compiler;
using Xtensive.Sql.Ddl;
using Xtensive.Sql.Dml;
using Xtensive.Sql.Drivers.Sqlite.Resources;
using Xtensive.Sql.Model;

namespace Xtensive.Sql.Drivers.Sqlite.v3
{
  internal class Translator : SqlTranslator
  {
    /// <inheritdoc/>
    public override string DateTimeFormatString
    {
      get { return @"\'yyyy\-MM\-dd HH\:mm\:ss\.ffffff\'"; }
    }

    /// <inheritdoc/>
    public override string TimeSpanFormatString
    {
      get { return @"{0}{1}"; }
    }

    /// <inheritdoc/>
    public override string DdlStatementDelimiter
    {
      get { return ";"; }
    }

    /// <inheritdoc/>
    public override string BatchItemDelimiter
    {
      get { return ";\r\n"; }
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
      base.Initialize();
      FloatNumberFormat.NumberDecimalSeparator = ".";
      DoubleNumberFormat.NumberDecimalSeparator = ".";
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SchemaNode node)
    {
      return QuoteIdentifier(node.DbName);
    }

    /// <inheritdoc/>
    public override string Translate(SqlFunctionType functionType)
    {
      switch (functionType) {
      case SqlFunctionType.Acos:
      case SqlFunctionType.Asin:
      case SqlFunctionType.Atan:
      case SqlFunctionType.Atan2:
      case SqlFunctionType.Sin:
      case SqlFunctionType.SessionUser:
      case SqlFunctionType.Sqrt:
      case SqlFunctionType.Square:
      case SqlFunctionType.Tan:
      case SqlFunctionType.Truncate:
      case SqlFunctionType.Position:
      case SqlFunctionType.Power:
        throw SqlHelper.NotSupported(functionType.ToString());
      case SqlFunctionType.Concat:
        return "||";
      case SqlFunctionType.IntervalAbs:
        return "ABS";
      case SqlFunctionType.Substring:
        return "SUBSTR";
      case SqlFunctionType.IntervalNegate:
        return "-";
      case SqlFunctionType.CurrentDate:
        return "DATE()";
      case SqlFunctionType.BinaryLength:
        return "LENGTH";
      case SqlFunctionType.LastAutoGeneratedId:
        return "LAST_INSERT_ROWID()";
      case SqlFunctionType.DateTimeAddMonths:
        return "DATE";
      case SqlFunctionType.DateTimeConstruct:
        return "DATETIME";
      }
      return base.Translate(functionType);
    }

    public override string Translate(SqlCompilerContext context, object literalValue)
    {
      var literalType = literalValue.GetType();
      if (literalType==typeof(byte[]))
        return ByteArrayToString((byte[]) literalValue);
      if (literalType==typeof (TimeSpan))
        return TimeSpanToString(((TimeSpan) literalValue), TimeSpanFormatString);
      if (literalType==typeof (Boolean))
        return ((Boolean) literalValue) ? "1" : "0";
      if (literalType==typeof (Guid))
        return ByteArrayToString(((Guid) literalValue).ToByteArray());
      return base.Translate(context, literalValue);
    }

    private string ByteArrayToString(byte[] literalValue)
    {
      var result = new StringBuilder(literalValue.Length * 2 + 3);
      result.Append("x'");
      result.AppendHexArray(literalValue);
      result.Append("'");
      return result.ToString();
    }

    public static string TimeSpanToString(TimeSpan value, string format)
    {
      int days = value.Days;
      int hours = value.Hours;
      int minutes = value.Minutes;
      int seconds = value.Seconds;
      int milliseconds = value.Milliseconds;

      bool negative = false;

      if (days < 0) {
        days = -days;
        negative = true;
      }

      if (hours < 0) {
        hours = -hours;
        negative = true;
      }

      if (minutes < 0) {
        minutes = -minutes;
        negative = true;
      }

      if (seconds < 0) {
        seconds = -seconds;
        negative = true;
      }

      if (milliseconds < 0) {
        milliseconds = -milliseconds;
        negative = true;
      }

      return String.Format(format, negative ? "-" : string.Empty, value.Ticks);
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlAlterTable node, AlterTableSection section)
    {
      switch (section) {
      case AlterTableSection.Entry:
        return "ALTER TABLE " + Translate(context, node.Table);
      case AlterTableSection.AddColumn:
        return "ADD";
      case AlterTableSection.Exit:
        return string.Empty;
      default:
        throw SqlHelper.NotSupported(node.Action.GetType().Name);
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, Constraint constraint, ConstraintSection section)
    {
      switch (section) {
      case ConstraintSection.Exit:
        ForeignKey fk = constraint as ForeignKey;
        if (fk!=null) {
          if (fk.OnUpdate==ReferentialAction.Cascade)
            return ") ON UPDATE CASCADE";
          if (fk.OnDelete==ReferentialAction.Cascade)
            return ") ON DELETE CASCADE";
        }
        return ")";
      default:
        return base.Translate(context, constraint, section);
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SequenceDescriptor descriptor, SequenceDescriptorSection section)
    {
      //switch (section) {
      //  case SequenceDescriptorSection.Increment:
      //    if (descriptor.Increment.HasValue)
      //      return "AUTOINCREMENT";
      //    return string.Empty;
      //}
      return string.Empty;
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlCreateTable node, CreateTableSection section)
    {
      switch (section) {
      case CreateTableSection.Entry:
        var builder = new StringBuilder();
        builder.Append("CREATE ");
        var temporaryTable = node.Table as TemporaryTable;
        if (temporaryTable!=null)
          builder.Append("TEMPORARY TABLE " + Translate(context, temporaryTable));
        else
          builder.Append("TABLE " + Translate(context, node.Table));
        return builder.ToString();
      case CreateTableSection.Exit:
        return string.Empty;
      }
      return base.Translate(context, node, section);
    }

    public override string Translate(SqlCompilerContext context, SqlCreateView node, NodeSection section)
    {
      switch (section) {
      case NodeSection.Entry:
        var sb = new StringBuilder();
        if (node.View.ViewColumns.Count > 0) {
          sb.Append(" (");
          bool first = true;
          foreach (DataTableColumn c in node.View.ViewColumns) {
            if (first)
              first = false;
            else
              sb.Append(ColumnDelimiter);
            sb.Append(c.DbName);
          }
          sb.Append(")");
        }
        return sb.ToString();
      case NodeSection.Exit:
        return string.Empty;
      default:
        return string.Empty;
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlDropSchema node)
    {
      throw SqlHelper.NotSupported(node.GetType().Name);
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlDropTable node)
    {
      return "DROP TABLE IF EXISTS " + Translate(context, node.Table);
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlDropView node)
    {
      return "DROP VIEW IF EXISTS " + Translate(context, node.View);
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlFunctionCall node, FunctionCallSection section, int position)
    {
      if (node.FunctionType==SqlFunctionType.LastAutoGeneratedId) {
        if (section==FunctionCallSection.Entry)
          return Translate(node.FunctionType);
        if (section==FunctionCallSection.Exit)
          return string.Empty;
      }
      switch (section) {
      case FunctionCallSection.ArgumentEntry:
        return string.Empty;
      case FunctionCallSection.ArgumentDelimiter:
        return ArgumentDelimiter;
      default:
        return base.Translate(context, node, section, position);
      }
    }

    public override string Translate(SqlCompilerContext context, SqlUpdate node, UpdateSection section)
    {
      switch (section) {
      case UpdateSection.Entry:
        return "UPDATE";
      case UpdateSection.Set:
        return "SET";
      case UpdateSection.From:
        return "FROM";
      case UpdateSection.Where:
        return "WHERE";
      }
      return string.Empty;
    }

    public override string Translate(SqlCompilerContext context, SqlCreateIndex node, CreateIndexSection section)
    {
      Index index = node.Index;
      switch (section) {
      case CreateIndexSection.Entry:
        return string.Format("CREATE {0} INDEX {1} ON {2} ", index.IsUnique ? "UNIQUE" : String.Empty, QuoteIdentifier(index.Name), QuoteIdentifier(index.DataTable.Name));

      case CreateIndexSection.Exit:
        return string.Empty;
      default:
        return base.Translate(context, node, section);
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlDropIndex node)
    {
      return string.Format("DROP INDEX {0}.{1}", node.Index.DataTable.Schema.Name, QuoteIdentifier(node.Index.DbName));
    }

    /// <inheritdoc/>
    public override string Translate(SqlJoinMethod method)
    {
      switch (method) {
      case SqlJoinMethod.Hash:
        return "HASH";
      case SqlJoinMethod.Merge:
        return "MERGE";
      case SqlJoinMethod.Loop:
        return "LOOP";
      case SqlJoinMethod.Remote:
        return "REMOTE";
      default:
        return string.Empty;
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlExtract extract, ExtractSection section)
    {
      switch (section) {
      case ExtractSection.Entry:
        return "STRFTIME(";
      case ExtractSection.From:
        if (extract.DateTimePart==SqlDateTimePart.Year)
          return "'%Y'";
        if (extract.DateTimePart==SqlDateTimePart.Month)
          return "'%m'";
        if (extract.DateTimePart==SqlDateTimePart.Day)
          return "'%d'";
        if (extract.DateTimePart==SqlDateTimePart.DayOfWeek)
          return "'%w'";
        if (extract.DateTimePart==SqlDateTimePart.Hour)
          return "'%H'";
        if (extract.DateTimePart==SqlDateTimePart.Minute)
          return "'%M'";
        if (extract.DateTimePart==SqlDateTimePart.Second)
          return "'%s'";
        if (extract.DateTimePart==SqlDateTimePart.Millisecond)
          return "'%f'";
        throw extract.DateTimePart!=SqlDateTimePart.Nothing
          ? SqlHelper.NotSupported(extract.DateTimePart.ToString())
          : SqlHelper.NotSupported(extract.IntervalPart.ToString());
      case ExtractSection.Exit:
        return ")";
      default:
        return string.Empty;
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlCast node, NodeSection section)
    {
      //http://www.sqlite.org/lang_expr.html
      switch (node.Type.Type) {
      case SqlType.Binary:
      case SqlType.Char:
      case SqlType.Interval:
      case SqlType.DateTime:
        switch (section) {
        case NodeSection.Entry:
          return "CAST(";
        case NodeSection.Exit:
          return "AS " + Translate(node.Type) + ")";
        default:
          throw new ArgumentOutOfRangeException("section");
        }
      case SqlType.Int16:
      case SqlType.Int32:
        switch (section) {
        case NodeSection.Entry:
          return "CAST(";
        case NodeSection.Exit:
          return "AS " + Translate(node.Type) + ")";
        default:
          throw new ArgumentOutOfRangeException("section");
        }
      case SqlType.Decimal:
      case SqlType.Double:
      case SqlType.Float:
        switch (section) {
        case NodeSection.Entry:
          return string.Empty;
        case NodeSection.Exit:
          return "+ 0.0";
        default:
          throw new ArgumentOutOfRangeException("section");
        }
      }
      return string.Empty;
    }

    /// <inheritdoc/>
    public virtual string Translate(SqlCompilerContext context, SqlRenameColumn action)
    {
      throw SqlHelper.NotSupported(action.GetType().Name);
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, SqlTrim node, TrimSection section)
    {
      switch (section) {
      case TrimSection.Entry:
        switch (node.TrimType) {
        case SqlTrimType.Leading:
          return "LTRIM(";
        case SqlTrimType.Trailing:
          return "RTRIM(";
        case SqlTrimType.Both:
          return "TRIM(";
        default:
          throw new ArgumentOutOfRangeException();
        }
      case TrimSection.Exit:
        switch (node.TrimType) {
        case SqlTrimType.Leading:
        case SqlTrimType.Trailing:
          return ")";
        case SqlTrimType.Both:
          return ")";
        default:
          throw new ArgumentOutOfRangeException();
        }
      default:
        throw new ArgumentOutOfRangeException();
      }
    }

    /// <inheritdoc/>
    public override string Translate(SqlCompilerContext context, TableColumn column, TableColumnSection section)
    {
      switch (section) {
      case TableColumnSection.Type:
        if (column.SequenceDescriptor==null)
          return base.Translate(context, column, section);
        return "integer"; // SQLite requires autoincrement columns to have exactly 'integer' type.
      case TableColumnSection.Exit:
        if (column.SequenceDescriptor==null)
          return string.Empty;
        var primaryKey = column.Table.TableConstraints.OfType<PrimaryKey>().FirstOrDefault();
        if (primaryKey==null)
          return string.Empty;
        return string.Format("CONSTRAINT {0} PRIMARY KEY AUTOINCREMENT", QuoteIdentifier(primaryKey.Name));
      case TableColumnSection.GeneratedExit:
        return string.Empty;
      default:
        return base.Translate(context, column, section);
      }
    }

    /// <inheritdoc/>
    public override string Translate(Collation collation)
    {
      return collation.DbName;
    }

    /// <inheritdoc/>
    public override string Translate(SqlTrimType type)
    {
      return string.Empty;
    }

    /// <inheritdoc/>
    public override string Translate(SqlLockType lockType)
    {
      if (lockType.Supports(SqlLockType.Shared))
        return "SHARED";
      if (lockType.Supports(SqlLockType.Exclusive))
        return "EXCLUSIVE";
      if (lockType.Supports(SqlLockType.SkipLocked) || lockType.Supports(SqlLockType.ThrowIfLocked))
        return base.Translate(lockType);
      return "PENDING"; //http://www.sqlite.org/lockingv3.html Not sure whether this is the best alternative.
    }

    /// <inheritdoc/>
    public override string Translate(SqlNodeType type)
    {
      switch (type) {
      case SqlNodeType.DateTimePlusInterval:
        return "+";
      case SqlNodeType.DateTimeMinusInterval:
      case SqlNodeType.DateTimeMinusDateTime:
        return "-";
      case SqlNodeType.Overlaps:
        throw SqlHelper.NotSupported(type.ToString());
      default:
        return base.Translate(type);
      }
    }

    protected virtual string TranslateClrType(Type type)
    {
      switch (Type.GetTypeCode(type)) {
      case TypeCode.Boolean:
        return "bit";
      case TypeCode.Byte:
      case TypeCode.SByte:
      case TypeCode.Int16:
      case TypeCode.UInt16:
      case TypeCode.Int32:
      case TypeCode.UInt32:
        return "int";
      case TypeCode.Int64:
      case TypeCode.UInt64:
        return "bigint";
      case TypeCode.Decimal:
      case TypeCode.Single:
      case TypeCode.Double:
        return "numeric";
      case TypeCode.Char:
      case TypeCode.String:
        return "text";
      case TypeCode.DateTime:
        return "timestamp";
      default:
        if (type==typeof (TimeSpan))
          return "bigint";
        if (type==typeof (Guid))
          return "guid";
        return "text";
      }
    }

    // Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Translator"/> class.
    /// </summary>
    /// <param name="driver">The driver.</param>
    protected internal Translator(SqlDriver driver)
      : base(driver)
    {
    }
  }
}