// Copyright (C) 2007-2024 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Nick Svetlov
// Created:    2007.06.13

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Xtensive.Collections;
using System.Linq;
using Xtensive.Core;

using Xtensive.Sorting;
using JetBrains.Annotations;

namespace Xtensive.Reflection
{
  /// <summary>
  /// <see cref="Type"/> related helper \ extension methods.
  /// </summary>
  public static class TypeHelper
  {
    private class TypesEqualityComparer : IEqualityComparer<(Type, Type[])>
    {
      public bool Equals((Type, Type[]) x, (Type, Type[]) y) =>
         x.Item1 == y.Item1 && x.Item2.SequenceEqual(y.Item2);

      public int GetHashCode((Type, Type[]) obj)
      {
        var hash = obj.Item1.GetHashCode();
        for (int i = obj.Item2.Length; i-- > 0;) {
          hash = HashCode.Combine(hash, obj.Item2[i]);
        }
        return hash;
      }
    }

    private const string InvokeMethodName = "Invoke";

    private static readonly object EmitLock = new object();
    private static readonly int NullableTypeMetadataToken = WellKnownTypes.NullableOfT.MetadataToken;
    private static readonly int ValueTuple1MetadataToken = typeof(ValueTuple<>).MetadataToken;
    private static readonly int ValueTuple8MetadataToken = typeof(ValueTuple<,,,,,,,>).MetadataToken;
    private static readonly Module SystemCoreLibModule = WellKnownTypes.NullableOfT.Module;
    private static readonly Type CompilerGeneratedAttributeType = typeof(CompilerGeneratedAttribute);
    private static readonly string TypeHelperNamespace = typeof(TypeHelper).Namespace;

    #region Caches and cache items factories
#if NET8_0_OR_GREATER
    private static readonly ConcurrentDictionary<(Type, Type[]), ConstructorInvoker> ConstructorInvokerByTypes =
      new(new TypesEqualityComparer());
#endif
    private static readonly ConcurrentDictionary<(Type, Type[]), ConstructorInfo> ConstructorInfoByTypes =
      new(new TypesEqualityComparer());

    private static readonly ConcurrentDictionary<Type, Type[]> OrderedInterfaces = new();

    private static readonly ConcurrentDictionary<Type, Type[]> UnorderedInterfaces = new();

    private static readonly ConcurrentDictionary<Type, Type[]> OrderedCompatibles = new();

    private static readonly ConcurrentDictionary<(Type, Type), InterfaceMapping> interfaceMaps = new();

    private static readonly ConcurrentDictionary<(MethodInfo, Type), MethodInfo> GenericMethodInstances1 = new();

    private static readonly ConcurrentDictionary<(MethodInfo, Type, Type), MethodInfo> GenericMethodInstances2 = new();
#if NET8_0_OR_GREATER

    private static readonly ConcurrentDictionary<(MethodInfo, Type), MethodInvoker> GenericMethodInvokers1 = new();

    private static readonly ConcurrentDictionary<(MethodInfo, Type, Type), MethodInvoker> GenericMethodInvokers2 = new();
#endif

    private static readonly ConcurrentDictionary<(Type, Type), Type> GenericTypeInstances1 = new();

    private static readonly ConcurrentDictionary<(Type, Type, Type), Type> GenericTypeInstances2 = new();

    private static readonly Func<(MethodInfo genericDefinition, Type typeArgument), MethodInfo> GenericMethodFactory1 =
      key => key.genericDefinition.MakeGenericMethod(key.typeArgument);

    private static readonly Func<(MethodInfo genericDefinition, Type typeArgument1, Type typeArgument2), MethodInfo> GenericMethodFactory2 =
      key => key.genericDefinition.MakeGenericMethod(key.typeArgument1, key.typeArgument2);

    private static readonly Func<(Type genericDefinition, Type typeArgument), Type> GenericTypeFactory1 =
      key => key.genericDefinition.MakeGenericType(key.typeArgument);

    private static readonly Func<(Type genericDefinition, Type typeArgument1, Type typeArgument2), Type> GenericTypeFactory2 =
      key => key.genericDefinition.MakeGenericType(key.typeArgument1, key.typeArgument2);
#if NET8_0_OR_GREATER

    private static readonly Func<(MethodInfo genericDefinition, Type typeArgument), MethodInvoker> GenericMethodInvokerFactory1 =
      key => MethodInvoker.Create(key.genericDefinition.MakeGenericMethod(key.typeArgument));

    private static readonly Func<(MethodInfo genericDefinition, Type typeArgument1, Type typeArgument2), MethodInvoker> GenericMethodInvokerFactory2 =
      key => MethodInvoker.Create(key.genericDefinition.MakeGenericMethod(key.typeArgument1, key.typeArgument2));
#endif

    #endregion

    private static int createDummyTypeNumber = 0;
    private static AssemblyBuilder assemblyBuilder;
    private static ModuleBuilder moduleBuilder;

    /// <summary>
    /// Searches for associated class for <paramref name="forType"/>, creates its instance, if found.
    /// Otherwise returns <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Type of result. Can be ether class or interface.</typeparam>
    /// <param name="forType">Type to search the associate for.</param>
    /// <param name="foundForType">Type the associate was found for.</param>
    /// <param name="associateTypeSuffixes">Associate type name suffix.</param>
    /// <param name="constructorParams">Parameters to pass to associate constructor.</param>
    /// <returns>Newly created associate for <paramref name="forType"/>, if found;
    /// otherwise, <see langword="null"/>.</returns>
    public static T CreateAssociate<T>(Type forType, out Type foundForType, string[] associateTypeSuffixes,
      object[] constructorParams)
      where T : class =>
      CreateAssociate<T>(forType, out foundForType, associateTypeSuffixes, constructorParams,
        Array.Empty<Pair<Assembly, string>>());

