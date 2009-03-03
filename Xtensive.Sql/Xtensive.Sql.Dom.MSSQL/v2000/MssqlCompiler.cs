// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Sql.Common;
using Xtensive.Sql.Dom.Compiler;
using Xtensive.Sql.Dom.Dml;

namespace Xtensive.Sql.Dom.Mssql.v2000
{
  public class MssqlCompiler : SqlCompiler
  {
    private const int MillisecondsPerDay = 24 * 60 * 60 * 1000;
    private static readonly SqlExpression DateFirst = Sql.FunctionCall(MssqlTranslator.DateFirst);

    /// <inheritdoc/>
    public override void Visit(SqlFunctionCall node)
    {
      switch (node.FunctionType) {
        case SqlFunctionType.Substring:
          if (node.Arguments.Count==2) {
            SqlExpression len = Sql.Length(node.Arguments[0]);
            node.Arguments.Add(len);
            base.Visit(node);
            if (node.Arguments.Contains(len))
              node.Arguments.Remove(len);
            return;
          }
          break;

        case SqlFunctionType.IntervalConstruct:
        case SqlFunctionType.IntervalToMilliseconds:
          Visit(CastToLong(node.Arguments[0]));
          return;
        case SqlFunctionType.IntervalExtract:
          IntervalExtract(node.Arguments[0], node.Arguments[1]);
          return;
        case SqlFunctionType.IntervalDuration:
          Visit(Sql.Abs(node.Arguments[0]));
          return;
        case SqlFunctionType.DateTimeAddMonths:
          Visit(DateAddMonth(node.Arguments[0], node.Arguments[1]));
          return;
        case SqlFunctionType.DateTimeAddYears:
          Visit(DateAddYear(node.Arguments[0], node.Arguments[1]));
          return;
        case SqlFunctionType.DateTimeAddInterval:
          DateTimeAddInterval(node.Arguments[0], node.Arguments[1]);
          return;
        case SqlFunctionType.DateTimeSubtractInterval:
          DateTimeAddInterval(node.Arguments[0], -node.Arguments[1]);
          return;
        case SqlFunctionType.DateTimeSubtractDateTime:
          DateTimeSubtractDateTime(node.Arguments[0], node.Arguments[1]);
          return;
        case SqlFunctionType.DateTimeTruncate:
          DateTimeTruncate(node.Arguments[0]);
          return;
        case SqlFunctionType.DateTimeConstruct:
          Visit(DateAddDay(DateAddMonth(DateAddYear(Sql.Literal(new DateTime(2001, 1, 1)),
            node.Arguments[0] - 2001),
            node.Arguments[1] - 1),
            node.Arguments[2] - 1));
          return;

        case SqlFunctionType.Extract:
          if (((SqlLiteral<SqlDateTimePart>)node.Arguments[0]).Value == SqlDateTimePart.DayOfWeek) {
            Visit((DatePartWeekDay(node.Arguments[1]) + DateFirst + 6) % 7);
            return;
          }
          break;
      }

      base.Visit(node);
    }

    private void DateTimeTruncate(SqlExpression date)
    {
      Visit(DateAddMillisecond(DateAddSecond(DateAddMinute(DateAddHour(date,
        -Sql.Extract(SqlDateTimePart.Hour, date)),
        -Sql.Extract(SqlDateTimePart.Minute, date)),
        -Sql.Extract(SqlDateTimePart.Second, date)),
        -Sql.Extract(SqlDateTimePart.Millisecond, date)));
    }

    private void DateTimeSubtractDateTime(SqlExpression date1, SqlExpression date2)
    {
      Visit(
        CastToLong(DateDiffDay(date2, date1)) * MillisecondsPerDay
          + DateDiffMillisecond(DateAddDay(date2, DateDiffDay(date2, date1)), date1)
        );
    }

    private void DateTimeAddInterval(SqlExpression date, SqlExpression interval)
    {
      Visit(
        DateAddMillisecond(DateAddDay(date, interval / MillisecondsPerDay), interval % MillisecondsPerDay)
        );
    }

    private void IntervalExtract(SqlExpression partExpression, SqlExpression source)
    {
      var part = ((SqlLiteral<SqlIntervalPart>) partExpression).Value;
      
      switch (part) {
        case SqlIntervalPart.Day:
          Visit(CastToLong(source / MillisecondsPerDay));
          return;
        case SqlIntervalPart.Hour:
          Visit(CastToLong(source / (60 * 60 * 1000)) % 24);
          return;
        case SqlIntervalPart.Minute:
          Visit(CastToLong(source / (60 * 1000)) % 60);
          return;
        case SqlIntervalPart.Second:
          Visit(CastToLong(source / 1000) % 60);
          return;
        case SqlIntervalPart.Millisecond:
          Visit(source % 1000);
          return; 
      }
    }

    private static SqlCast CastToLong(SqlExpression arg)
    {
      return Sql.Cast(arg, SqlDataType.Int64);
    }

    private static SqlUserFunctionCall DatePartWeekDay(SqlExpression date)
    {
      return Sql.FunctionCall(MssqlTranslator.DatePartWeekDay, date);
    }

    private static SqlUserFunctionCall DateDiffDay(SqlExpression date1, SqlExpression date2)
    {
      return Sql.FunctionCall(MssqlTranslator.DateDiffDay, date1, date2);
    }

    private static SqlUserFunctionCall DateDiffMillisecond(SqlExpression date1, SqlExpression date2)
    {
      return Sql.FunctionCall(MssqlTranslator.DateDiffMillisecond, date1, date2);
    }

    private static SqlUserFunctionCall DateAddYear(SqlExpression date, SqlExpression years)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddYear, years, date);
    }

    private static SqlUserFunctionCall DateAddMonth(SqlExpression date, SqlExpression months)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddMonth, months, date);
    }

    private static SqlUserFunctionCall DateAddDay(SqlExpression date, SqlExpression days)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddDay, days, date);
    }

    private static SqlUserFunctionCall DateAddHour(SqlExpression date, SqlExpression hours)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddHour, hours, date);
    }

    private static SqlUserFunctionCall DateAddMinute(SqlExpression date, SqlExpression minutes)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddMinute, minutes, date);
    }

    private static SqlUserFunctionCall DateAddSecond(SqlExpression date, SqlExpression seconds)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddSecond, seconds, date);
    }

    private static SqlUserFunctionCall DateAddMillisecond(SqlExpression date, SqlExpression milliseconds)
    {
      return Sql.FunctionCall(MssqlTranslator.DateAddMillisecond, milliseconds, date);
    }

    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="driver">The driver.</param>
    public MssqlCompiler(SqlDriver driver)
      : base(driver)
    {
    }
  }
}