// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.09.14

using System;
using Xtensive.Storage.Building.Definitions;

namespace Xtensive.Storage.Building.FixupActions
{
  [Serializable]
  internal class BuildImplementorListAction : TypeAction
  {
    public override void Run()
    {
      FixupActionProcessor.Process(this);
    }

    public override string ToString()
    {
      return string.Format("Build implementor list for '{0}' interface", Type.Name);
    }

    public BuildImplementorListAction(TypeDef type)
      : base(type)
    {
    }
  }
}