    /// <summary>
    /// Searches for associated class for <paramref name="forType"/>, creates its instance, if found.
    /// Otherwise returns <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Type of result. Can be ether class or interface.</typeparam>
    /// <param name="forType">Type to search the associate for.</param>
    /// <param name="foundForType">Type the associate was found for.</param>
    /// <param name="associateTypeSuffixes">Associate type name suffix.</param>
    /// <param name="highPriorityLocations">High-priority search locations (assembly + namespace pairs).</param>
    /// <param name="constructorParams">Parameters to pass to associate constructor.</param>
    /// <returns>Newly created associate for <paramref name="forType"/>, if found;
    /// otherwise, <see langword="null"/>.</returns>
    public static T CreateAssociate<T>(Type forType, out Type foundForType, string[] associateTypeSuffixes,
      object[] constructorParams, IEnumerable<Pair<Assembly, string>> highPriorityLocations)
      where T : class =>
      CreateAssociate<T>(forType, out foundForType, associateTypeSuffixes, constructorParams, highPriorityLocations,
        false);

    /// <summary>
    /// Searches for associated class for <paramref name="forType"/>, creates its instance, if found.
    /// Otherwise returns <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Type of result. Can be ether class or interface.</typeparam>
    /// <param name="forType">Type to search the associate for.</param>
    /// <param name="foundForType">Type the associate was found for.</param>
    /// <param name="associateTypeSuffixes">Associate type name suffix.</param>
    /// <param name="highPriorityLocations">High-priority search locations (assembly + namespace pairs).</param>
    /// <param name="constructorParams">Parameters to pass to associate constructor.</param>
    /// <returns>Newly created associate for <paramref name="forType"/>, if found;
    /// otherwise, <see langword="null"/>.</returns>
    /// <param name="exactTypeMatch">If <see langword="false"/> tries to create associates for base class, interfaces,
    /// arrays and <see cref="Nullable{T}"/>(if struct) too.</param>
    /// <exception cref="InvalidOperationException"><paramref name="forType"/> is generic type definition.</exception>
    public static T CreateAssociate<T>(Type forType, out Type foundForType, string[] associateTypeSuffixes,
      object[] constructorParams, IEnumerable<Pair<Assembly, string>> highPriorityLocations, bool exactTypeMatch)
      where T : class
    {
      ArgumentValidator.EnsureArgumentNotNull(forType, nameof(forType));
      if (forType.IsGenericTypeDefinition) {
        throw new InvalidOperationException(string.Format(
          Strings.ExCantCreateAssociateForGenericTypeDefinitions, GetShortName(forType)));
      }

      var locations = new List<Pair<Assembly, string>>(1);
      if (highPriorityLocations != null) {
        locations.AddRange(highPriorityLocations);
      }

      return
        CreateAssociateInternal<T>(forType, forType, out foundForType, associateTypeSuffixes, constructorParams,
          locations, exactTypeMatch);
    }

    private static T CreateAssociateInternal<T>(Type originalForType, Type currentForType, out Type foundForType,
      string[] associateTypeSuffixes, object[] constructorParams, List<Pair<Assembly, string>> locations,
      bool exactTypeMatch)
      where T : class
    {
      if (currentForType == null) {
        foundForType = null;
        return null;
      }

      string associateTypePrefix;
      Type[] genericArguments;

      // Possible cases: generic type, array type, regular type
      if (currentForType.IsGenericType) {
        // Generic type
        associateTypePrefix = currentForType.Name;
        genericArguments = currentForType.GetGenericArguments();
      }
      else if (currentForType.IsArray) {
        // Array type
        var elementType = currentForType.GetElementType();
        var rank = currentForType.GetArrayRank();
        associateTypePrefix = rank == 1 ? "Array`1" : $"Array{rank}D`1";

        genericArguments = new[] { elementType };
      }
      else if (currentForType == WellKnownTypes.Enum) {
        // Enum type
        var underlyingType = Enum.GetUnderlyingType(originalForType);
        associateTypePrefix = "Enum`2";
        genericArguments = new[] { originalForType, underlyingType };
      }
      else if (currentForType == WellKnownTypes.Array) {
        // Untyped Array type
        foundForType = null;
        return null;
      }
      else {
        // Regular type
        associateTypePrefix = currentForType.Name;
        genericArguments = null;
      }

      // Replacing 'I' at interface types
      if (currentForType.IsInterface && associateTypePrefix.StartsWith("I", StringComparison.Ordinal)) {
        associateTypePrefix = AddSuffix(associateTypePrefix.Substring(1), "Interface");
      }

      // Search for exact associate
      var result = CreateAssociateInternal<T>(originalForType, currentForType, out foundForType, associateTypePrefix,
        associateTypeSuffixes, locations, genericArguments, constructorParams);
      if (result != null) {
        return result;
      }

      if (exactTypeMatch) {
        foundForType = null;
        return null;
      }

      // Nothing is found; trying to find an associate for base type (except Object)
      var forTypeBase = currentForType.BaseType;
      if (forTypeBase != null) {
        while (forTypeBase != null && forTypeBase != WellKnownTypes.Object) {
          result = CreateAssociateInternal<T>(originalForType, forTypeBase, out foundForType,
            associateTypeSuffixes, constructorParams, locations, true);
          if (result != null) {
            return result;
          }

          forTypeBase = forTypeBase.BaseType;
        }
      }

      // Nothing is found; trying to find an associate for implemented interface
      var interfaces = GetInterfacesUnordered(currentForType).ToArray();
      var interfaceCount = interfaces.Length;
      var suppressed = new BitArray(interfaceCount);
      while (interfaceCount > 0) {
        // Suppressing all the interfaces inherited from others
        // to allow their associates to not conflict with each other
        for (var i = 0; i < interfaceCount; i++) {
          for (var j = 0; j < interfaceCount; j++) {
            if (i == j || !interfaces[i].IsAssignableFrom(interfaces[j])) {
              continue;
            }

            suppressed[i] = true;
            break;
          }
        }

        Type lastGoodInterfaceType = null;
        // Scanning non-suppressed interfaces
        for (var i = 0; i < interfaceCount; i++) {
          if (suppressed[i]) {
            continue;
          }

          var resultForInterface = CreateAssociateInternal<T>(originalForType, interfaces[i], out foundForType,
            associateTypeSuffixes, constructorParams, locations, true);
          if (resultForInterface == null) {
            continue;
          }

          if (result != null) {
            throw new InvalidOperationException(string.Format(
              Strings.ExMultipleAssociatesMatch,
              GetShortName(currentForType),
              GetShortName(result.GetType()),
              GetShortName(resultForInterface.GetType())));
          }

          result = resultForInterface;
          lastGoodInterfaceType = foundForType;
          foundForType = null;
        }

        if (result != null) {
          foundForType = lastGoodInterfaceType;
          return result;
        }

        // Moving suppressed interfaces to the beginning
        // to scan them on the next round
        var k = 0;
        for (var i = 0; i < interfaceCount; i++) {
          if (suppressed[i]) {
            interfaces[k] = interfaces[i];
            suppressed[k] = false;
            k++;
          }
        }

        interfaceCount = k;
      }

      // Nothing is found; trying to find an associate for Object type
      if (currentForType != WellKnownTypes.Object) {
        result = CreateAssociateInternal<T>(originalForType, WellKnownTypes.Object, out foundForType,
          "Object", associateTypeSuffixes,
          locations, null, constructorParams);
        if (result != null) {
          return result;
        }
      }

      // Nothing is found at all
      foundForType = null;
      return null;
    }

