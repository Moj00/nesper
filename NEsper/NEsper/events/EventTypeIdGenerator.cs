///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.events.bean;

namespace com.espertech.esper.events
{
    public interface EventTypeIdGenerator
    {
        int GetTypeId(String eventTypeName);
        void AssignedType(String name, BeanEventType eventType);
    }
}
