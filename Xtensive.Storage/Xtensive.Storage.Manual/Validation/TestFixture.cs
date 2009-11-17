// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Kofman
// Created:    2009.10.28

using System;
using System.Transactions;
using NUnit.Framework;
using Xtensive.Core;
using Xtensive.Storage.Configuration;

namespace Xtensive.Storage.Manual.Validation
{
  [TestFixture]
  public class TestFixture
  {
    [Test]
    public void MainTest()
    {
      try {
        using (var transactionScope = Transaction.Open()) {
          using (var inconsistencyRegion = Storage.Validation.Disable()) {

            Person person = new Person();
            person.FirstName = "Mike";
            person.LastName = "Grooovy";
            person.Height = 1.5;
            person.BirthDay = new DateTime(1983, 03, 16);
            person.IsSubscribedOnNews = true;
            person.Email = "mike@grooovy.com";

            inconsistencyRegion.Complete();
          }
          transactionScope.Complete();
        }
      }
      catch (AggregateException exception) {
        Console.WriteLine("Following validation errors were found:");
        foreach (var error in exception.GetFlatExceptions())
          Console.WriteLine(error.Message);
      }
    }
  }
}