    private static T CreateAssociateInternal<T>(Type originalForType,
      Type currentForType,
      out Type foundForType,
      string associateTypePrefix,
      string[] associateTypeSuffixes,
      List<Pair<Assembly, string>> locations,
      Type[] genericArguments,
      object[] constructorParams)
      where T : class
    {
      var newLocationCount = 0;
      var pair = new Pair<Assembly, string>(typeof(T).Assembly, typeof(T).Namespace);
      if (locations.FindIndex(p => p.First == pair.First && p.Second == pair.Second) < 0) {
        locations.Add(pair);
        newLocationCount++;
      }

      pair = new Pair<Assembly, string>(currentForType.Assembly, currentForType.Namespace);
      if (locations.FindIndex(p => p.First == pair.First && p.Second == pair.Second) < 0) {
        locations.Add(pair);
        newLocationCount++;
      }

      try {
        for (int i = 0, count = locations.Count; i < count; i++) {
          var location = locations[i];
          for (var currentSuffix = 0; currentSuffix < associateTypeSuffixes.Length; currentSuffix++) {
            var associateTypeSuffix = associateTypeSuffixes[currentSuffix];
            // Trying exact type match (e.g. EnumerableInterfaceHandler`1<...>)
            var associateTypeName = AddSuffix($"{location.Second}.{associateTypePrefix}", associateTypeSuffix);
            var suffix = CorrectGenericSuffix(associateTypeName, genericArguments?.Length ?? 0);
            if (Activate(location.First, suffix, genericArguments, constructorParams) is T result) {
              foundForType = currentForType;
              return result;
            }

            // Trying to paste original type as generic parameter
            suffix = CorrectGenericSuffix(associateTypeName, 1);
            result = Activate(location.First, suffix, new[] { originalForType }, constructorParams) as T;
            if (result != null) {
              foundForType = currentForType;
              return result;
            }

            // Trying a generic one (e.g. EnumerableInterfaceHandler`2<T, ...>)
            Type[] newGenericArguments;
            if (genericArguments == null || genericArguments.Length == 0) {
              newGenericArguments = new[] { originalForType };
              associateTypeName = AddSuffix($"{location.Second}.{associateTypePrefix}`1", associateTypeSuffix);
            }
            else {
              newGenericArguments = new Type[genericArguments.Length + 1];
              newGenericArguments[0] = originalForType;
              Array.Copy(genericArguments, 0, newGenericArguments, 1, genericArguments.Length);
              associateTypeName = AddSuffix(
                $"{location.Second}.{TrimGenericSuffix(associateTypePrefix)}`{newGenericArguments.Length}",
                associateTypeSuffix);
            }

            suffix = CorrectGenericSuffix(associateTypeName, newGenericArguments.Length);
            result = Activate(location.First, suffix, newGenericArguments, constructorParams) as T;
            if (result != null) {
              foundForType = currentForType;
              return result;
            }
          }
        }

        foundForType = null;
        return null;
      }
      finally {
        for (var i = 0; i < newLocationCount; i++) {
          locations.RemoveAt(locations.Count - 1);
        }
      }
    }

    /// <summary>
    /// Creates new dummy type. Such types can be used
    /// as generic arguments (to instantiate unique generic
    /// instances).
    /// </summary>
    /// <param name="namePrefix">Prefix to include into type name.</param>
    /// <param name="inheritFrom">The type to inherit the dummy type from.</param>
    /// <param name="implementProtectedConstructorAccessor">If <see langword="true"/>, static method with name
    /// <see cref="DelegateHelper.AspectedFactoryMethodName"/> will be created for each constructor.</param>
    /// <returns><see cref="Type"/> object of newly created type.</returns>
    public static Type CreateDummyType(string namePrefix, Type inheritFrom, bool implementProtectedConstructorAccessor)
    {
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(namePrefix, nameof(namePrefix));
      ArgumentValidator.EnsureArgumentNotNull(inheritFrom, nameof(inheritFrom));


      var n = Interlocked.Increment(ref createDummyTypeNumber);
      var typeName = $"{TypeHelperNamespace}.Internal.{namePrefix}{n}";

      return CreateInheritedDummyType(typeName, inheritFrom, implementProtectedConstructorAccessor);
    }

