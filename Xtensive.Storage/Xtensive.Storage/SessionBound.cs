// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.08.10

using System;
using System.Diagnostics;
using Xtensive.Core;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Aspects;
using Xtensive.Storage.Model;
using Xtensive.Storage.Resources;

namespace Xtensive.Storage
{
  /// <summary>
  /// Base class for all objects that are bound to the <see cref="Session"/> instance.
  /// </summary>
  public abstract class SessionBound : 
    IContextBound<Session>
  {
    private Session session;

    /// <summary>
    /// Gets <see cref="Session"/> which current instance is bound to.
    /// </summary>
    [Infrastructure]
    public Session Session {
      [DebuggerStepThrough]
      get { return session; }
      [DebuggerStepThrough]
      internal set { session = value; }
    }

    /// <summary>
    /// Gets the core services accessor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not registered in <see cref="DomainModel"/> of the 
    /// <see cref="Domain"/> this instance belongs to.</exception>
    [Infrastructure]
    protected internal CoreServiceAccessor CoreServices {
      [DebuggerStepThrough]
      get {
        if (!Session.Domain.Configuration.Types.Assemblies.Contains(GetType().Assembly))
          throw new InvalidOperationException(
            Strings.ExUnauthorizedAccessDeclarationOfCallerTypeIsNotInRegisteredAssembly);
        return session.CoreServices;
      }
    }

    #region IContextBound<Session> Members

    [Infrastructure]
    Session IContextBound<Session>.Context {
      [DebuggerStepThrough]
      get { return session; }
    }

    #endregion


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    protected SessionBound() 
      : this(Session.Current)
    {
    }

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/> 
    /// </summary>
    /// <param name="session"><see cref="Xtensive.Storage.Session"/>, to which current instance 
    /// is bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null" />.</exception>
    protected SessionBound(Session session)
    {
      if (session==null)
        throw new InvalidOperationException(
          Strings.ExSessionIsNotOpen);

      ArgumentValidator.EnsureArgumentNotNull(session, "session");
      this.session = session;
    }
  }
}