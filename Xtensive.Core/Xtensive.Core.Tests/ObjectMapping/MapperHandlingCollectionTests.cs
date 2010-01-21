// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2010.01.14

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core.Collections;
using Xtensive.Core.ObjectMapping;
using Xtensive.Core.Testing;
using Xtensive.Core.Tests.ObjectMapping.SourceModel;
using Xtensive.Core.Tests.ObjectMapping.TargetModel;
using NUnit.Framework;
using Action=Xtensive.Core.Tests.ObjectMapping.SourceModel.Action;

namespace Xtensive.Core.Tests.ObjectMapping
{
  [TestFixture]
  public sealed class MapperHandlingCollectionTests : MapperTestBase
  {
    [Test]
    public void CollectionTransformationTest()
    {
      var mapper = GetPetOwnerAnimalMapper();
      var source = GetSourcePetOwner();
      var target = (PetOwnerDto) mapper.Transform(source);
      Assert.IsNotNull(target);
      AssertAreEqual(source, target);
      Assert.AreEqual(source.Pets.Count, target.Pets.Count);
      foreach (var animalDto in target.Pets)
        Assert.IsNotNull(source.Pets.Where(p => p.Id == animalDto.Id).Single());
    }

    [Test]
    public void CollectionComparisonTest()
    {
      var mapper = GetPetOwnerAnimalMapper();
      var source = GetSourcePetOwner();
      var original = (PetOwnerDto) mapper.Transform(source);
      var modified = (PetOwnerDto) original.Clone();
      const int removedIndex = 1;
      var key = modified.Pets[removedIndex].Id;
      var removedAnimal0 = original.Pets.Single(p => p.Id == key);
      modified.Pets.RemoveAt(removedIndex);
      key = modified.Pets[removedIndex].Id;
      var removedAnimal1 = original.Pets.Single(p => p.Id == key);
      modified.Pets.RemoveAt(removedIndex);
      var newAnimal = new AnimalDto {Id = Guid.NewGuid(), Name = "N"};
      modified.Pets.Add(newAnimal);

      ValidateCollectionComparison(mapper, original, modified, newAnimal, removedAnimal0, removedAnimal1);
    }

    [Test]
    public void TransformationOfPrimitiveCustomCollectionTest()
    {
      var mapper = new DefaultMapper();
      mapper.MapType<PrimitiveCollectionContainer, PrimitiveCollectionContainerDto, Guid>(c => c.Id, c => c.Id)
        .Complete();
      var source = new PrimitiveCollectionContainer();
      source.Collection.Add(3);
      source.Collection.Add(2);
      source.Collection.Add(1);
      var target = (PrimitiveCollectionContainerDto) mapper.Transform(source);
      Assert.AreEqual(3, target.Collection[0]);
      Assert.AreEqual(2, target.Collection[1]);
      Assert.AreEqual(1, target.Collection[2]);
    }

    [Test]
    public void TransformationOfComplexCustomCollectionTest()
    {
      var mapper = new DefaultMapper();
      mapper.MapType<ComplexCollectionContainer, ComplexCollectionContainerDto, Guid>(c => c.Id, c => c.Id)
        .MapType<Person, PersonDto, int>(p => p.Id, p => p.Id).Complete();
      var source = new ComplexCollectionContainer();
      source.Collection.Add(GetSourcePerson(3));
      source.Collection.Add(GetSourcePerson(2));
      source.Collection.Add(GetSourcePerson(1));
      var target = (ComplexCollectionContainerDto) mapper.Transform(source);
      Assert.AreEqual(3, target.Collection[0].Id);
      Assert.AreEqual(2, target.Collection[1].Id);
      Assert.AreEqual(1, target.Collection[2].Id);
    }

    [Test]
    public void TransformationWhenGraphRootContainsPrimitiveObjectsTest()
    {
      var mapper = GetPersonStructureMapper();
      Person person;
      int intItem;
      string stringItem;
      Structure structureItem;
      var source = GetSourcePersonStructure(out person, out intItem, out stringItem, out structureItem);

      var target = (List<object>) mapper.Transform(source);
      Assert.AreEqual(source.Count, target.Count);
      Assert.AreEqual(((Person) source[0]).Id, ((PersonDto) target[0]).Id);
      Assert.AreEqual(((Person) source[0]).FirstName, ((PersonDto) target[0]).FirstName);
      Assert.AreEqual(intItem, target[1]);
      Assert.AreEqual(stringItem, target[2]);
      var targetStructureItem = (StructureDto) target[3];
      Assert.AreEqual(structureItem.DateTime, targetStructureItem.DateTime);
      Assert.AreEqual(structureItem.Int, targetStructureItem.Int);
      Assert.AreEqual(structureItem.String, targetStructureItem.String);

      var intArray = new[] {8, 9, 0};
      source.Add(intArray);
      AssertEx.ThrowsArgumentException(() => mapper.Transform(source));
    }

    [Test]
    public void ComparisonWhenRootsContainsPrimitiveObjectsTest()
    {
      var mapper = GetPersonStructureMapper();
      Person person;
      int intItem;
      string stringItem;
      Structure structureItem;
      var source = GetSourcePersonStructure(out person, out intItem, out stringItem, out structureItem);
      var original = (List<object>) mapper.Transform(source);
      var modified = Clone(original);
      var newFirstName = ((PersonDto) modified[0]).FirstName + "Modified0";
      ((PersonDto) modified[0]).FirstName = newFirstName;
      modified[1] = ((int) modified[1]) + 3;
      modified[2] = ((string) modified[2]) + "Modified1";
      var modifiedStructureItem = (StructureDto) modified[3];
      modifiedStructureItem.Int += 5;
      var operations = ((DefaultOperationSet) mapper.Compare(original, modified).Operations).ToList();
      Assert.AreEqual(1, operations.Count);
      ValidatePropertyOperation<PersonDto>(original[0], operations[0], p => p.FirstName, newFirstName,
        OperationType.SetProperty);

      var intArray = new[] {8, 9, 0};
      modified.Add(intArray);
      AssertEx.ThrowsArgumentException(() => mapper.Compare(original, modified));
    }