    /// <summary>
    /// Creates new dummy type inherited from another type.
    /// </summary>
    /// <param name="typeName">Type name.</param>
    /// <param name="inheritFrom">The type to inherit the dummy type from.</param>
    /// <param name="implementProtectedConstructorAccessor">If <see langword="true"/>, static method with name
    /// <see cref="DelegateHelper.AspectedFactoryMethodName"/> will be created for each constructor.</param>
    /// <returns>New type.</returns>
    public static Type CreateInheritedDummyType(string typeName, Type inheritFrom,
      bool implementProtectedConstructorAccessor)
    {
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(typeName, nameof(typeName));
      ArgumentValidator.EnsureArgumentNotNull(inheritFrom, nameof(inheritFrom));
      EnsureEmitInitialized();
      lock (EmitLock) {
        var typeBuilder = moduleBuilder.DefineType(
          typeName,
          TypeAttributes.Public | TypeAttributes.Sealed,
          inheritFrom,
          Array.Empty<Type>()
        );
        const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var baseConstructor in inheritFrom.GetConstructors(bindingFlags)) {
          var parameters = baseConstructor.GetParameters();
          var parameterTypes = new Type[parameters.Length];
          for (var index = 0; index < parameters.Length; index++) {
            parameterTypes[index] = parameters[index].ParameterType;
          }
          var constructorBuilder = typeBuilder.DefineConstructor(
            baseConstructor.Attributes,
            CallingConventions.Standard,
            parameterTypes
          );

          // Create constructor
          var constructorIlGenerator = constructorBuilder.GetILGenerator();
          constructorIlGenerator.Emit(OpCodes.Ldarg_0);
          var parametersCount = parameterTypes.Length;
          for (short i = 1; i <= parametersCount; i++) {
            constructorIlGenerator.Emit(OpCodes.Ldarg, i);
          }

          constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
          constructorIlGenerator.Emit(OpCodes.Ret);

          // Create ProtectedConstructorAccessor
          if (implementProtectedConstructorAccessor) {
            var methodBuilder = typeBuilder.DefineMethod(DelegateHelper.AspectedFactoryMethodName,
              MethodAttributes.Private | MethodAttributes.Static,
              CallingConventions.Standard, typeBuilder.UnderlyingSystemType, parameterTypes);
            var accessorIlGenerator = methodBuilder.GetILGenerator();
            for (short i = 0; i < parameterTypes.Length; i++) {
              accessorIlGenerator.Emit(OpCodes.Ldarg, i);
            }

            accessorIlGenerator.Emit(OpCodes.Newobj, constructorBuilder);
            accessorIlGenerator.Emit(OpCodes.Ret);
          }
        }

        return typeBuilder.CreateTypeInfo().AsType();
      }
    }

    private static void EnsureEmitInitialized()
    {
      if (moduleBuilder != null) {
        return;
      }

      lock (EmitLock) {
        if (moduleBuilder != null) {
          return;
        }

        var assemblyName = new AssemblyName("Xtensive.TypeHelper.GeneratedTypes");
        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var tmp = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        Thread.MemoryBarrier();
        moduleBuilder = tmp;
      }
    }

    /// <summary>
    /// Adds suffix to specified generic type name.
    /// </summary>
    /// <param name="typeName">Type name to add suffix for.</param>
    /// <param name="suffix">Suffix to add.</param>
    /// <returns>Specified generic type name with its suffix.</returns>
    public static string AddSuffix(string typeName, string suffix)
    {
      var i = typeName.IndexOf('`');
      if (i >= 0) {
        return typeName.Substring(0, i) + suffix + typeName.Substring(i);
      }

      i = typeName.IndexOf('<');
      if (i >= 0) {
        return typeName.Substring(0, i) + suffix + typeName.Substring(i);
      }

      return typeName + suffix;
    }

    /// <summary>
    /// Instantiates specified generic type; returns <see langword="null"/>, if either no such a type,
    /// or an error has occurred.
    /// </summary>
    /// <param name="assembly">Assembly where the type is located.</param>
    /// <param name="typeName">Name of the type to instantiate.</param>
    /// <param name="genericArguments">Generic arguments for the type to instantiate
    /// (<see langword="null"/> means type isn't a generic type definition).</param>
    /// <param name="arguments">Arguments to pass to the type constructor.</param>
    /// <returns>An instance of specified type; <see langword="null"/>, if either no such a type,
    /// or an error has occurred.</returns>
    public static object Activate(Assembly assembly, string typeName, Type[] genericArguments,
      params object[] arguments)
    {
      ArgumentValidator.EnsureArgumentNotNull(assembly, nameof(assembly));
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(typeName, nameof(typeName));
      var type = assembly.GetType(typeName, false);
      return type == null ? null : Activate(type, genericArguments, arguments);
    }

    /// <summary>
    /// Instantiates specified generic type; returns <see langword="null"/>, if either no such a type,
    /// or an error has occurred.
    /// </summary>
    /// <param name="type">Generic type definition to instantiate.</param>
    /// <param name="genericArguments">Generic arguments for the type to instantiate
    /// (<see langword="null"/> means <paramref name="type"/> isn't a generic type definition).</param>
    /// <param name="arguments">Arguments to pass to the type constructor.</param>
    /// <returns>An instance of specified type; <see langword="null"/>, if either no such a type,
    /// or an error has occurred.</returns>
    [DebuggerStepThrough]
    public static object Activate(this Type type, Type[] genericArguments, params object[] arguments)
    {
      try {
        if (type.IsAbstract) {
          return null;
        }

        if (type.IsGenericTypeDefinition ^ (genericArguments != null)) {
          return null;
        }

        if (type.IsGenericTypeDefinition) {
          if (genericArguments == null) {
            genericArguments = Array.Empty<Type>();
          }

          var genericParameters = type.GetGenericArguments();
          if (genericParameters.Length != genericArguments.Length) {
            return null;
          }

          var genericParameterIndexes = genericParameters
            .Select((parameter, index) => (parameter, index))
            .ToDictionary(a => a.parameter, a => a.index);
          for (var i = 0; i < genericParameters.Length; i++) {
            var parameter = genericParameters[i];
            var constraints = parameter.GetGenericParameterConstraints();
            var argument = genericArguments[i];
            foreach (var constraint in constraints) {
              var projectedConstraint = constraint;
              if (constraint.IsGenericParameter) {
                projectedConstraint = genericArguments[genericParameterIndexes[constraint]];
              }
              else if (constraint.IsGenericType) {
                var constraintArguments = constraint.GetGenericArguments();
                var projectedConstraintArguments = new Type[constraintArguments.Length];
                for (var j = 0; j < constraintArguments.Length; j++) {
                  projectedConstraintArguments[j] =
                    genericParameterIndexes.TryGetValue(constraintArguments[j], out var index)
                      ? genericArguments[index]
                      : constraintArguments[j];
                }

                projectedConstraint = constraint
                  .GetGenericTypeDefinition()
                  .MakeGenericType(projectedConstraintArguments);
              }

              if (!projectedConstraint.IsAssignableFrom(argument)) {
                return null;
              }
            }
          }

          type = type.MakeGenericType(genericArguments);
        }

        if (arguments == null) {
          arguments = Array.Empty<object>();
        }

        const BindingFlags bindingFlags =
          BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var argumentTypes = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++) {
          var o = arguments[i];
          if (o == null) {
            // Actually a case when GetConstructor will fail,
            // so we should fall back to Activator.CreateInstance
            return Activator.CreateInstance(type, bindingFlags, null, arguments, null);
          }

          argumentTypes[i] = o.GetType();
        }

