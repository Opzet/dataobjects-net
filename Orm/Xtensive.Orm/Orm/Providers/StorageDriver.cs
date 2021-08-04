// Copyright (C) 2009-2021 Xtensive LLC.
// This code is distributed under MIT license terms.
// See the License.txt file in the project root for more information.
// Created by: Denis Krjuchkov
// Created:    2009.08.14

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Xtensive.Core;
using Xtensive.Linq;
using Xtensive.Orm.Logging;
using Xtensive.Orm.Configuration;
using Xtensive.Orm.Model;
using Xtensive.Sql;
using Xtensive.Sql.Compiler;
using Xtensive.Sql.Info;
using Xtensive.Sql.Model;
using Xtensive.Tuples;

namespace Xtensive.Orm.Providers
{
  /// <summary>
  /// SQL storage driver.
  /// </summary>
  public sealed partial class StorageDriver
  {
    private static readonly MethodInfo FactoryCreatorMethod = typeof(StorageDriver)
      .GetMethod(nameof(CreateNewHandler), BindingFlags.Static | BindingFlags.NonPublic);

    private readonly DomainConfiguration configuration;
    private readonly SqlDriver underlyingDriver;
    private readonly SqlTranslator translator;
    private readonly TypeMappingRegistry allMappings;
    private readonly bool isLoggingEnabled;
    private readonly bool hasSavepoints;

    private readonly IReadOnlyDictionary<Type, Func<IConnectionHandler>> handlerFactoriesCache;

    public ProviderInfo ProviderInfo { get; private set; }

    public StorageExceptionBuilder ExceptionBuilder { get; private set; }

    public ServerInfo ServerInfo { get; private set; }

    public string BuildBatch(string[] statements)
    {
      return translator.BuildBatch(statements);
    }

    public string BuildParameterReference(string parameterName)
    {
      return translator.ParameterPrefix + parameterName;
    }

    public DefaultSchemaInfo GetDefaultSchema(SqlConnection connection) =>
      underlyingDriver.GetDefaultSchema(connection);

    public Task<DefaultSchemaInfo> GetDefaultSchemaAsync(SqlConnection connection, CancellationToken token) =>
      underlyingDriver.GetDefaultSchemaAsync(connection, token);

    public SqlExtractionResult Extract(SqlConnection connection, IEnumerable<SqlExtractionTask> tasks)
    {
      var result = underlyingDriver.Extract(connection, tasks);
      FixExtractionResult(result);
      return result;
    }

    public async Task<SqlExtractionResult> ExtractAsync(
      SqlConnection connection, IEnumerable<SqlExtractionTask> tasks, CancellationToken token)
    {
      var result = await underlyingDriver.ExtractAsync(connection, tasks, token).ConfigureAwait(false);
      FixExtractionResult(result);
      return result;
    }

    public SqlCompilationResult Compile(ISqlCompileUnit statement)
    {
      var options = new SqlCompilerConfiguration {
        DatabaseQualifiedObjects = configuration.IsMultidatabase
      };
      return underlyingDriver.Compile(statement, options);
    }

    public SqlCompilationResult Compile(ISqlCompileUnit statement, NodeConfiguration nodeConfiguration)
    {
      SqlCompilerConfiguration options;
      if (configuration.ShareStorageSchemaOverNodes)
        options = new SqlCompilerConfiguration(nodeConfiguration.GetDatabaseMapping(), nodeConfiguration.GetSchemaMapping());
      else
        options = new SqlCompilerConfiguration();
      options.DatabaseQualifiedObjects = configuration.IsMultidatabase;
      return underlyingDriver.Compile(statement, options);
    }

    public DbDataReaderAccessor GetDataReaderAccessor(TupleDescriptor descriptor)
    {
      return new DbDataReaderAccessor(descriptor, descriptor.Select(GetTypeMapping));
    }

    public StorageDriver CreateNew(Domain domain)
    {
      ArgumentValidator.EnsureArgumentNotNull(domain, "domain");
      return new StorageDriver(underlyingDriver, ProviderInfo, domain.Configuration, GetModelProvider(domain), handlerFactoriesCache);
    }

    private static DomainModel GetNullModel()
    {
      return null;
    }

    private static Func<DomainModel> GetModelProvider(Domain domain)
    {
      return () => domain.Model;
    }

    private void FixExtractionResult(SqlExtractionResult result)
    {
      switch (ProviderInfo.ProviderName) {
      case WellKnown.Provider.SqlServer:
      case WellKnown.Provider.SqlServerCe:
        FixExtractionResultSqlServerFamily(result);
        break;
      case WellKnown.Provider.Sqlite:
        FixExtractionResultSqlite(result);
        break;
      }
    }

    private void FixExtractionResultSqlite(SqlExtractionResult result)
    {
      var tablesToFix =
        result.Catalogs
          .SelectMany(c => c.Schemas)
          .SelectMany(s => s.Tables)
          .Where(t => t.Name.EndsWith("-Generator", StringComparison.Ordinal)
            && t.TableColumns.Count==1
            && t.TableColumns[0].SequenceDescriptor==null);

      foreach (var table in tablesToFix) {
        var column = table.TableColumns[0];
        column.SequenceDescriptor = new SequenceDescriptor(column, 1, 1);
      }
    }

    private void FixExtractionResultSqlServerFamily(SqlExtractionResult result)
    {
      // Don't bother about tables for diagramming

      foreach (var schema in result.Catalogs.SelectMany(c => c.Schemas)) {
        var tables = schema.Tables;
        var sysdiagrams = tables["sysdiagrams"];
        if (sysdiagrams!=null)
          tables.Remove(sysdiagrams);
      }
    }

