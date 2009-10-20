// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Aleksey Gamzov
// Created:    2008.08.11

using System.Configuration;
using Xtensive.Core.Configuration;

namespace Xtensive.Storage.Configuration.Elements
{
  /// <summary>
  /// A root element of storage configuration section within a configuration file.
  /// </summary>
  public class ConfigurationSection : global::System.Configuration.ConfigurationSection
  {
    private const string DomainCollectionElementName = "domains";

    /// <summary>
    /// Gets the collection of domain configurations.
    /// </summary>
    [ConfigurationProperty(DomainCollectionElementName, IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(ConfigurationCollection<DomainConfigurationElement>), AddItemName = "domain")]
    public ConfigurationCollection<DomainConfigurationElement> Domains {
      get {
        return (ConfigurationCollection<DomainConfigurationElement>)base[DomainCollectionElementName];
      }
    }
  }
}