        var constructor = type.GetConstructorEx(bindingFlags, argumentTypes);
        return constructor == null ? null : constructor.Invoke(arguments);
      }
      catch (Exception) {
        return null;
      }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets <see cref="ConstructorInvoker"/> of the public constructor of type <paramref name="type"/>
    /// accepting specified <paramref name="argumentTypes"/>.
    /// </summary>
    /// <param name="type">The type to get the constructor for.</param>
    /// <param name="argumentTypes">The arguments.</param>
    /// <returns>
    /// Appropriate constructor invoker, if a single match is found;
    /// otherwise throws <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The <paramref name="type"/> has no constructors suitable for <paramref name="argumentTypes"/>
    /// -or- more than one such constructor.
    /// </exception>
    internal static ConstructorInvoker GetSingleConstructorInvoker(this Type type, Type[] argumentTypes) =>
      ConstructorInvokerByTypes.GetOrAdd((type, argumentTypes),
        static t => ConstructorExtractor(t) is ConstructorInfo ctor
         ? ConstructorInvoker.Create(ctor)
         : throw new InvalidOperationException(Strings.ExGivenTypeHasNoOrMoreThanOneCtorWithGivenParameters));

#endif
    /// <summary>
    /// Gets the public constructor of type <paramref name="type"/>
    /// accepting specified <paramref name="argumentTypes"/>.
    /// </summary>
    /// <param name="type">The type to get the constructor for.</param>
    /// <param name="argumentTypes">The arguments.</param>
    /// <returns>
    /// Appropriate constructor, if a single match is found;
    /// otherwise throws <see cref="InvalidOperationException"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The <paramref name="type"/> has no constructors suitable for <paramref name="argumentTypes"/>
    /// -or- more than one such constructor.
    /// </exception>
    [Obsolete]// obsolete to safely make it internal later on or delete
    public static ConstructorInfo GetSingleConstructor(this Type type, Type[] argumentTypes) =>
      ConstructorInfoByTypes.GetOrAdd((type, argumentTypes), ConstructorExtractor)
        ?? throw new InvalidOperationException(Strings.ExGivenTypeHasNoOrMoreThanOneCtorWithGivenParameters);

#if NET8_0_OR_GREATER
    /// <summary>
    /// Gets <see cref="ConstructorInvoker"/> of the public constructor of type <paramref name="type"/>
    /// accepting specified <paramref name="argumentTypes"/>.
    /// </summary>
    /// <param name="type">The type to get the constructor for.</param>
    /// <param name="argumentTypes">The arguments.</param>
    /// <returns>
    /// Appropriate constructor, if a single match is found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    [CanBeNull]
    internal static ConstructorInvoker GetSingleConstructorInvokerOrDefault(this Type type, Type[] argumentTypes) =>
      ConstructorInvokerByTypes.GetOrAdd((type, argumentTypes),
        static t => ConstructorExtractor(t) is ConstructorInfo ctor ? ConstructorInvoker.Create(ctor) : null);

#endif
    /// <summary>
    /// Gets the public constructor of type <paramref name="type"/>
    /// accepting specified <paramref name="argumentTypes"/>.
    /// </summary>
    /// <param name="type">The type to get the constructor for.</param>
    /// <param name="argumentTypes">The arguments.</param>
    /// <returns>
    /// Appropriate constructor, if a single match is found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    [CanBeNull]
    [Obsolete]// obsolete to safely make it internal later on
    public static ConstructorInfo GetSingleConstructorOrDefault(this Type type, Type[] argumentTypes) =>
      ConstructorInfoByTypes.GetOrAdd((type, argumentTypes), ConstructorExtractor);

    private static readonly Func<(Type, Type[]), ConstructorInfo> ConstructorExtractor = t => {
      (var type, var argumentTypes) = t;
      var constructors =
        from ctor in type.GetConstructors()
        let parameters = ctor.GetParameters()
        where parameters.Length == argumentTypes.Length
        let zipped = parameters.Zip(argumentTypes, (parameter, argumentType) => (parameter, argumentType))
        where (
          from pair in zipped
          let parameter = pair.parameter
          let parameterType = parameter.ParameterType
          let argumentType = pair.argumentType
          select
            !parameter.IsOut && (
              parameterType.IsAssignableFrom(argumentType ?? WellKnownTypes.Object) ||
              (!parameterType.IsValueType && argumentType == null) ||
              (parameterType.IsNullable() && argumentType == null)
            )
        ).All(passed => passed)
        select ctor;
      return constructors.SingleOrDefault();
    };

    /// <summary>
    /// Orders the specified <paramref name="types"/> by their inheritance
    /// (very base go first).
    /// </summary>
    /// <param name="types">The types to sort.</param>
    /// <returns>The list of <paramref name="types"/> ordered by their inheritance.</returns>
    public static IEnumerable<Type> OrderByInheritance(this IEnumerable<Type> types) =>
      TopologicalSorter.Sort(types, static (t1, t2) => t1.IsAssignableFrom(t2));

    /// <summary>
    /// Fast analogue of <see cref="Type.GetInterfaceMap"/>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="targetInterface">The target interface.</param>
    /// <returns>Interface map for the specified interface.</returns>
    public static InterfaceMapping GetInterfaceMapFast(this Type type, Type targetInterface) =>
      interfaceMaps.GetOrAdd((type, targetInterface), static pair => new InterfaceMapping(pair.Item1.GetInterfaceMap(pair.Item2)));

    /// <summary>
    /// Gets the interfaces of the specified type.
    /// Interfaces will be unordered.
    /// </summary>
    /// <param name="type">The type to get the interfaces of.</param>
    public static IReadOnlyList<Type> GetInterfacesUnordered(Type type) =>
      UnorderedInterfaces.GetOrAdd(type, static t => t.GetInterfaces());

    /// <summary>
    /// Gets the interfaces of the specified type.
    /// Interfaces will be ordered from the very base ones to ancestors.
    /// </summary>
    /// <param name="type">The type to get the interfaces of.</param>
    public static Type[] GetInterfacesOrderedByInheritance(this Type type) =>
      OrderedInterfaces.GetOrAdd(type, t => t.GetInterfaces().OrderByInheritance().ToArray());

