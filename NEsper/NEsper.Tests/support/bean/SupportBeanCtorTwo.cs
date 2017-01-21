///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

namespace com.espertech.esper.support.bean
{
    public class SupportBeanCtorTwo
    {
        public SupportBeanCtorTwo(SupportBean_ST0 st0,
                                  SupportBean_ST1 st1)
        {
            St0 = st0;
            St1 = st1;
        }

        public SupportBean_ST0 St0 { get; private set; }

        public SupportBean_ST1 St1 { get; private set; }
    }
}