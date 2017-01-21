///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

using com.espertech.esper.epl.spec;
using com.espertech.esper.filter;

namespace com.espertech.esper.core.context.mgr
{
    public interface ContextControllerFactoryFactorySvc
    {
	    ContextControllerFactory Make(ContextControllerFactoryContext factoryContext, ContextDetail detail, IList<FilterSpecCompiled> optFiltersNested);
	}
} // end of namespace