    /// <summary>
    /// Gets the sequence of type itself, all its base types and interfaces.
    /// Types will be ordered from the very base ones to ancestors with the specified type in the end of sequence.
    /// </summary>
    /// <param name="type">The type to get compatible types for.</param>
    /// <returns>The interfaces of the specified type.</returns>
    public static Type[] GetCompatibles(this Type type) =>
      OrderedCompatibles.GetOrAdd(type,
        t => {
          var interfaces = GetInterfacesUnordered(t);
          var bases = EnumerableUtils.Unfold(t.BaseType, baseType => baseType.BaseType);
          return bases
            .Concat(interfaces)
            .OrderByInheritance()
            .Append(t)
            .ToArray();
        });

    /// <summary>
    /// Builds correct full generic type name.
    /// </summary>
    /// <param name="type">A <see cref="Type"/> which name is built.</param>
    /// <returns>Full type name.</returns>
    public static string GetFullName(this Type type)
    {
      if (type == null) {
        return null;
      }

      if (type.IsGenericParameter) {
        return type.Name;
      }

      var declaringType = type.DeclaringType;
      if (declaringType == null) {
        return type.InnerGetTypeName(useShortForm: false);
      }

      if (declaringType.IsGenericTypeDefinition) {
        declaringType =
          declaringType.MakeGenericType(
            type.GetGenericArguments()
              .Take(declaringType.GetGenericArguments().Length)
              .ToArray());
      }

      return $"{declaringType.GetFullName()}+{type.InnerGetTypeName(useShortForm: false)}";
    }

    /// <summary>
    /// Builds correct short generic type name (without namespaces).
    /// </summary>
    /// <param name="type">A <see cref="Type"/> which name is built.</param>
    /// <returns>Short type name.</returns>
    public static string GetShortName(this Type type)
    {
      if (type == null) {
        return null;
      }

      if (type.IsGenericParameter) {
        return type.Name;
      }

      var declaringType = type.DeclaringType;
      if (declaringType == null) {
        return type.InnerGetTypeName(useShortForm: true);
      }

      if (declaringType.IsGenericTypeDefinition) {
        declaringType =
          declaringType.MakeGenericType(
            type.GetGenericArguments()
              .Take(declaringType.GetGenericArguments().Length)
              .ToArray());
      }

      return $"{declaringType.GetShortName()}+{type.InnerGetTypeName(useShortForm: true)}";
    }

    private static string InnerGetTypeName(this Type type, bool useShortForm)
    {
      var result = useShortForm
        ? type.Name
        : type.DeclaringType != null // Is nested
          ? type.Name
          : type.Namespace + "." + type.Name;

      var arrayBracketPosition = result.IndexOf('[');
      if (arrayBracketPosition > 0) {
        result = result.Substring(0, arrayBracketPosition);
      }

      var arguments = type.GetGenericArguments();
      if (arguments.Length > 0) {
        if (type.DeclaringType != null) {
          arguments = arguments
            .Skip(type.DeclaringType.GetGenericArguments().Length)
            .ToArray();
        }

        var sb = new ValueStringBuilder(stackalloc char[4096]);
        sb.Append(TrimGenericSuffix(result));
        sb.Append('<');
        char? comma = default;
        foreach (var argument in arguments) {
          if (comma.HasValue) {
            sb.Append(comma.Value);
          }

          if (!type.IsGenericTypeDefinition) {
            sb.Append(InnerGetTypeName(argument, useShortForm));
          }

          comma = ',';
        }

        sb.Append('>');
        result = sb.ToString();
      }

      if (type.IsArray) {
        var sb = new StringBuilder(result);
        var elementType = type;
        while (elementType?.IsArray == true) {
          _ = sb.Append('[');
          var commaCount = elementType.GetArrayRank() - 1;
          for (var i = 0; i < commaCount; i++) {
            _ = sb.Append(',');
          }
          _ = sb.Append(']');
          elementType = elementType.GetElementType();
        }
        result = sb.ToString();
      }
      return result;
    }

    /// <summary>
    /// Indicates whether <paramref name="type"/> is a <see cref="Nullable{T}"/> type.
    /// </summary>
    /// <param name="type">Type to check.</param>
    /// <returns><see langword="True"/> if type is nullable type;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsNullable(this Type type) =>
      (type.MetadataToken ^ NullableTypeMetadataToken) == 0 && ReferenceEquals(type.Module, SystemCoreLibModule);

    /// <summary>
    /// Indicates whether <typeparamref name="T"/> type is a <see cref="Nullable{T}"/> type.
    /// </summary>
    /// <typeparam name="T">Type to check.</typeparam>
    /// <returns><see langword="True"/> if type is nullable type;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsNullable<T>() => typeof(T).IsNullable();

    /// <summary>
    /// Indicates whether <paramref name="type"/> is a final type.
    /// </summary>
    /// <param name="type">Type to check.</param>
    /// <returns><see langword="True"/> if type is final type;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsFinal(this Type type) => type.IsValueType || type.IsSealed;

    /// <summary>
    /// Indicates whether <typeparamref name="T"/> type is a final type.
    /// </summary>
    /// <typeparam name="T">Type to check.</typeparam>
    /// <returns><see langword="True"/> if type is final type;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsFinal<T>() => IsFinal(typeof(T));

    /// <summary>
    /// Gets the delegate "Invoke" method (describing the delegate) for
    /// the specified <paramref name="delegateType"/>.
    /// </summary>
    /// <param name="delegateType">Type of the delegate to get the "Invoke" method of.</param>
    /// <returns><see cref="MethodInfo"/> object describing the delegate "Invoke" method.</returns>
    public static MethodInfo GetInvokeMethod(this Type delegateType) => delegateType.GetMethod(InvokeMethodName);


