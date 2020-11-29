// Copyright (C) 2019 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Kudelin
// Created:    2019.02.08

using System;
using Xtensive.Orm.Model;

namespace Xtensive.Orm.Tests.Linq.OfTypeTestModel
{
  public interface IBaseEntity : IEntity
  {
    [Field, Key]
    int Id { get; }

    [Field]
    long Field1 { get; set; }
  }

  public interface IBaseInterface : IEntity
  {
    [Field]
    ulong BaseField { get; set; }
  }

  public interface IA
  {
    long Field2 { get; set; }
  }

  public interface IB1 : IBaseInterface
  {
    [Field]
    Structure1 Field3 { get; set; }

    [Field]
    string Field4 { get; set; }
  }

  public interface IC : IBaseInterface
  {
    [Field]
    double Field5 { get; set; }

    [Field]
    EntitySet<IB1> Field6 { get; set; }
  }

  public interface IB2 : IBaseInterface
  {
    [Field]
    Structure1 Field3 { get; set; }

    [Field]
    string Field4 { get; set; }
  }

  public class BaseEntity : Entity, IBaseEntity
  {
    public int Id { get; private set; }

    public long Field1 { get; set; }
  }

  public class Structure1 : Structure
  {
    [Field]
    public int Value1 { get; set; }

    [Field]
    public Structure2 Value2 { get; set; }

    [Field]
    public string Value3 { get; set; }
  }

  public class Structure2 : Structure
  {
    [Field]
    public int Value1 { get; set; }

    [Field]
    public DateTime Value2 { get; set; }
  }

  public interface IB3ClassTable : IB2
  {
  }

  [HierarchyRoot(InheritanceSchema.ClassTable)]
  public class AClassTable : BaseEntity, IA
  {
    [Field]
    public long Field2 { get; set; }
  }

  public class B1ClassTable : AClassTable, IB1
  {
    public Structure1 Field3 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }
  }

  public class C1ClassTable : B1ClassTable, IC
  {
    public double Field5 { get; set; }

    public EntitySet<IB1> Field6 { get; set; }
  }

  public class B2ClassTable : AClassTable, IB1
  {
    [Field]
    public EntitySet<B2ClassTable> Field5 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }

    public Structure1 Field3 { get; set; }
  }

  public class B3ClassTable : AClassTable, IB3ClassTable
  {
    public ulong BaseField { get; set; }

    public Structure1 Field3 { get; set; }

    public string Field4 { get; set; }
  }


  public interface IB3SingleTable : IB2
  {
  }

  [HierarchyRoot(InheritanceSchema.SingleTable)]
  public class ASingleTable : BaseEntity, IA
  {
    [Field]
    public long Field2 { get; set; }
  }

  public class B1SingleTable : ASingleTable, IB1
  {
    public Structure1 Field3 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }
  }

  public class C1SingleTable : B1SingleTable, IC
  {
    public double Field5 { get; set; }

    public EntitySet<IB1> Field6 { get; set; }
  }

  public class B2SingleTable : ASingleTable, IB1
  {
    [Field]
    public EntitySet<B2SingleTable> Field5 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }

    public Structure1 Field3 { get; set; }
  }

  public class B3SingleTable : ASingleTable, IB3SingleTable
  {
    public ulong BaseField { get; set; }

    public Structure1 Field3 { get; set; }

    public string Field4 { get; set; }
  }


  public interface IB3ConcreteTable : IB2
  {
  }

  [HierarchyRoot(InheritanceSchema.ConcreteTable)]
  public class AConcreteTable : BaseEntity, IA
  {
    [Field]
    public long Field2 { get; set; }
  }

  public class B1ConcreteTable : AConcreteTable, IB1
  {
    public Structure1 Field3 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }
  }

  public class C1ConcreteTable : B1ConcreteTable, IC
  {
    public double Field5 { get; set; }

    public EntitySet<IB1> Field6 { get; set; }
  }

  public class B2ConcreteTable : AConcreteTable, IB1
  {
    [Field]
    public EntitySet<B2ConcreteTable> Field5 { get; set; }

    public ulong BaseField { get; set; }

    public string Field4 { get; set; }

    public Structure1 Field3 { get; set; }
  }

  public class B3ConcreteTable : AConcreteTable, IB3ConcreteTable
  {
    public ulong BaseField { get; set; }

    public Structure1 Field3 { get; set; }

    public string Field4 { get; set; }
  }
}