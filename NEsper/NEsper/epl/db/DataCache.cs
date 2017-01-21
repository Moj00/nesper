///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.epl.join.table;

namespace com.espertech.esper.epl.db
{
    /// <summary>
    /// Implementations serve as caches for historical or reference data retrieved
    /// via lookup keys consisting or one or more rows represented by a list of events.
    /// </summary>

    public interface DataCache : IDisposable
    {
        /// <summary>
        /// Ask the cache if the keyed value is cached, returning a list or rows if the key
        /// is in the cache,or returning null to indicate no such key cached. Zero rows may
        /// also be cached.
        /// </summary>
        /// <param name="lookupKeys">is the keys to look up in the cache</param>
        /// <returns>
        /// a list of rows that can be empty is the key was found in the cache, or null if 
        /// the key is not found in the cache
        /// </returns>
        EventTable[] GetCached(object[] lookupKeys);

        /// <summary>
        /// Puts into the cache a key and a list of rows, or an empty list if zero rows.
        /// <para/>
        /// The put method is designed to be called when the cache does not contain a key as
        /// determined by the get method. Implementations typically simply overwrite any keys
        ///  put into the cache that already existed in the cache.
        /// </summary>
        /// <param name="lookupKeys">is the keys to the cache entry</param>
        /// <param name="rows">is a number of rows</param>
        void PutCached(object[] lookupKeys, EventTable[] rows);

        /// <summary>
        /// Returns true if the cache is active and currently caching, or false if the
        /// cache is inactive and not currently caching
        /// </summary>
        /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
        /// <returns>true for caching enabled, false for no caching taking place</returns>
        bool IsActive { get; }
    }
}