    private IReadOnlyCollection<IConnectionHandler> CreateConnectionHandlersFast(IEnumerable<Type> connectionHandlerTypes)
    {
      if (handlerFactoriesCache == null)
        return Array.Empty<IConnectionHandler>();
      var instances = new List<IConnectionHandler>(handlerFactoriesCache.Count);
      foreach (var type in connectionHandlerTypes) {
        if (handlerFactoriesCache.TryGetValue(type, out var factory)) {
          instances.Add(factory());
        }
      }
      return instances.ToArray();
    }

    private static IReadOnlyCollection<IConnectionHandler> CreateConnectionHandlers(IEnumerable<Type> connectionHandlerTypes,
      out IReadOnlyDictionary<Type, Func<IConnectionHandler>> factories)
    {
      factories = null;

      List<IConnectionHandler> instances;
      Dictionary<Type, Func<IConnectionHandler>> factoriesLocal;

      if(connectionHandlerTypes is IReadOnlyCollection<Type> asCollection) {
        if (asCollection.Count == 0)
          return Array.Empty<IConnectionHandler>();
        instances = new List<IConnectionHandler>(asCollection.Count);
        factoriesLocal = new Dictionary<Type, Func<IConnectionHandler>>(asCollection.Count);
      }
      else {
        if (connectionHandlerTypes.Any())
          return Array.Empty<IConnectionHandler>();
        instances = new List<IConnectionHandler>();
        factoriesLocal = new Dictionary<Type, Func<IConnectionHandler>>();
      }

      foreach (var type in connectionHandlerTypes) {
        var ctor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (ctor == null) {
          throw new NotSupportedException(string.Format(Strings.ExConnectionHandlerXHasNoParameterlessConstructor, type));
        }

        var handlerFactory = (Func<IConnectionHandler>) FactoryCreatorMethod.MakeGenericMethod(type).Invoke(null, null);
        instances.Add(handlerFactory());
        factoriesLocal[type] = handlerFactory;
      }
      factories = factoriesLocal; 
      return instances.ToArray();
    }

    private static Func<IConnectionHandler> CreateNewHandler<T>() where T : IConnectionHandler
    {
      return FastExpression.Lambda<Func<IConnectionHandler>>(
        Expression.Convert(Expression.New(typeof(T)), typeof(IConnectionHandler)))
        .Compile();
    }

    // Constructors

    public static StorageDriver Create(SqlDriverFactory driverFactory, DomainConfiguration configuration)
    {
      ArgumentValidator.EnsureArgumentNotNull(driverFactory, nameof(driverFactory));
      ArgumentValidator.EnsureArgumentNotNull(configuration, nameof(configuration));

      var handlers = CreateConnectionHandlers(configuration.Types.ConnectionHandlers, out var factories);
      var driverConfiguration = new SqlDriverConfiguration(handlers) {
        ForcedServerVersion = configuration.ForcedServerVersion,
        ConnectionInitializationSql = configuration.ConnectionInitializationSql,
        EnsureConnectionIsAlive = configuration.EnsureConnectionIsAlive,
      };

      var driver = driverFactory.GetDriver(configuration.ConnectionInfo, driverConfiguration);
      var providerInfo = ProviderInfoBuilder.Build(configuration.ConnectionInfo.Provider, driver);

      return new StorageDriver(driver, providerInfo, configuration, GetNullModel, factories);
    }

    public static async Task<StorageDriver> CreateAsync(
      SqlDriverFactory driverFactory, DomainConfiguration configuration, CancellationToken token)
    {
      ArgumentValidator.EnsureArgumentNotNull(driverFactory, nameof(driverFactory));
      ArgumentValidator.EnsureArgumentNotNull(configuration, nameof(configuration));

      var handlers = CreateConnectionHandlers(configuration.Types.ConnectionHandlers, out var factories);
      var driverConfiguration = new SqlDriverConfiguration(handlers) {
        ForcedServerVersion = configuration.ForcedServerVersion,
        ConnectionInitializationSql = configuration.ConnectionInitializationSql,
        EnsureConnectionIsAlive = configuration.EnsureConnectionIsAlive,
      };

      var driver = await driverFactory.GetDriverAsync(configuration.ConnectionInfo, driverConfiguration, token)
        .ConfigureAwait(false);
      var providerInfo = ProviderInfoBuilder.Build(configuration.ConnectionInfo.Provider, driver);

      return new StorageDriver(driver, providerInfo, configuration, GetNullModel, factories);
    }

    private StorageDriver(SqlDriver driver,
      ProviderInfo providerInfo,
      DomainConfiguration configuration,
      Func<DomainModel> modelProvider,
      IReadOnlyDictionary<Type,Func<IConnectionHandler>> factoryCache)
    {
      underlyingDriver = driver;
      ProviderInfo = providerInfo;
      this.configuration = configuration;
      ExceptionBuilder = new StorageExceptionBuilder(driver, configuration, modelProvider);
      allMappings = underlyingDriver.TypeMappings;
      translator = underlyingDriver.Translator;
      hasSavepoints = underlyingDriver.ServerInfo.ServerFeatures.Supports(ServerFeatures.Savepoints);
      isLoggingEnabled = SqlLog.IsLogged(LogLevel.Info); // Just to cache this value
      ServerInfo = underlyingDriver.ServerInfo;
      handlerFactoriesCache = factoryCache;
    }
  }
}