    /// <summary>
    /// Determines whether given <paramref name="method"/> is a specification
    /// of the provided <paramref name="genericMethodDefinition"/>.
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> to check.</param>
    /// <param name="genericMethodDefinition">The <see cref="MethodInfo"/> of the generic method definition
    /// to check against.</param>
    /// <returns><see langword="true"/> if the specified <paramref name="method"/> is a specification
    /// of the provided <paramref name="genericMethodDefinition"/>.</returns>
    public static bool IsGenericMethodSpecificationOf(this MethodInfo method, MethodInfo genericMethodDefinition) =>
      method.MetadataToken == genericMethodDefinition.MetadataToken
      && (ReferenceEquals(method.Module, genericMethodDefinition.Module)
        || method.Module == genericMethodDefinition.Module)
      && method.IsGenericMethod && genericMethodDefinition.IsGenericMethodDefinition;

    /// <summary>
    /// Makes generic <see cref="MethodInfo"/> for given definition and type argument
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic definition of method.</param>
    /// <param name="typeArgument">Type argument for final generic method.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static MethodInfo CachedMakeGenericMethod(this MethodInfo genericDefinition, Type typeArgument) =>
      GenericMethodInstances1.GetOrAdd((genericDefinition, typeArgument), GenericMethodFactory1);

    /// <summary>
    /// Makes generic <see cref="MethodInfo"/> for given definition and type arguments
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic definition of method.</param>
    /// <param name="typeArgument1">First type argument for final generic method.</param>
    /// <param name="typeArgument2">Second type argument for final generic method.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static MethodInfo CachedMakeGenericMethod(this MethodInfo genericDefinition, Type typeArgument1, Type typeArgument2) =>
      GenericMethodInstances2.GetOrAdd((genericDefinition, typeArgument1, typeArgument2), GenericMethodFactory2);

#if NET8_0_OR_GREATER
    /// <summary>
    /// Makes <see cref="MethodInvoker"/> for generic <see cref="MethodInfo"/> for given definition and type argument
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic definition of method.</param>
    /// <param name="typeArgument">Type argument for final generic method.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static MethodInvoker CachedMakeGenericMethodInvoker(this MethodInfo genericDefinition, Type typeArgument) =>
      GenericMethodInvokers1.GetOrAdd((genericDefinition, typeArgument), GenericMethodInvokerFactory1);

    /// <summary>
    /// Makes <see cref="MethodInvoker"/> for generic <see cref="MethodInfo"/> for given definition and type arguments
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic definition of method.</param>
    /// <param name="typeArgument1">First type argument for final generic method.</param>
    /// <param name="typeArgument2">Second type argument for final generic method.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static MethodInvoker CachedMakeGenericMethodInvoker(this MethodInfo genericDefinition, Type typeArgument1, Type typeArgument2) =>
      GenericMethodInvokers2.GetOrAdd((genericDefinition, typeArgument1, typeArgument2), GenericMethodInvokerFactory2);
#endif

    /// <summary>
    /// Makes generic type of given type definition and type argument
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic type definition.</param>
    /// <param name="typeArgument">Type argument for final generic type.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static Type CachedMakeGenericType(this Type genericDefinition, Type typeArgument) =>
      GenericTypeInstances1.GetOrAdd((genericDefinition, typeArgument), GenericTypeFactory1);

    /// <summary>
    /// Makes generic type of given type definition and type argument
    /// or returns already existing instance from cache.
    /// </summary>
    /// <param name="genericDefinition">Generic type definition.</param>
    /// <param name="typeArgument1">First type argument for final generic type.</param>
    /// <param name="typeArgument2">Second type argument for final generic type.</param>
    /// <returns>Newly created instance or already existing one.</returns>
    public static Type CachedMakeGenericType(this Type genericDefinition, Type typeArgument1, Type typeArgument2) =>
      GenericTypeInstances2.GetOrAdd((genericDefinition, typeArgument1, typeArgument2), GenericTypeFactory2);

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is an ancestor or an instance of the
    /// provided <paramref name="openGenericBaseType"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="openGenericBaseType">Type of the generic. It is supposed this is an open generic type.</param>
    /// <returns>
    /// <see langword="true"/> if the specified <paramref name="type"/>  is an ancestor or an instance of the
    /// provided <paramref name="openGenericBaseType"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsOfGenericType(this Type type, Type openGenericBaseType) =>
      GetGenericType(type, openGenericBaseType) != null;

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is an ancestor or an instance of
    /// the provided <paramref name="openGenericBaseType"/> and returns closed generic type with the
    /// specified type arguments if found.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="openGenericBaseType">Open generic type to be matched.</param>
    /// <returns>
    /// A <see cref="Type"/> representing the closed generic version of <paramref name="openGenericBaseType"/>
    /// where type parameters are bound in case it exists in <paramref name="type"/>'s inheritance hierarchy;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    public static Type GetGenericType(this Type type, Type openGenericBaseType)
    {
      var definitionMetadataToken = openGenericBaseType.MetadataToken;
      var definitionModule = openGenericBaseType.Module;
      while (type != null && !ReferenceEquals(type, WellKnownTypes.Object)) {
        if ((type.MetadataToken ^ definitionMetadataToken) == 0 && ReferenceEquals(type.Module, definitionModule)) {
          return type;
        }

        type = type.BaseType;
      }

      return null;
    }

    /// <summary>
    /// Determines whether specified <paramref name="type"/> is an implementation of the
    /// provided <paramref name="openGenericInterface"/>.
    /// </summary>
    /// <param name="type">A <see cref="Type"/> instance to be checked.</param>
    /// <param name="openGenericInterface">A <see cref="Type"/> of an open generic <see langword="interface"/>
    /// to match the specified <paramref name="type"/> against.</param>
    /// <returns>
    /// <see langword="true"/> if the specified <paramref name="type"/> is an implementation of the
    /// provided <paramref name="openGenericInterface"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsOfGenericInterface(this Type type, Type openGenericInterface) =>
      GetGenericInterface(type, openGenericInterface) != null;

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is an implementation of the
    /// provided <paramref name="openGenericInterface"/> and returns a <see cref="Type"/> instance
    /// for the closed generic interface where type arguments are specified if implementation is found.
    /// </summary>
    /// <param name="type">The type to be checked.</param>
    /// <param name="openGenericInterface">Open generic <see langword="interface"/> to be matched.</param>
    /// <returns>
    /// A <see cref="Type"/> representing closed generic version of <paramref name="openGenericInterface"/>
    /// where type parameters are bound in case it is implemented by the <paramref name="type"/>;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    public static Type GetGenericInterface(this Type type, Type openGenericInterface)
    {
      var metadataToken = openGenericInterface.MetadataToken;
      var module = openGenericInterface.Module;
      if (type == null || ((type.MetadataToken ^ metadataToken) == 0 && ReferenceEquals(type.Module, module))) {
        return type;
      }

      // We don't use LINQ as we don't want to create a closure here
      foreach (var implementedInterface in GetInterfacesUnordered(type)) {
        if ((implementedInterface.MetadataToken ^ metadataToken) == 0
          && ReferenceEquals(implementedInterface.Module, module)) {
          return implementedInterface;
        }
      }

      return null;
    }

