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
    [Serializable]
    public class SupportBeanSimple
    {
        public SupportBeanSimple(String myString, int myInt)
        {
            MyString = myString;
            MyInt = myInt;
        }

        public String MyString { get; set; }

        public int MyInt { get; set; }
    }
}
