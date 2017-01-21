///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

using com.espertech.esper.client;

namespace com.espertech.esper.epl.join.exec.composite
{
    public interface CompositeIndexLookup
    {
        void Lookup(IDictionary<object, object> parent, ICollection<EventBean> result, CompositeIndexQueryResultPostProcessor postProcessor);
        void SetNext(CompositeIndexLookup value);
    }
}