    [Test]
    public void TransformationOfPropertyBeingPrimitiveTypeCollectionTest()
    {
      var mapper = new DefaultMapper();
      mapper.MapType<Account, AccountDto, Guid>(a => a.Id, a => a.Id)
        .MapStructure<AccessRight, AccessRightDto>()
        .Complete();
      var source = new Account {
        AccessRights = new List<AccessRight> {
          new AccessRight {Action = Action.Read, ObjectId = new[] {1L, 2, 3, 4, 5}},
          new AccessRight {Action = Action.Write, ObjectId = new[] {0L, 9, 8, 7, 6}}
        },
        PasswordHash = new byte[] {1, 8, 89, 29, 50, 77}
      };
      var target = (AccountDto) mapper.Transform(source);
      Assert.AreEqual(source.Id, target.Id);
      Assert.AreEqual(source.AccessRights.Count, target.AccessRights.Count);
      Assert.AreEqual(source.PasswordHash.Length, target.PasswordHash.Length);
      Assert.IsTrue(source.PasswordHash.SequenceEqual(target.PasswordHash));
    }

    private static DefaultMapper GetPersonStructureMapper()
    {
      var mapper = new DefaultMapper();
      mapper.MapType<Person, PersonDto, int>(p => p.Id, p => p.Id)
        .MapStructure<Structure, StructureDto>().Complete();
      return mapper;
    }

    private static List<object> GetSourcePersonStructure(out Person person, out int intItem,
      out string stringItem, out Structure structureItem)
    {
      var source = new List<object>();
      person = new Person {BirthDate = DateTime.Now, FirstName = "Name"};
      source.Add(person);
      intItem = 5;
      source.Add(intItem);
      stringItem = "String";
      source.Add(stringItem);
      structureItem = new Structure {DateTime = DateTime.Now, Int = 3, String = "String"};
      source.Add(structureItem);
      return source;
    }
    
    private static void ValidateCollectionComparison(DefaultMapper mapper, PetOwnerDto original,
      PetOwnerDto modified, AnimalDto newAnimal, AnimalDto removedAnimal0, AnimalDto removedAnimal1)
    {
      var itemRemovalPublished0 = false;
      var itemRemovalPublished1 = false;
      var creationPublished = false;
      var removalPublished0 = false;
      var removalPublished1 = false;
      var additionPublished = false;
      var petPropertyCounts = CreateCountsForMutableProperties(typeof (AnimalDto), mapper);
      const string petsName = "Pets";
      var eventRaisingCount = 0;
      Action<OperationInfo> validator = descriptor => {
        eventRaisingCount++;
        switch (descriptor.Type) {
        case OperationType.AddItem:
          Assert.AreEqual(original, descriptor.Object);
          Assert.AreEqual(petsName, descriptor.PropertyPath[0].SystemProperty.Name);
          Assert.AreEqual(newAnimal, descriptor.Value);
          additionPublished = true;
          break;
        case OperationType.RemoveItem:
          if (ReferenceEquals(removedAnimal0, descriptor.Value))
            itemRemovalPublished0 = true;
          else if (ReferenceEquals(removedAnimal1, descriptor.Value))
            itemRemovalPublished1 = true;
          else
            Assert.Fail();
          Assert.AreEqual(petsName, descriptor.PropertyPath[0].SystemProperty.Name);
          Assert.AreEqual(original, descriptor.Object);
          break;
        case OperationType.CreateObject:
          creationPublished = true;
          Assert.AreEqual(newAnimal, descriptor.Object);
          Assert.IsNull(descriptor.PropertyPath);
          Assert.IsNull(descriptor.Value);
          break;
        case OperationType.RemoveObject:
          if (ReferenceEquals(removedAnimal0, descriptor.Object))
            removalPublished0 = true;
          else if (ReferenceEquals(removedAnimal1, descriptor.Object))
            removalPublished1 = true;
          else
            Assert.Fail();
          Assert.IsNull(descriptor.PropertyPath);
          Assert.IsNull(descriptor.Value);
          break;
        case OperationType.SetProperty:
          petPropertyCounts[descriptor.PropertyPath[0].SystemProperty.Name] += 1;
          Assert.AreSame(newAnimal, descriptor.Object);
          var expectedValue = descriptor.PropertyPath[0].SystemProperty.GetValue(newAnimal, null);
          Assert.AreEqual(expectedValue, descriptor.Value);
          break;
        default:
          Assert.Fail();
          break;
        }
      };
      ((DefaultOperationSet) mapper.Compare(original, modified).Operations).Apply(validator);
      Assert.AreEqual(7, eventRaisingCount);
      Assert.IsTrue(itemRemovalPublished0);
      Assert.IsTrue(itemRemovalPublished1);
      Assert.IsTrue(removalPublished0);
      Assert.IsTrue(removalPublished1);
      Assert.IsTrue(creationPublished);
      Assert.IsTrue(additionPublished);
    }
  }
}