    /// <summary>
    /// Converts <paramref name="type"/> to type that can assign both
    /// values of <paramref name="type"/> and <see landword="null"/>.
    /// This method is a reverse for <see cref="StripNullable"/> method.
    /// </summary>
    /// <param name="type">A type to convert.</param>
    /// <returns>
    /// If <paramref name="type"/> is a reference type or a <see cref="Nullable{T}"/> instance
    /// returns <paramref name="type"/>.
    /// Otherwise returns <see cref="Nullable{T}"/> of <paramref name="type"/>.
    /// </returns>
    public static Type ToNullable(this Type type)
    {
      ArgumentValidator.EnsureArgumentNotNull(type, nameof(type));
      return type.IsValueType && !type.IsNullable()
        ? WellKnownTypes.NullableOfT.CachedMakeGenericType(type)
        : type;
    }

    /// <summary>
    /// Converts <paramref name="type"/> to <see cref="Nullable{T}"/> if <paramref name="type"/> is a value type.
    /// Otherwise returns just <paramref name="type"/>.
    /// This method is a reverse for <see cref="ToNullable"/> method.
    /// </summary>
    /// <param name="type">The type to process.</param>
    /// <returns>
    /// <see cref="Nullable{T}"/> of <paramref name="type"/> is specified type is a value type.
    /// Otherwise return just <paramref name="type"/>.
    /// </returns>
    public static Type StripNullable(this Type type)
    {
      ArgumentValidator.EnsureArgumentNotNull(type, nameof(type));
      return type.IsNullable()
        ? type.GetGenericArguments()[0]
        : type;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is anonymous type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>
    ///   <see langword="true" /> if the specified type is anonymous; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsAnonymous(this Type type)
    {
      var typeName = type.Name;
      return (type.Attributes & TypeAttributes.Public) == 0
        && type.BaseType == WellKnownTypes.Object
        && (typeName.StartsWith("<>", StringComparison.Ordinal) || typeName.StartsWith("VB$", StringComparison.Ordinal))
        && typeName.IndexOf("AnonymousType", StringComparison.Ordinal) >= 0
        && type.IsDefined(CompilerGeneratedAttributeType, false);
    }

    /// <summary>
    /// Determines whether the specified <paramref name="type"/> is closure type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>
    ///   <see langword="true" /> if the specified type is anonymous; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsClosure(this Type type)
    {
      var typeName = type.Name;
      return type.BaseType == WellKnownTypes.Object
        && (typeName.StartsWith("<>", StringComparison.Ordinal) || typeName.StartsWith("VB$", StringComparison.Ordinal))
        && typeName.IndexOf("DisplayClass", StringComparison.Ordinal) >= 0
        && type.IsDefined(CompilerGeneratedAttributeType, false);
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> is a public non-abstract inheritor of <paramref name="baseType"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="baseType">The base type.</param>
    /// <returns>
    /// <see langword="true"/> if type is a public non-abstract inheritor of specified base type;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsPublicNonAbstractInheritorOf(this Type type, Type baseType)
    {
      ArgumentValidator.EnsureArgumentNotNull(type, nameof(type));
      ArgumentValidator.EnsureArgumentNotNull(baseType, nameof(baseType));
      return type.IsPublic && !type.IsAbstract && baseType.IsAssignableFrom(type);
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> is numeric or nullable numeric
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>
    /// <see langword="true"/> If type is numeric or nullable numeric;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsNumericType(this Type type)
    {
      ArgumentValidator.EnsureArgumentNotNull(type, nameof(type));
      var nonNullableType = type.StripNullable();
      if (nonNullableType.IsEnum) {
        return false;
      }

      switch (Type.GetTypeCode(nonNullableType)) {
        case TypeCode.Byte:
        case TypeCode.SByte:
        case TypeCode.UInt16:
        case TypeCode.UInt32:
        case TypeCode.UInt64:
        case TypeCode.Int16:
        case TypeCode.Int32:
        case TypeCode.Int64:
        case TypeCode.Decimal:
        case TypeCode.Double:
        case TypeCode.Single:
          return true;
        default:
          return false;
      }
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> is generic form of <see cref="ValueTuple"/>
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    internal static bool IsValueTuple(this Type type)
    {
      // this stands on the theory that tokens for all generic versions of ValueTuple
      // go one after another.
      var currentToken = type.MetadataToken;
      return ((currentToken >= ValueTuple1MetadataToken) && currentToken <= ValueTuple8MetadataToken)
        && ReferenceEquals(type.Module, SystemCoreLibModule);
    }

    #region Private \ internal methods

    /// <summary>
    /// Gets information about field in closure.
    /// </summary>
    /// <param name="closureType">Closure type.</param>
    /// <param name="fieldType">Type of field in closure.</param>
    /// <returns>If field of <paramref name="fieldType"/> exists in closure then returns
    /// <see cref="MemberInfo"/> of that field, otherwise, <see langword="null"/>.</returns>
    internal static MemberInfo TryGetFieldInfoFromClosure(this Type closureType, Type fieldType) =>
      closureType.IsClosure()
        ? closureType.GetFields().FirstOrDefault(field => field.FieldType == fieldType)
        : null;

    private static string TrimGenericSuffix(string @string)
    {
      var backtickPosition = @string.IndexOf('`');
      return backtickPosition < 0 ? @string : @string.Substring(0, backtickPosition);
    }

    private static string CorrectGenericSuffix(string typeName, int argumentCount)
    {
      var backtickPosition = typeName.IndexOf('`');
      if (backtickPosition > 0) {
        typeName = typeName.Substring(0, backtickPosition);
      }

      return argumentCount == 0 ? typeName : $"{typeName}`{argumentCount}";
    }

    #endregion
  }
}
