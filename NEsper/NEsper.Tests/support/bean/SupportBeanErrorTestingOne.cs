///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

namespace com.espertech.esper.support.bean
{
    public class SupportBeanErrorTestingOne
    {
        public SupportBeanErrorTestingOne()
        {
            throw new ApplicationException("Default ctor manufactured test exception");
        }

        public string Value
        {
            get { throw new ApplicationException("Getter manufactured test exception"); }
            set { throw new ApplicationException("Setter manufactured test exception"); }
        }
    }
}
